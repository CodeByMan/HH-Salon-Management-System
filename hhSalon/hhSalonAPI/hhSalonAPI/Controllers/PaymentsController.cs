using hhSalon.Domain.Entities;
using hhSalon.Domain.Entities.Enums;
using hhSalon.Domain.Entities.Static;
using hhSalon.Services.Models.Dto;
using hhSalonAPI.Domain.Concrete;
using hhSalonAPI.Payments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Security.Claims;

namespace hhSalonAPI.Controllers
{
	[Route("api/[controller]")]
	[ApiController]
	public class PaymentsController : ControllerBase
	{
		private static readonly TimeSpan ReservationLifetime = TimeSpan.FromMinutes(30);
		private readonly AppDbContext _context;
		private readonly IPayPalService _payPal;

		public PaymentsController(AppDbContext context, IPayPalService payPal)
		{
			_context = context;
			_payPal = payPal;
		}

		[AllowAnonymous]
		[HttpGet("paypal-configuration")]
		public IActionResult Configuration()
			=> Ok(new PayPalConfigurationDto { Enabled = _payPal.IsConfigured, ClientId = _payPal.ClientId, Currency = _payPal.Currency });

		[Authorize(Roles = UserRoles.Client)]
		[EnableRateLimiting("payments")]
		[HttpPost("paypal/orders")]
		public async Task<IActionResult> CreateOrder([FromBody] PaymentAttendanceIdsDto request, CancellationToken cancellationToken)
		{
			if (!_payPal.IsConfigured)
				return StatusCode(StatusCodes.Status503ServiceUnavailable, new { Message = "PayPal is not configured." });

			var clientId = CurrentUserId();
			var transactionId = Guid.NewGuid().ToString("N");
			var placeholderOrderId = $"PENDING-{transactionId}";
			decimal amount;

			await using (var databaseTransaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken))
			{
				var validation = await GetPayableAttendances(request.AttendanceIds, clientId, null, true, cancellationToken);
				if (validation.Error != null)
					return validation.Error;

				amount = validation.Amount;
				var payment = new PaymentTransaction
				{
					Id = transactionId,
					ProviderOrderId = placeholderOrderId,
					ClientId = clientId,
					AttendanceIds = CanonicalIds(validation.Attendances.Select(a => a.Id)),
					Amount = amount,
					Currency = _payPal.Currency,
					Status = "CREATING",
					CreatedAtUtc = DateTime.UtcNow,
					ExpiresAtUtc = DateTime.UtcNow.Add(ReservationLifetime)
				};
				_context.PaymentTransactions.Add(payment);
				foreach (var attendance in validation.Attendances)
					attendance.PaymentTransactionId = transactionId;

				try
				{
					await _context.SaveChangesAsync(cancellationToken);
					await databaseTransaction.CommitAsync(cancellationToken);
				}
				catch (DbUpdateException)
				{
					return Conflict(new { Message = "These appointments are already reserved for another payment attempt." });
				}
			}

			PayPalProviderOrder providerOrder;
			try
			{
				providerOrder = await _payPal.CreateOrder(amount, clientId, transactionId, cancellationToken);
			}
			catch (HttpRequestException)
			{
				await ReleaseReservation(transactionId, "FAILED", cancellationToken);
				return StatusCode(StatusCodes.Status502BadGateway, new { Message = "PayPal order creation failed." });
			}

			if (string.IsNullOrWhiteSpace(providerOrder.Id)
				|| providerOrder.Amount != amount
				|| !string.Equals(providerOrder.Currency, _payPal.Currency, StringComparison.OrdinalIgnoreCase)
				|| providerOrder.CustomId != clientId
				|| providerOrder.InvoiceId != transactionId)
			{
				await ReleaseReservation(transactionId, "INVALID", cancellationToken);
				return StatusCode(StatusCodes.Status502BadGateway, new { Message = "PayPal returned an invalid order." });
			}

			try
			{
				_context.ChangeTracker.Clear();
				var storedTransaction = await _context.PaymentTransactions.FirstAsync(p => p.Id == transactionId, cancellationToken);
				storedTransaction.ProviderOrderId = providerOrder.Id;
				storedTransaction.Status = providerOrder.Status;
				await _context.SaveChangesAsync(cancellationToken);
			}
			catch (DbUpdateException)
			{
				await ReleaseReservation(transactionId, "FAILED", cancellationToken);
				return StatusCode(StatusCodes.Status500InternalServerError, new { Message = "The PayPal order could not be linked to the local payment transaction." });
			}

			return Ok(new PayPalOrderDto { OrderId = providerOrder.Id, Status = providerOrder.Status, Amount = amount, Currency = _payPal.Currency });
		}

		[Authorize(Roles = UserRoles.Client)]
		[EnableRateLimiting("payments")]
		[HttpPost("paypal/orders/{orderId}/capture")]
		public async Task<IActionResult> CaptureOrder(string orderId, [FromBody] PaymentAttendanceIdsDto request, CancellationToken cancellationToken)
		{
			if (!_payPal.IsConfigured)
				return StatusCode(StatusCodes.Status503ServiceUnavailable, new { Message = "PayPal is not configured." });

			var clientId = CurrentUserId();
			var transaction = await _context.PaymentTransactions.AsNoTracking().FirstOrDefaultAsync(
				p => p.ProviderOrderId == orderId && p.ClientId == clientId, cancellationToken);
			if (transaction == null || transaction.AttendanceIds != CanonicalIds(request.AttendanceIds))
				return BadRequest(new { Message = "The payment order does not match these appointments." });

			if (transaction.Status == "COMPLETED")
				return Ok(ToCaptureDto(transaction));
			if (transaction.ExpiresAtUtc <= DateTime.UtcNow)
			{
				await ReleaseReservation(transaction.Id, "EXPIRED", cancellationToken);
				return Conflict(new { Message = "The payment reservation expired. Create a new PayPal order." });
			}

			var validation = await GetPayableAttendances(request.AttendanceIds, clientId, transaction.Id, false, cancellationToken);
			if (validation.Error != null || validation.Amount != transaction.Amount)
				return validation.Error ?? Conflict(new { Message = "The appointment total changed before payment capture." });

			PayPalProviderOrder providerOrder;
			try
			{
				try
				{
					await _payPal.CaptureOrder(orderId, cancellationToken);
				}
				catch (HttpRequestException)
				{
					// PayPal may already have completed an idempotent retry; the authoritative GET below determines the result.
				}
				providerOrder = await _payPal.GetOrder(orderId, cancellationToken);
			}
			catch (HttpRequestException)
			{
				return StatusCode(StatusCodes.Status502BadGateway, new { Message = "PayPal capture verification failed." });
			}

			if (!IsValidCompletedOrder(providerOrder, transaction, clientId))
				return StatusCode(StatusCodes.Status502BadGateway, new { Message = "PayPal did not return a valid completed capture." });

			_context.ChangeTracker.Clear();
			await using var databaseTransaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
			var storedTransaction = await _context.PaymentTransactions
				.Include(payment => payment.Attendances)
				.FirstAsync(payment => payment.Id == transaction.Id, cancellationToken);
			if (storedTransaction.Status == "COMPLETED")
				return Ok(ToCaptureDto(storedTransaction));

			var expectedIds = CanonicalIds(request.AttendanceIds);
			var attendances = storedTransaction.Attendances.Where(a => a.ClientId == clientId).ToList();
			var amount = decimal.Round(attendances.Sum(a => a.Price), 2, MidpointRounding.AwayFromZero);
			if (CanonicalIds(attendances.Select(a => a.Id)) != expectedIds
				|| storedTransaction.AttendanceIds != expectedIds
				|| amount != storedTransaction.Amount
				|| attendances.Any(a => a.PaymentTransactionId != storedTransaction.Id
					|| a.IsPaid == YesNo.Yes || a.IsRendered == YesNo.Yes || a.Date.Date < DateTime.UtcNow.Date))
				return Conflict(new { Message = "One or more appointments are no longer payable." });

			foreach (var attendance in attendances)
				attendance.IsPaid = YesNo.Yes;

			storedTransaction.Status = "COMPLETED";
			storedTransaction.PayerId = providerOrder.PayerId;
			storedTransaction.PayerEmail = providerOrder.PayerEmail;
			storedTransaction.CapturedAtUtc = DateTime.UtcNow;
			await _context.SaveChangesAsync(cancellationToken);
			await databaseTransaction.CommitAsync(cancellationToken);

			return Ok(ToCaptureDto(storedTransaction));
		}

		private async Task<(List<Attendance> Attendances, decimal Amount, IActionResult Error)> GetPayableAttendances(
			IEnumerable<int> requestedIds, string clientId, string allowedTransactionId,
			bool releaseExpiredReservations, CancellationToken cancellationToken)
		{
			var ids = requestedIds.Distinct().OrderBy(id => id).ToList();
			if (ids.Count == 0 || ids.Count > 50)
				return (null, 0, BadRequest(new { Message = "Select between 1 and 50 appointments." }));

			var attendances = await _context.Attendances
				.Include(a => a.PaymentTransaction)
				.Where(a => ids.Contains(a.Id) && a.ClientId == clientId)
				.ToListAsync(cancellationToken);
			if (attendances.Count != ids.Count)
				return (null, 0, Forbid());

			if (releaseExpiredReservations)
			{
				foreach (var attendance in attendances.Where(a => a.PaymentTransaction != null
					&& a.PaymentTransaction.Status != "COMPLETED"
					&& a.PaymentTransaction.ExpiresAtUtc <= DateTime.UtcNow))
				{
					attendance.PaymentTransaction.Status = "EXPIRED";
					attendance.PaymentTransactionId = null;
					attendance.PaymentTransaction = null;
				}
			}

			if (allowedTransactionId == null && attendances.Any(a => a.PaymentTransactionId != null))
				return (null, 0, Conflict(new { Message = "One or more appointments are already reserved for payment." }));
			if (allowedTransactionId != null && attendances.Any(a => a.PaymentTransactionId != allowedTransactionId))
				return (null, 0, Conflict(new { Message = "The appointments are not reserved for this payment order." }));
			if (attendances.Any(a => a.IsPaid == YesNo.Yes || a.IsRendered == YesNo.Yes || a.Date.Date < DateTime.UtcNow.Date))
				return (null, 0, Conflict(new { Message = "Only future, unpaid and unrendered appointments can be paid." }));

			var amount = decimal.Round(attendances.Sum(a => a.Price), 2, MidpointRounding.AwayFromZero);
			return amount <= 0
				? (null, 0, Conflict(new { Message = "The payment amount must be greater than zero." }))
				: (attendances, amount, null);
		}

		private async Task ReleaseReservation(string transactionId, string status, CancellationToken cancellationToken)
		{
			_context.ChangeTracker.Clear();
			await using var databaseTransaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
			var transaction = await _context.PaymentTransactions
				.Include(payment => payment.Attendances)
				.FirstOrDefaultAsync(payment => payment.Id == transactionId, cancellationToken);
			if (transaction == null || transaction.Status == "COMPLETED")
				return;

			foreach (var attendance in transaction.Attendances)
				attendance.PaymentTransactionId = null;
			transaction.Status = status;
			await _context.SaveChangesAsync(cancellationToken);
			await databaseTransaction.CommitAsync(cancellationToken);
		}

		private bool IsValidCompletedOrder(PayPalProviderOrder providerOrder, PaymentTransaction transaction, string clientId)
			=> providerOrder.Id == transaction.ProviderOrderId
				&& string.Equals(providerOrder.Status, "COMPLETED", StringComparison.OrdinalIgnoreCase)
				&& providerOrder.Amount == transaction.Amount
				&& string.Equals(providerOrder.Currency, transaction.Currency, StringComparison.OrdinalIgnoreCase)
				&& providerOrder.CustomId == clientId
				&& providerOrder.InvoiceId == transaction.Id
				&& !string.IsNullOrWhiteSpace(providerOrder.PayerId)
				&& !string.IsNullOrWhiteSpace(providerOrder.PayerEmail);

		private string CurrentUserId()
			=> User.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new InvalidOperationException("Authenticated user ID is missing.");

		private static string CanonicalIds(IEnumerable<int> ids)
			=> string.Join(',', ids.Distinct().OrderBy(id => id));

		private static PayPalCaptureDto ToCaptureDto(PaymentTransaction transaction)
			=> new()
			{
				OrderId = transaction.ProviderOrderId,
				Status = transaction.Status,
				Amount = transaction.Amount,
				Currency = transaction.Currency,
				PayerId = transaction.PayerId,
				PayerEmail = transaction.PayerEmail
			};
	}
}
