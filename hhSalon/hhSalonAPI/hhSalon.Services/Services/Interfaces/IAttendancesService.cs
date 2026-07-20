using hhSalon.Domain.Abstract;
using hhSalon.Domain.Entities;
using hhSalon.Services.Models.Dto;
using hhSalon.Services.ViewModels;

namespace hhSalon.Services.Services.Interfaces
{
	public interface IAttendancesService : IEntityBaseRepository<Attendance>
	{
		Task<IEnumerable<AttendanceDto>> GetAllAttendances();
		Task<IEnumerable<AttendanceDto>> GetAttendancesBySearch(string content);
		Task<Attendance> GetAttendanceByIdAsync(int id);
		Task AddNewAttendanceAsync(NewAttendanceVM newAttendance, string clientId);
		Task<IEnumerable<AttendanceDto>> MyIsRenderedAttendances(string userId);
		Task<IEnumerable<AttendanceDto>> MyNotRenderedIsPaidAttendances(string userId);
		Task<IEnumerable<AttendanceDto>> MyNotRenderedNotPaidAttendances(string userId);
		Task<IEnumerable<AttendanceDto>> WorkerNotRenderedIsPaidAttendances(string workerId);
		Task<IEnumerable<AttendanceDto>> WorkerNotRenderedNotPaidAttendances(string workerId);
		Task<IEnumerable<AttendanceDto>> WorkerNotRenderedAttendances(string workerId);
		Task<IEnumerable<AttendanceDto>> WorkerIsRenderedAttendances(string workerId);
		Task<IEnumerable<TimeSpan>> GetFreeTimeSlots(string workerId, DateTime date);
		Task UpdateAttendances(List<AttendanceUpdateDto> attendances);
		Task UpdateAttendance(AttendanceUpdateDto attendance);
		Task UpdateWorkerAttendanceStatus(WorkerAttendanceUpdateDto attendance, string workerId);
	}
}
