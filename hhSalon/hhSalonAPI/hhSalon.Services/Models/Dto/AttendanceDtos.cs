using System.ComponentModel.DataAnnotations;

namespace hhSalon.Services.Models.Dto
{
	public class AttendanceUpdateDto
	{
		[Required]
		public int Id { get; set; }
		public int? GroupId { get; set; }
		public int? ServiceId { get; set; }
		public DateTime Date { get; set; }
		public TimeSpan? Time { get; set; }
		public string IsRendered { get; set; }
		public string IsPaid { get; set; }
	}

	public class WorkerAttendanceUpdateDto
	{
		[Required]
		public int Id { get; set; }

		[Required]
		public string IsRendered { get; set; }
	}

	public class AttendanceGroupDto
	{
		public int Id { get; set; }
		public string Name { get; set; }
	}

	public class AttendanceServiceDto
	{
		public int Id { get; set; }
		public string Name { get; set; }
		public decimal Price { get; set; }
	}

	public class AttendanceWorkerDto
	{
		public string Id { get; set; }
		public UserSummaryDto User { get; set; }
	}

	public class AttendanceDto
	{
		public int Id { get; set; }
		public string ClientId { get; set; }
		public UserSummaryDto Client { get; set; }
		public int? GroupId { get; set; }
		public AttendanceGroupDto Group { get; set; }
		public int? ServiceId { get; set; }
		public AttendanceServiceDto Service { get; set; }
		public string WorkerId { get; set; }
		public AttendanceWorkerDto Worker { get; set; }
		public DateTime Date { get; set; }
		public TimeSpan? Time { get; set; }
		public decimal Price { get; set; }
		public string IsRendered { get; set; }
		public string IsPaid { get; set; }
	}
}
