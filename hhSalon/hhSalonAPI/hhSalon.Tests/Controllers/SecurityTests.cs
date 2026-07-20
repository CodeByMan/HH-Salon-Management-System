using hhSalon.Domain.Entities;
using hhSalon.Domain.Entities.Enums;
using hhSalon.Domain.Entities.Static;
using hhSalon.Services.Models.Dto;
using hhSalon.Services.Services.Implementations;
using hhSalon.Services.Services.Interfaces;
using hhSalon.Services.ViewModels;
using hhSalonAPI.Controllers;
using hhSalonAPI.Domain.Concrete;
using hhSalonAPI.Hubs;
using hhSalonAPI.Payments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.IdentityModel.Tokens.Jwt;
using System.Reflection;
using System.Security.Claims;

namespace hhSalon.Tests.Controllers
{
	public class SecurityTests
	{
		private AppDbContext _context = null!;
		private IConfiguration _configuration = null!;
		private AuthController _authController = null!;
		private CapturingEmailService _emailService = null!;

		[SetUp]
		public void Setup()
		{
			var options = new DbContextOptionsBuilder<AppDbContext>()
				.UseInMemoryDatabase(Guid.NewGuid().ToString())
				.Options;
			_context = new AppDbContext(options);
			_configuration = TestConfiguration();
			_emailService = new CapturingEmailService();
			_authController = NewAuthController();
		}

		[TearDown]
		public void TearDown()
		{
			_context.Database.EnsureDeleted();
			_context.Dispose();
		}

		[Test]
		public async Task PublicRegistrationAlwaysAssignsClientRole()
		{
			var result = await _authController.RegisterUser(ValidRegistration("client"));
			Assert.That(result, Is.TypeOf<OkObjectResult>());
			Assert.That(_context.Users.Single().Role, Is.EqualTo(UserRoles.Client));
		}

		[Test]
		public void RegistrationDtoDoesNotExposeRoleOrSecurityFields()
		{
			var propertyNames = typeof(RegisterUserDto).GetProperties().Select(p => p.Name).ToList();
			Assert.That(propertyNames, Does.Not.Contain("Role"));
			Assert.That(propertyNames, Does.Not.Contain("RoleId"));
			Assert.That(propertyNames, Does.Not.Contain("RefreshToken"));
			Assert.That(propertyNames, Does.Not.Contain("Token"));
		}

		[Test]
		public void WorkerAttendanceDtoOnlyAllowsCompletionStatus()
		{
			var propertyNames = typeof(WorkerAttendanceUpdateDto).GetProperties().Select(p => p.Name).ToArray();
			Assert.That(propertyNames, Is.EquivalentTo(new[] { "Id", "IsRendered" }));
		}

		[Test]
		public void AdminAndWorkerCreationRequireAdminRole()
		{
			AssertAdminOnly(nameof(AuthController.RegisterAdmin));
			AssertAdminOnly(nameof(AuthController.RegisterWorker));
		}

		[Test]
		public async Task UserCannotUpdateAnotherUser()
		{
			var usersController = new UsersController(new UsersService(_context));
			SetUser(usersController, "user-1", UserRoles.Client);
			var result = await usersController.UpdateUser(new UserUpdateDto { Id = "user-2", FirstName = "Other", LastName = "User", Email = "other@example.com" });
			Assert.That(result, Is.TypeOf<ForbidResult>());
		}

		[Test]
		public async Task AuthenticationUsesHttpOnlyCookiesAndStoresOnlyRefreshHash()
		{
			await _authController.RegisterUser(ValidRegistration("cookieuser"));
			var result = await _authController.Authenticate(new LoginDto { UserName = "cookieuser", Password = "Strong1!" });
			var cookies = _authController.Response.Headers.SetCookie.ToArray();
			var refresh = ExtractCookie(cookies, "hhSalon.refresh_token");
			var access = ExtractCookie(cookies, "hhSalon.access_token");
			var jwt = new JwtSecurityTokenHandler().ReadJwtToken(access);
			var stored = _context.Users.Single();

			Assert.That(result, Is.TypeOf<OkObjectResult>());
			Assert.That(cookies.All(cookie => cookie.Contains("httponly", StringComparison.OrdinalIgnoreCase)), Is.True);
			Assert.That(cookies.All(cookie => cookie.Contains("secure", StringComparison.OrdinalIgnoreCase)), Is.True);
			Assert.That(stored.RefreshTokenHash, Has.Length.EqualTo(64));
			Assert.That(stored.RefreshTokenHash, Is.Not.EqualTo(refresh));
			Assert.That(jwt.ValidTo - jwt.ValidFrom, Is.InRange(TimeSpan.FromMinutes(29), TimeSpan.FromMinutes(31)));
			Assert.That(jwt.Claims.Any(c => c.Type == ClaimTypes.Role && c.Value == UserRoles.Client), Is.True);
		}

		[Test]
		public async Task RefreshTokenRotatesAndOldCookieIsRejected()
		{
			await _authController.RegisterUser(ValidRegistration("refreshuser"));
			await _authController.Authenticate(new LoginDto { UserName = "refreshuser", Password = "Strong1!" });
			var first = ExtractCookie(_authController.Response.Headers.SetCookie.ToArray(), "hhSalon.refresh_token");

			_authController = NewAuthController(first);
			var refreshed = await _authController.Refresh();
			var second = ExtractCookie(_authController.Response.Headers.SetCookie.ToArray(), "hhSalon.refresh_token");

			_authController = NewAuthController(first);
			var replay = await _authController.Refresh();
			Assert.That(refreshed, Is.TypeOf<OkObjectResult>());
			Assert.That(second, Is.Not.EqualTo(first));
			Assert.That(replay, Is.TypeOf<UnauthorizedObjectResult>());
		}

		[Test]
		public async Task PasswordResetResponseDoesNotEnumerateAccountsAndTokenIsHashed()
		{
			await _authController.RegisterUser(ValidRegistration("resetuser"));
			var existing = await _authController.SendEmail("resetuser@example.com") as OkObjectResult;
			var missing = await _authController.SendEmail("missing@example.com") as OkObjectResult;
			var stored = _context.Users.Single().ResetPasswordTokenHash;

			Assert.That(existing?.Value?.ToString(), Is.EqualTo(missing?.Value?.ToString()));
			Assert.That(stored, Has.Length.EqualTo(64));
			Assert.That(_emailService.LastEmail, Is.Not.Null);
		}

		[Test]
		public async Task WorkerCanOnlyChangeAssignedAppointmentCompletionStatus()
		{
			_context.Users.AddRange(TestUser("client", UserRoles.Client), TestUser("worker", UserRoles.Worker));
			_context.Workers.Add(new Worker { Id = "worker", Address = "Address", Gender = "Other" });
			_context.Attendances.Add(new Attendance { Id = 1, ClientId = "client", WorkerId = "worker", Date = DateTime.Today.AddDays(1), Time = TimeSpan.FromHours(10), Price = 10m, IsPaid = YesNo.No, IsRendered = YesNo.No });
			await _context.SaveChangesAsync();

			var service = new AttendancesService(_context);
			await service.UpdateWorkerAttendanceStatus(new WorkerAttendanceUpdateDto { Id = 1, IsRendered = YesNo.Yes }, "worker");
			var attendance = await _context.Attendances.FindAsync(1);
			Assert.That(attendance!.IsRendered, Is.EqualTo(YesNo.Yes));
			Assert.That(attendance.IsPaid, Is.EqualTo(YesNo.No));
			Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.UpdateWorkerAttendanceStatus(new WorkerAttendanceUpdateDto { Id = 1, IsRendered = YesNo.No }, "other-worker"));
		}

		[Test]
		public async Task PayPalCaptureVerifiesProviderDataBeforeMarkingAppointmentPaid()
		{
			await using var connection = new SqliteConnection("DataSource=:memory:");
			await connection.OpenAsync();
			var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
			await using var context = new AppDbContext(options);
			await context.Database.EnsureCreatedAsync();
			context.Users.AddRange(TestUser("client", UserRoles.Client), TestUser("worker", UserRoles.Worker));
			context.Workers.Add(new Worker { Id = "worker", Address = "Address", Gender = "Other" });
			context.Attendances.Add(new Attendance { Id = 7, ClientId = "client", WorkerId = "worker", Date = DateTime.Today.AddDays(1), Time = TimeSpan.FromHours(10), Price = 25m, IsPaid = YesNo.No, IsRendered = YesNo.No });
			await context.SaveChangesAsync();

			var payPal = new FakePayPalService();
			var controller = new PaymentsController(context, payPal);
			SetUser(controller, "client", UserRoles.Client);
			var created = await controller.CreateOrder(new PaymentAttendanceIdsDto { AttendanceIds = new() { 7 } }, CancellationToken.None) as OkObjectResult;
			var order = (PayPalOrderDto)created!.Value!;
			var captured = await controller.CaptureOrder(order.OrderId, new PaymentAttendanceIdsDto { AttendanceIds = new() { 7 } }, CancellationToken.None);

			Assert.That(captured, Is.TypeOf<OkObjectResult>());
			Assert.That((await context.Attendances.FindAsync(7))!.IsPaid, Is.EqualTo(YesNo.Yes));
			Assert.That(context.PaymentTransactions.Single().PayerEmail, Is.EqualTo("payer@example.com"));
		}

		[Test]
		public async Task AppointmentCannotBeReservedByTwoPayPalOrders()
		{
			await using var connection = new SqliteConnection("DataSource=:memory:");
			await connection.OpenAsync();
			var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
			await using var context = new AppDbContext(options);
			await context.Database.EnsureCreatedAsync();
			context.Users.AddRange(TestUser("client", UserRoles.Client), TestUser("worker", UserRoles.Worker));
			context.Workers.Add(new Worker { Id = "worker", Address = "Address", Gender = "Other" });
			context.Attendances.Add(new Attendance { Id = 9, ClientId = "client", WorkerId = "worker", Date = DateTime.Today.AddDays(1), Time = TimeSpan.FromHours(12), Price = 40m, IsPaid = YesNo.No, IsRendered = YesNo.No });
			await context.SaveChangesAsync();

			var controller = new PaymentsController(context, new FakePayPalService());
			SetUser(controller, "client", UserRoles.Client);
			var request = new PaymentAttendanceIdsDto { AttendanceIds = new() { 9 } };
			var first = await controller.CreateOrder(request, CancellationToken.None);
			var second = await controller.CreateOrder(request, CancellationToken.None);

			Assert.That(first, Is.TypeOf<OkObjectResult>());
			Assert.That(second, Is.TypeOf<ConflictObjectResult>());
			Assert.That((await context.Attendances.FindAsync(9))!.PaymentTransactionId, Is.Not.Null);
		}

		[Test]
		public async Task InvalidPayPalAmountDoesNotMarkAppointmentPaid()
		{
			await using var connection = new SqliteConnection("DataSource=:memory:");
			await connection.OpenAsync();
			var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
			await using var context = new AppDbContext(options);
			await context.Database.EnsureCreatedAsync();
			context.Users.AddRange(TestUser("client", UserRoles.Client), TestUser("worker", UserRoles.Worker));
			context.Workers.Add(new Worker { Id = "worker", Address = "Address", Gender = "Other" });
			context.Attendances.Add(new Attendance { Id = 8, ClientId = "client", WorkerId = "worker", Date = DateTime.Today.AddDays(1), Time = TimeSpan.FromHours(11), Price = 30m, IsPaid = YesNo.No, IsRendered = YesNo.No });
			await context.SaveChangesAsync();

			var payPal = new FakePayPalService { CaptureAmountAdjustment = 1m };
			var controller = new PaymentsController(context, payPal);
			SetUser(controller, "client", UserRoles.Client);
			var created = await controller.CreateOrder(new PaymentAttendanceIdsDto { AttendanceIds = new() { 8 } }, CancellationToken.None) as OkObjectResult;
			var order = (PayPalOrderDto)created!.Value!;
			var captured = await controller.CaptureOrder(order.OrderId, new PaymentAttendanceIdsDto { AttendanceIds = new() { 8 } }, CancellationToken.None);

			Assert.That(captured, Is.TypeOf<ObjectResult>());
			Assert.That(((ObjectResult)captured).StatusCode, Is.EqualTo(StatusCodes.Status502BadGateway));
			Assert.That((await context.Attendances.FindAsync(8))!.IsPaid, Is.EqualTo(YesNo.No));
		}

		[Test]
		public async Task ChatSenderComesFromClaimsAndOnlyRecipientCanMarkRead()
		{
			_context.Users.AddRange(TestUser("sender", UserRoles.Client), TestUser("recipient", UserRoles.Worker));
			await _context.SaveChangesAsync();

			var chatData = new ChatDataService(_context);
			var controller = new ChatController(new UsersService(_context), chatData);
			SetUser(controller, "sender", UserRoles.Client);
			var saved = await controller.SaveMessage(new ChatMessageInputDto { ToId = "recipient", Content = "Hello" });
			var stored = await _context.Chats.SingleAsync();

			Assert.That(saved.Result, Is.TypeOf<OkObjectResult>());
			Assert.That(stored.FromId, Is.EqualTo("sender"));
			Assert.That(stored.ToId, Is.EqualTo("recipient"));
			Assert.That(await controller.UpdateMessage(new ChatReadDto { Id = stored.Id }), Is.TypeOf<ForbidResult>());

			SetUser(controller, "recipient", UserRoles.Worker);
			Assert.That(await controller.UpdateMessage(new ChatReadDto { Id = stored.Id }), Is.TypeOf<OkResult>());
			Assert.That((await _context.Chats.FindAsync(stored.Id))!.IsRead, Is.True);
		}

		[Test]
		public async Task WorkerCannotSubmitScheduleForAnotherWorker()
		{
			var controller = new WorkersController(new WorkersService(_context));
			SetUser(controller, "worker-1", UserRoles.Worker);

			var result = await controller.CreateWorkerSchedule(new List<Schedule>
			{
				new() { WorkerId = "worker-2", Day = DayOfWeek.Monday.ToString(), Start = TimeSpan.FromHours(9), End = TimeSpan.FromHours(17) }
			});

			Assert.That(result, Is.TypeOf<ForbidResult>());
		}

		[Test]
		public async Task AppointmentRejectsWorkerOutsideSelectedServiceGroup()
		{
			_context.Users.AddRange(TestUser("client", UserRoles.Client), TestUser("worker", UserRoles.Worker));
			_context.Workers.Add(new Worker { Id = "worker", Address = "Address", Gender = "Other" });
			_context.Groups.AddRange(
				new GroupOfServices { Id = 1, Name = "Hair" },
				new GroupOfServices { Id = 2, Name = "Nails" });
			_context.Services.Add(new Service { Id = 1, Name = "Cut", Price = 20m });
			_context.Services_Groups.Add(new ServiceGroup { ServiceId = 1, GroupId = 1 });
			_context.Workers_Groups.Add(new WorkerGroup { WorkerId = "worker", GroupId = 2 });
			await _context.SaveChangesAsync();

			var service = new AttendancesService(_context);
			var request = new NewAttendanceVM
			{
				GroupId = 1, ServiceId = 1, WorkerId = "worker", Date = DateTime.Today.AddDays(7), Time = TimeSpan.FromHours(10)
			};

			var exception = Assert.ThrowsAsync<InvalidOperationException>(() => service.AddNewAttendanceAsync(request, "client"));
			Assert.That(exception!.Message, Is.EqualTo("The selected worker does not provide this service group."));
		}

		[Test]
		public void ModelEnforcesWorkerSlotAndAccountUniqueness()
		{
			var attendanceIndex = _context.Model.FindEntityType(typeof(Attendance))!.GetIndexes()
				.Single(index => index.Properties.Select(property => property.Name).SequenceEqual(new[] { "WorkerId", "Date", "Time" }));
			var attendanceType = _context.Model.FindEntityType(typeof(Attendance))!;
			var attendanceTime = attendanceType.FindProperty(nameof(Attendance.Time));
			var paymentReservation = attendanceType.FindProperty(nameof(Attendance.PaymentTransactionId));
			var userIndexes = _context.Model.FindEntityType(typeof(User))!.GetIndexes().Where(index => index.IsUnique).ToList();
			Assert.That(attendanceIndex.IsUnique, Is.True);
			Assert.That(attendanceTime!.IsNullable, Is.False);
			Assert.That(paymentReservation!.IsConcurrencyToken, Is.True);
			Assert.That(userIndexes.Any(index => index.Properties.Single().Name == "UserName"), Is.True);
			Assert.That(userIndexes.Any(index => index.Properties.Single().Name == "Email"), Is.True);
		}

		[Test]
		public void ChatControllerAndHubRequireAuthentication()
		{
			Assert.That(typeof(ChatController).GetCustomAttribute<AuthorizeAttribute>(), Is.Not.Null);
			Assert.That(typeof(ChatHub).GetCustomAttribute<AuthorizeAttribute>(), Is.Not.Null);
		}

		[Test]
		public void AppointmentAdministrationEndpointsAreProtected()
		{
			var method = typeof(AttendancesController).GetMethod(nameof(AttendancesController.GetAllAttendances));
			Assert.That(method!.GetCustomAttribute<AuthorizeAttribute>()?.Roles, Is.EqualTo(UserRoles.Admin));
		}

		[Test]
		public void WorkersCannotCancelAppointments()
		{
			var method = typeof(AttendancesController).GetMethod(nameof(AttendancesController.DeleteAttendance));
			var roles = method!.GetCustomAttribute<AuthorizeAttribute>()?.Roles?.Split(',');
			Assert.That(roles, Does.Contain(UserRoles.Client));
			Assert.That(roles, Does.Contain(UserRoles.Admin));
			Assert.That(roles, Does.Not.Contain(UserRoles.Worker));
		}

		private AuthController NewAuthController(string refreshCookie = null)
		{
			var controller = new AuthController(_context, _configuration, _emailService);
			var httpContext = new DefaultHttpContext();
			if (refreshCookie != null) httpContext.Request.Headers.Cookie = $"hhSalon.refresh_token={refreshCookie}";
			controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
			return controller;
		}

		private static IConfiguration TestConfiguration() => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
		{
			["Jwt:Key"] = "unit-test-signing-key-that-is-longer-than-32-bytes",
			["Jwt:Issuer"] = "hhSalonAPI",
			["Jwt:Audience"] = "hhSalonClient",
			["Jwt:AccessTokenMinutes"] = "30",
			["Jwt:RefreshTokenDays"] = "5"
		}).Build();

		private static RegisterUserDto ValidRegistration(string userName) => new()
		{
			FirstName = "Test", LastName = "User", Email = $"{userName}@example.com", UserName = userName, Password = "Strong1!"
		};

		private static User TestUser(string id, string role) => new()
		{
			Id = id, FirstName = "Test", LastName = "User", Email = $"{id}@example.com", UserName = id, Password = "hash", Role = role, Token = string.Empty
		};

		private static void AssertAdminOnly(string methodName)
		{
			var authorize = typeof(AuthController).GetMethod(methodName)!.GetCustomAttribute<AuthorizeAttribute>();
			Assert.That(authorize?.Roles, Is.EqualTo(UserRoles.Admin));
		}

		private static void SetUser(ControllerBase controller, string userId, string role)
		{
			var identity = new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, userId), new Claim(ClaimTypes.Role, role) }, "TestAuthentication");
			controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) } };
		}

		private static string ExtractCookie(string[] setCookieHeaders, string name)
		{
			var header = setCookieHeaders.Single(value => value.StartsWith(name + "=", StringComparison.Ordinal));
			return Uri.UnescapeDataString(header[(name.Length + 1)..].Split(';')[0]);
		}

		private sealed class CapturingEmailService : IEmailService
		{
			public EmailModel LastEmail { get; private set; }
			public void SendEmail(EmailModel emailModel) => LastEmail = emailModel;
		}

		private sealed class FakePayPalService : IPayPalService
		{
			private decimal _amount;
			private string _clientId = string.Empty;
			private string _transactionId = string.Empty;
			public decimal CaptureAmountAdjustment { get; set; }
			public bool IsConfigured => true;
			public string ClientId => "sandbox-client";
			public string Currency => "USD";

			public Task<PayPalProviderOrder> CreateOrder(decimal amount, string clientId, string localTransactionId, CancellationToken cancellationToken)
			{
				_amount = amount; _clientId = clientId; _transactionId = localTransactionId;
				return Task.FromResult(Order("ORDER-1", "CREATED", amount));
			}

			public Task<PayPalProviderOrder> CaptureOrder(string orderId, CancellationToken cancellationToken)
				=> Task.FromResult(Order(orderId, "COMPLETED", _amount + CaptureAmountAdjustment));

			public Task<PayPalProviderOrder> GetOrder(string orderId, CancellationToken cancellationToken)
				=> Task.FromResult(Order(orderId, "COMPLETED", _amount + CaptureAmountAdjustment));

			private PayPalProviderOrder Order(string id, string status, decimal amount) => new()
			{
				Id = id, Status = status, Amount = amount, Currency = Currency, CustomId = _clientId, InvoiceId = _transactionId,
				PayerId = status == "COMPLETED" ? "PAYER-1" : null, PayerEmail = status == "COMPLETED" ? "payer@example.com" : null
			};
		}
	}
}
