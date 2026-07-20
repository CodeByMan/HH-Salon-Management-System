using hhSalon.Domain.Entities;

namespace hhSalon.Services.Models.Dto
{
	public class WorkerGroupDto
	{
		public int GroupId { get; set; }
		public WorkerGroupNameDto Group { get; set; }
	}

	public class WorkerGroupNameDto
	{
		public int Id { get; set; }
		public string Name { get; set; }
	}

	public class WorkerListDto
	{
		public string Id { get; set; }
		public UserSummaryDto User { get; set; }
		public List<WorkerGroupDto> Workers_Groups { get; set; }
		public List<Schedule> Schedules { get; set; }
	}
}
