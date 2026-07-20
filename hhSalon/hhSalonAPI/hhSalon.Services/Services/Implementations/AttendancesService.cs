using hhSalon.Domain.Abstract;
using hhSalon.Domain.Entities;
using hhSalon.Domain.Entities.Static;
using hhSalon.Services.Models.Dto;
using hhSalon.Services.Services.Interfaces;
using hhSalon.Services.ViewModels;
using hhSalonAPI.Domain.Concrete;
using Microsoft.EntityFrameworkCore;

namespace hhSalon.Services.Services.Implementations
{
	public class AttendancesService : EntityBaseRepository<Attendance>, IAttendancesService
	{
		private readonly AppDbContext _context;

		public AttendancesService(AppDbContext context) : base(context)
		{
			_context = context;
		}

		public async Task AddNewAttendanceAsync(NewAttendanceVM newAttendance, string clientId)
		{
			var date = newAttendance.Date.Date;
			if (date < DateTime.Today || (date == DateTime.Today && newAttendance.Time <= DateTime.Now.TimeOfDay))
				throw new InvalidOperationException("Appointments must be scheduled in the future.");

			var service = await _context.Services
				.Include(s => s.ServiceGroup)
				.FirstOrDefaultAsync(s => s.Id == newAttendance.ServiceId);
			if (service == null || service.ServiceGroup == null || service.ServiceGroup.GroupId != newAttendance.GroupId)
				throw new InvalidOperationException("The selected service does not belong to the selected group.");

			var workerSupportsGroup = await _context.Workers_Groups
				.AnyAsync(wg => wg.WorkerId == newAttendance.WorkerId && wg.GroupId == newAttendance.GroupId);
			if (!workerSupportsGroup)
				throw new InvalidOperationException("The selected worker does not provide this service group.");

			var schedule = await _context.Schedules.FirstOrDefaultAsync(s =>
				s.WorkerId == newAttendance.WorkerId && s.Day == date.DayOfWeek.ToString());
			if (schedule == null || newAttendance.Time < schedule.Start || newAttendance.Time >= schedule.End)
				throw new InvalidOperationException("The selected time is outside the worker's schedule.");

			var duplicate = await _context.Attendances.AnyAsync(a =>
				a.ClientId == clientId && a.ServiceId == newAttendance.ServiceId && a.Date == date);
			if (duplicate)
				throw new InvalidOperationException("You already have an appointment with the same service on this date.");

			var conflict = await _context.Attendances.AnyAsync(a =>
				a.WorkerId == newAttendance.WorkerId && a.Date == date && a.Time == newAttendance.Time);
			if (conflict)
				throw new InvalidOperationException("The selected appointment time is no longer available.");

			var attendance = new Attendance
			{
				GroupId = newAttendance.GroupId,
				ServiceId = newAttendance.ServiceId,
				WorkerId = newAttendance.WorkerId,
				Date = date,
				Price = service.Price,
				IsRendered = YesNo.No,
				IsPaid = YesNo.No,
				ClientId = clientId,
				Time = newAttendance.Time
			};

			await _context.Attendances.AddAsync(attendance);
			try
			{
				await _context.SaveChangesAsync();
			}
			catch (DbUpdateException ex) when (IsWorkerSlotConflict(ex))
			{
				throw new InvalidOperationException("The selected appointment time was booked by another request. Choose another time.", ex);
			}
			catch (DbUpdateException ex) when (IsClientDuplicate(ex))
			{
				throw new InvalidOperationException("You already have an appointment with the same service on this date.", ex);
			}
		}

		public async Task<IEnumerable<AttendanceDto>> MyNotRenderedIsPaidAttendances(string userId)
			=> await GetAttendances(a => a.IsRendered == YesNo.No && a.IsPaid == YesNo.Yes && a.ClientId == userId);

		public async Task<IEnumerable<AttendanceDto>> MyNotRenderedNotPaidAttendances(string userId)
			=> await GetAttendances(a => a.IsRendered == YesNo.No && a.IsPaid == YesNo.No && a.ClientId == userId);

		public async Task<IEnumerable<AttendanceDto>> MyIsRenderedAttendances(string userId)
			=> await GetAttendances(a => a.IsRendered == YesNo.Yes && a.ClientId == userId);

		public async Task<IEnumerable<AttendanceDto>> WorkerNotRenderedIsPaidAttendances(string workerId)
			=> await GetAttendances(a => a.WorkerId == workerId && a.IsRendered == YesNo.No && a.IsPaid == YesNo.Yes);

		public async Task<IEnumerable<AttendanceDto>> WorkerNotRenderedNotPaidAttendances(string workerId)
			=> await GetAttendances(a => a.WorkerId == workerId && a.IsRendered == YesNo.No && a.IsPaid == YesNo.No);

		public async Task<IEnumerable<AttendanceDto>> WorkerNotRenderedAttendances(string workerId)
			=> await GetAttendances(a => a.WorkerId == workerId && a.IsRendered == YesNo.No);

		public async Task<IEnumerable<AttendanceDto>> WorkerIsRenderedAttendances(string workerId)
			=> await GetAttendances(a => a.WorkerId == workerId && a.IsRendered == YesNo.Yes);

		public async Task<Attendance> GetAttendanceByIdAsync(int id)
			=> await _context.Attendances.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id);

		public async Task<IEnumerable<TimeSpan>> GetFreeTimeSlots(string workerId, DateTime date)
		{
			date = date.Date;
			if (date < DateTime.Today)
				return Array.Empty<TimeSpan>();

			var schedule = await _context.Schedules.AsNoTracking().FirstOrDefaultAsync(s =>
				s.WorkerId == workerId && s.Day == date.DayOfWeek.ToString());
			if (schedule == null || schedule.Start >= schedule.End)
				return Array.Empty<TimeSpan>();

			var slotsTaken = await _context.Attendances.AsNoTracking()
				.Where(a => a.WorkerId == workerId && a.Date == date)
				.Select(a => a.Time)
				.ToListAsync();

			var slots = new List<TimeSpan>();
			for (var slot = schedule.Start; slot < schedule.End; slot = slot.Add(TimeSpan.FromHours(1)))
			{
				if (date == DateTime.Today && slot <= DateTime.Now.TimeOfDay)
					continue;
				if (!slotsTaken.Contains(slot))
					slots.Add(slot);
			}
			return slots;
		}

		public async Task UpdateAttendances(List<AttendanceUpdateDto> attendances)
		{
			foreach (var attendanceDto in attendances)
				await ApplyAdministrativeUpdate(attendanceDto);
			await SaveAdministrativeUpdates();
		}

		public async Task UpdateAttendance(AttendanceUpdateDto attendance)
		{
			await ApplyAdministrativeUpdate(attendance);
			await SaveAdministrativeUpdates();
		}

		public async Task UpdateWorkerAttendanceStatus(WorkerAttendanceUpdateDto attendanceDto, string workerId)
		{
			var attendance = await _context.Attendances.FirstOrDefaultAsync(a => a.Id == attendanceDto.Id);
			if (attendance == null)
				throw new KeyNotFoundException("Appointment was not found.");
			if (attendance.WorkerId != workerId)
				throw new UnauthorizedAccessException("The appointment is not assigned to the authenticated worker.");
			if (attendanceDto.IsRendered != YesNo.Yes && attendanceDto.IsRendered != YesNo.No)
				throw new InvalidOperationException("Rendered status is invalid.");

			attendance.IsRendered = attendanceDto.IsRendered;
			await _context.SaveChangesAsync();
		}

		public async Task<IEnumerable<AttendanceDto>> GetAllAttendances()
			=> await GetAttendances(a => true);

		public async Task<IEnumerable<AttendanceDto>> GetAttendancesBySearch(string content)
		{
			var attendances = await GetAttendances(a => true);
			if (string.IsNullOrWhiteSpace(content))
				return attendances;

			return attendances.Where(a =>
				Contains(a.Client?.UserName, content) || Contains(a.Client?.FirstName, content) || Contains(a.Client?.LastName, content) ||
				Contains(a.Worker?.User?.UserName, content) || Contains(a.Worker?.User?.FirstName, content) || Contains(a.Worker?.User?.LastName, content) ||
				Contains(a.Group?.Name, content) || Contains(a.Service?.Name, content) || Contains(a.IsPaid, content) || Contains(a.IsRendered, content) ||
				Contains(a.Date.ToString(), content) || Contains(a.Time?.ToString(), content) || Contains(a.Price.ToString(), content));
		}

		private async Task ApplyAdministrativeUpdate(AttendanceUpdateDto attendanceDto)
		{
			var attendance = await _context.Attendances.FirstOrDefaultAsync(a => a.Id == attendanceDto.Id);
			if (attendance == null)
				throw new KeyNotFoundException("Appointment was not found.");

			var proposedDate = attendanceDto.Date == default ? attendance.Date.Date : attendanceDto.Date.Date;
			var proposedTime = attendanceDto.Time ?? attendance.Time;
			var proposedGroupId = attendanceDto.GroupId ?? attendance.GroupId;
			var proposedServiceId = attendanceDto.ServiceId ?? attendance.ServiceId;
			var schedulingChanged = proposedDate != attendance.Date.Date || proposedTime != attendance.Time ||
				proposedGroupId != attendance.GroupId || proposedServiceId != attendance.ServiceId;

			if (schedulingChanged)
			{
				if (!proposedGroupId.HasValue || !proposedServiceId.HasValue)
					throw new InvalidOperationException("Appointment group and service are required.");
				if (proposedDate < DateTime.Today || (proposedDate == DateTime.Today && proposedTime <= DateTime.Now.TimeOfDay))
					throw new InvalidOperationException("Appointments must be scheduled in the future.");

				var service = await _context.Services.Include(s => s.ServiceGroup)
					.FirstOrDefaultAsync(s => s.Id == proposedServiceId.Value);
				if (service == null)
					throw new KeyNotFoundException("Service was not found.");
				if (service.ServiceGroup == null || service.ServiceGroup.GroupId != proposedGroupId.Value)
					throw new InvalidOperationException("The selected service does not belong to the selected group.");

				var workerSupportsGroup = await _context.Workers_Groups.AnyAsync(wg =>
					wg.WorkerId == attendance.WorkerId && wg.GroupId == proposedGroupId.Value);
				if (!workerSupportsGroup)
					throw new InvalidOperationException("The assigned worker does not provide this service group.");

				var schedule = await _context.Schedules.FirstOrDefaultAsync(s =>
					s.WorkerId == attendance.WorkerId && s.Day == proposedDate.DayOfWeek.ToString());
				if (schedule == null || proposedTime < schedule.Start || proposedTime >= schedule.End)
					throw new InvalidOperationException("The selected time is outside the worker's schedule.");

				var conflict = await _context.Attendances.AnyAsync(a => a.Id != attendance.Id &&
					a.WorkerId == attendance.WorkerId && a.Date == proposedDate && a.Time == proposedTime);
				if (conflict)
					throw new InvalidOperationException("The selected appointment time is no longer available.");

				var duplicate = await _context.Attendances.AnyAsync(a => a.Id != attendance.Id &&
					a.ClientId == attendance.ClientId && a.ServiceId == proposedServiceId.Value && a.Date == proposedDate);
				if (duplicate)
					throw new InvalidOperationException("The client already has this service booked on the selected date.");

				attendance.Date = proposedDate;
				attendance.Time = proposedTime;
				attendance.GroupId = proposedGroupId;
				attendance.ServiceId = proposedServiceId;
				attendance.Price = service.Price;
			}

			if (!string.IsNullOrWhiteSpace(attendanceDto.IsRendered))
			{
				if (attendanceDto.IsRendered != YesNo.Yes && attendanceDto.IsRendered != YesNo.No)
					throw new InvalidOperationException("Rendered status is invalid.");
				attendance.IsRendered = attendanceDto.IsRendered;
			}

			if (!string.IsNullOrWhiteSpace(attendanceDto.IsPaid))
			{
				if (attendanceDto.IsPaid != YesNo.Yes && attendanceDto.IsPaid != YesNo.No)
					throw new InvalidOperationException("Payment status is invalid.");
				attendance.IsPaid = attendanceDto.IsPaid;
				if (attendanceDto.IsPaid == YesNo.No)
					attendance.PaymentTransactionId = null;
			}
		}

		private async Task SaveAdministrativeUpdates()
		{
			try
			{
				await _context.SaveChangesAsync();
			}
			catch (DbUpdateException ex) when (IsWorkerSlotConflict(ex))
			{
				throw new InvalidOperationException("The selected appointment time was booked by another request. Choose another time.", ex);
			}
			catch (DbUpdateException ex) when (IsClientDuplicate(ex))
			{
				throw new InvalidOperationException("The client already has this service booked on the selected date.", ex);
			}
		}

		private async Task<List<AttendanceDto>> GetAttendances(System.Linq.Expressions.Expression<Func<Attendance, bool>> predicate)
		{
			return await _context.Attendances.AsNoTracking().Where(predicate)
				.OrderBy(a => a.Date).ThenBy(a => a.Time)
				.Select(a => new AttendanceDto
				{
					Id = a.Id,
					ClientId = a.ClientId,
					Client = new UserSummaryDto { Id = a.Client.Id, FirstName = a.Client.FirstName, LastName = a.Client.LastName, UserName = a.Client.UserName },
					GroupId = a.GroupId,
					Group = a.Group == null ? null : new AttendanceGroupDto { Id = a.Group.Id, Name = a.Group.Name },
					ServiceId = a.ServiceId,
					Service = a.Service == null ? null : new AttendanceServiceDto { Id = a.Service.Id, Name = a.Service.Name, Price = a.Service.Price },
					WorkerId = a.WorkerId,
					Worker = a.Worker == null ? null : new AttendanceWorkerDto
					{
						Id = a.Worker.Id,
						User = new UserSummaryDto { Id = a.Worker.User.Id, FirstName = a.Worker.User.FirstName, LastName = a.Worker.User.LastName, UserName = a.Worker.User.UserName }
					},
					Date = a.Date,
					Time = a.Time,
					Price = a.Price,
					IsRendered = a.IsRendered,
					IsPaid = a.IsPaid
				})
				.ToListAsync();
		}

		private static bool IsWorkerSlotConflict(DbUpdateException exception)
			=> exception.InnerException?.Message.Contains("UX_Attendances_Worker_Date_Time", StringComparison.OrdinalIgnoreCase) == true;

		private static bool IsClientDuplicate(DbUpdateException exception)
			=> exception.InnerException?.Message.Contains("UX_Attendances_Client_Date_Service", StringComparison.OrdinalIgnoreCase) == true;

		private static bool Contains(string value, string content)
			=> value?.Contains(content, StringComparison.CurrentCultureIgnoreCase) == true;
	}
}
