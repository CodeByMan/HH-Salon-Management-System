using System.ComponentModel.DataAnnotations;

namespace hhSalon.Services.Models.Dto
{
	public class PaymentAttendanceIdsDto
	{
		[Required, MinLength(1), MaxLength(50)]
		public List<int> AttendanceIds { get; set; } = new();
	}

	public class PayPalConfigurationDto
	{
		public bool Enabled { get; set; }
		public string ClientId { get; set; }
		public string Currency { get; set; }
	}

	public class PayPalOrderDto
	{
		public string OrderId { get; set; }
		public string Status { get; set; }
		public decimal Amount { get; set; }
		public string Currency { get; set; }
	}

	public class PayPalCaptureDto : PayPalOrderDto
	{
		public string PayerId { get; set; }
		public string PayerEmail { get; set; }
	}
}
