using System.ComponentModel.DataAnnotations;

namespace hhSalon.Services.ViewModels
{
	public class NewAttendanceVM
	{
		[Required]
		public int GroupId { get; set; }

		[Required]
		public int ServiceId { get; set; }

		[Required]
		public string WorkerId { get; set; }

		[Required]
		public DateTime Date { get; set; }

		[Required]
		public TimeSpan Time { get; set; }
	}
}
