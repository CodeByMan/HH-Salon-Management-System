using hhSalon.Domain.Entities;
using hhSalon.Services.Models.Dto;
using hhSalon.Services.ViewModels;

namespace hhSalon.Services.Services.Interfaces
{
	public interface IWorkersService
	{
		Task<IEnumerable<WorkerListDto>> GetWorkersAsync();
		Task<IEnumerable<WorkerVM>> GetWorkersByGroupId(int groupId);
		Task CreateWorkerSchedule(List<Schedule> schedules);
		Task<WorkerVM> GetWorkerVMByIdAsync(string workerId);
		Task<Worker> GetWorkerByIdAsync(string workerId);
		Task UpdateWorkerAsync(WorkerVM workerVM);
		Task DeleteAsync(string workerId);
	}
}
