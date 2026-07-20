using hhSalon.Services.Models.Dto;

namespace hhSalonAPI.Payments
{
	public interface IPayPalService
	{
		bool IsConfigured { get; }
		string ClientId { get; }
		string Currency { get; }
		Task<PayPalProviderOrder> CreateOrder(decimal amount, string clientId, string localTransactionId, CancellationToken cancellationToken);
		Task<PayPalProviderOrder> CaptureOrder(string orderId, CancellationToken cancellationToken);
		Task<PayPalProviderOrder> GetOrder(string orderId, CancellationToken cancellationToken);
	}

	public class PayPalProviderOrder
	{
		public string Id { get; set; }
		public string Status { get; set; }
		public decimal Amount { get; set; }
		public string Currency { get; set; }
		public string CustomId { get; set; }
		public string InvoiceId { get; set; }
		public string PayerId { get; set; }
		public string PayerEmail { get; set; }
	}
}
