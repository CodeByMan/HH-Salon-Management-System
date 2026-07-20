using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace hhSalon.Domain.Entities
{
	public class PaymentTransaction
	{
		[Key]
		[Column("id")]
		public string Id { get; set; }

		[Required]
		[Column("provider_order_id")]
		public string ProviderOrderId { get; set; }

		[Required]
		[Column("client_id")]
		public string ClientId { get; set; }
		public User Client { get; set; }

		[Required]
		[Column("attendance_ids")]
		public string AttendanceIds { get; set; }

		[Column("amount", TypeName = "decimal(10,2)")]
		public decimal Amount { get; set; }

		[Required, StringLength(3)]
		[Column("currency")]
		public string Currency { get; set; }

		[Required, StringLength(32)]
		[Column("status")]
		public string Status { get; set; }

		[Column("payer_id")]
		public string PayerId { get; set; }

		[Column("payer_email")]
		public string PayerEmail { get; set; }

		[Column("created_at_utc")]
		public DateTime CreatedAtUtc { get; set; }

		[Column("expires_at_utc")]
		public DateTime ExpiresAtUtc { get; set; }

		[Column("captured_at_utc")]
		public DateTime? CapturedAtUtc { get; set; }

		public List<Attendance> Attendances { get; set; }
	}
}
