using System.ComponentModel.DataAnnotations;

namespace hhSalon.Services.Models.Dto
{
	public class UserSummaryDto
	{
		public string Id { get; set; }
		public string FirstName { get; set; }
		public string LastName { get; set; }
		public string UserName { get; set; }
	}

	public class UserDto : UserSummaryDto
	{
		public string Email { get; set; }
	}

	public class UserUpdateDto
	{
		[Required]
		public string Id { get; set; }

		[Required]
		public string FirstName { get; set; }

		[Required]
		public string LastName { get; set; }

		[Required, EmailAddress]
		public string Email { get; set; }
	}
}
