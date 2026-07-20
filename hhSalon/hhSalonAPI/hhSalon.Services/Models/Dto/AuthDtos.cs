using System.ComponentModel.DataAnnotations;

namespace hhSalon.Services.Models.Dto
{
	public class LoginDto
	{
		[Required]
		public string UserName { get; set; }

		[Required]
		public string Password { get; set; }
	}

	public class RegisterUserDto
	{
		[Required]
		public string FirstName { get; set; }

		[Required]
		public string LastName { get; set; }

		[Required, EmailAddress]
		public string Email { get; set; }

		[Required]
		public string UserName { get; set; }

		[Required]
		public string Password { get; set; }
	}

	public class RegisterWorkerDto : RegisterUserDto
	{
		[Required, StringLength(45)]
		public string Address { get; set; }

		[Required, StringLength(6)]
		public string Gender { get; set; }

		[Required]
		public List<int> GroupsIds { get; set; } = new List<int>();
	}
}
