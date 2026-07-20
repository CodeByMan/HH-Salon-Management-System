using hhSalon.Domain.Entities;
using System.ComponentModel.DataAnnotations;

namespace hhSalon.Services.ViewModels
{
	public class WorkerVM
	{
		public string Id { get; set; }
		public string FirstName { get; set; }
		public string LastName { get; set; }
		public string Email { get; set; }
		public string UserName { get; set; }

		[StringLength(45)]
		public string Address { get; set; }

		[StringLength(6)]
		public string Gender { get; set; }

		public List<int> GroupsIds { get; set; } = new List<int>();
		public List<Schedule> Schedules { get; set; } = new List<Schedule>();
	}
}
