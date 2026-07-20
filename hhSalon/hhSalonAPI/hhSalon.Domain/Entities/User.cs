using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace hhSalon.Domain.Entities
{
	public class User
	{
		[Key]
		[Column("id")]
		public string Id { get; set; }

		[Column("first_name"), Required]
		public string FirstName { get; set; }

		[Column("last_name"), Required]
		public string LastName { get; set; }

		[Column("email"), Required]
		public string Email { get; set; }

		[Column("user_name"), Required]
		public string UserName { get; set; }

		[Column("password"), Required]
		public string Password { get; set; }

		[Column("token")]
		public string Token { get; set; }

		[Column("role"), Required]
		public string Role { get; set; }

		// The database column name is retained for migration compatibility, but only a SHA-256 hash is stored.
		[Column("refresh_token")]
		public string RefreshTokenHash { get; set; }

		[Column("refresh_token_exp_time")]
		public DateTime RefreshTokenExpiryTime { get; set; }

		// The database column name is retained for migration compatibility, but only a SHA-256 hash is stored.
		[Column("reset_password_token")]
		public string ResetPasswordTokenHash { get; set; }

		[Column("reset_password_expiry")]
		public DateTime ResetPasswordExpiry { get; set; }

		public List<Attendance> Attendances { get; set; }
		public List<PaymentTransaction> PaymentTransactions { get; set; }
	}
}
