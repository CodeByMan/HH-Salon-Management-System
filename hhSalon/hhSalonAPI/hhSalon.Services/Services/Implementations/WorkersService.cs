using hhSalon.Domain.Entities;
using hhSalon.Domain.Entities.Enums;
using hhSalon.Domain.Entities.Static;
using hhSalon.Services.Models.Dto;
using hhSalon.Services.Services.Interfaces;
using hhSalon.Services.ViewModels;
using hhSalonAPI.Domain.Concrete;
using Microsoft.EntityFrameworkCore;

namespace hhSalon.Services.Services.Implementations
{
	public class WorkersService : IWorkersService
	{
		private readonly AppDbContext _context;
		public WorkersService(AppDbContext context)
		{
			_context = context;
		}

		public async Task<IEnumerable<WorkerVM>> GetWorkersByGroupId(int groupId)
		{
			return await _context.Workers_Groups.AsNoTracking()
				.Where(wg => wg.GroupId == groupId)
				.Select(wg => new WorkerVM
				{
					Id = wg.WorkerId,
					FirstName = wg.Worker.User.FirstName,
					LastName = wg.Worker.User.LastName,
					UserName = wg.Worker.User.UserName
				})
				.ToListAsync();
		}

		public async Task<IEnumerable<WorkerListDto>> GetWorkersAsync()
		{
			return await _context.Workers.AsNoTracking()
				.Select(w => new WorkerListDto
				{
					Id = w.Id,
					User = new UserSummaryDto
					{
						Id = w.User.Id,
						FirstName = w.User.FirstName,
						LastName = w.User.LastName,
						UserName = w.User.UserName
					},
					Workers_Groups = w.Workers_Groups.Select(wg => new WorkerGroupDto
					{
						GroupId = wg.GroupId,
						Group = new WorkerGroupNameDto { Id = wg.Group.Id, Name = wg.Group.Name }
					}).ToList(),
					Schedules = w.Schedules.Select(s => new Schedule
					{
						WorkerId = s.WorkerId,
						Day = s.Day,
						Start = s.Start,
						End = s.End
					}).ToList()
				})
				.ToListAsync();
		}

		public async Task<Worker> GetWorkerByIdAsync(string workerId)
		{
			return await _context.Workers.AsNoTracking().Include(w => w.User)
				.FirstOrDefaultAsync(w => w.Id == workerId);
		}

		public async Task<WorkerVM> GetWorkerVMByIdAsync(string workerId)
		{
			var worker = await _context.Workers.AsNoTracking()
				.Include(w => w.User)
				.Include(w => w.Schedules)
				.Include(w => w.Workers_Groups)
				.FirstOrDefaultAsync(w => w.Id == workerId);

			if (worker == null)
				throw new KeyNotFoundException("Worker was not found.");

			var workerVM = new WorkerVM
			{
				Id = worker.Id,
				Address = worker.Address,
				Email = worker.User.Email,
				FirstName = worker.User.FirstName,
				LastName = worker.User.LastName,
				GroupsIds = worker.Workers_Groups.Select(wg => wg.GroupId).ToList(),
				Gender = worker.Gender,
				UserName = worker.User.UserName
			};

			foreach (var day in Enum.GetValues(typeof(Days)).Cast<Days>())
			{
				var schedule = worker.Schedules.FirstOrDefault(s => s.Day == day.ToString());
				workerVM.Schedules.Add(schedule == null
					? new Schedule { Day = day.ToString(), WorkerId = worker.Id, End = TimeSpan.Zero, Start = TimeSpan.Zero }
					: new Schedule { Day = schedule.Day, WorkerId = worker.Id, Start = schedule.Start, End = schedule.End });
			}

			return workerVM;
		}

		public async Task CreateWorkerSchedule(List<Schedule> schedules)
		{
			if (schedules == null || schedules.Count == 0)
				throw new ArgumentException("At least one schedule entry is required.");

			var workerId = schedules[0].WorkerId;
			ValidateSchedules(schedules, workerId);
			if (!await _context.Workers.AnyAsync(w => w.Id == workerId))
				throw new KeyNotFoundException("Worker was not found.");

			var currentSchedules = await _context.Schedules.Where(s => s.WorkerId == workerId).ToListAsync();
			_context.Schedules.RemoveRange(currentSchedules);

			var validSchedules = schedules
				.Where(s => s.Start != TimeSpan.Zero && s.End != TimeSpan.Zero)
				.Select(s => new Schedule { WorkerId = workerId, Day = s.Day, Start = s.Start, End = s.End })
				.ToList();
			await _context.Schedules.AddRangeAsync(validSchedules);
			await _context.SaveChangesAsync();
		}

		public async Task UpdateWorkerAsync(WorkerVM workerVM)
		{
			if (workerVM == null || string.IsNullOrWhiteSpace(workerVM.Id))
				throw new ArgumentException("Worker ID is required.");

			ValidateSchedules(workerVM.Schedules ?? new List<Schedule>(), workerVM.Id);
			var worker = await _context.Workers.Include(w => w.User).FirstOrDefaultAsync(w => w.Id == workerVM.Id);
			if (worker == null)
				throw new KeyNotFoundException("Worker was not found.");

			if (await _context.Users.AnyAsync(u => u.Email == workerVM.Email && u.Id != workerVM.Id))
				throw new InvalidOperationException("This email is taken.");

			var groupIds = (workerVM.GroupsIds ?? new List<int>()).Distinct().ToList();
			var validGroupCount = await _context.Groups.CountAsync(g => groupIds.Contains(g.Id));
			if (validGroupCount != groupIds.Count)
				throw new InvalidOperationException("One or more service groups are invalid.");

			worker.Address = workerVM.Address;
			worker.Gender = workerVM.Gender;
			worker.User.FirstName = workerVM.FirstName;
			worker.User.LastName = workerVM.LastName;
			worker.User.Email = workerVM.Email;

			var currentGroups = await _context.Workers_Groups.Where(wg => wg.WorkerId == workerVM.Id).ToListAsync();
			_context.Workers_Groups.RemoveRange(currentGroups);
			await _context.Workers_Groups.AddRangeAsync(groupIds.Select(groupId => new WorkerGroup
			{
				WorkerId = workerVM.Id,
				GroupId = groupId
			}));

			var currentSchedules = await _context.Schedules.Where(s => s.WorkerId == workerVM.Id).ToListAsync();
			_context.Schedules.RemoveRange(currentSchedules);
			await _context.Schedules.AddRangeAsync((workerVM.Schedules ?? new List<Schedule>())
				.Where(s => s.Start != TimeSpan.Zero && s.End != TimeSpan.Zero)
				.Select(s => new Schedule { WorkerId = workerVM.Id, Day = s.Day, Start = s.Start, End = s.End }));

			await _context.SaveChangesAsync();
		}

		public async Task DeleteAsync(string workerId)
		{
			var worker = await _context.Users.FirstOrDefaultAsync(u => u.Id == workerId && u.Role == UserRoles.Worker);
			if (worker == null)
				throw new KeyNotFoundException("Worker was not found.");

			if (await _context.Attendances.AnyAsync(a => a.WorkerId == workerId))
				throw new InvalidOperationException("A worker with appointment history cannot be deleted.");

			_context.Users.Remove(worker);
			await _context.SaveChangesAsync();
		}

		private static void ValidateSchedules(List<Schedule> schedules, string workerId)
		{
			if (schedules.Any(s => s.WorkerId != workerId))
				throw new InvalidOperationException("All schedule entries must belong to the same worker.");

			var validDays = Enum.GetNames(typeof(Days)).ToHashSet(StringComparer.OrdinalIgnoreCase);
			if (schedules.Any(s => !validDays.Contains(s.Day)))
				throw new InvalidOperationException("One or more schedule days are invalid.");
			if (schedules.GroupBy(s => s.Day, StringComparer.OrdinalIgnoreCase).Any(g => g.Count() > 1))
				throw new InvalidOperationException("A worker can have only one schedule entry per day.");
			if (schedules.Any(s => (s.Start == TimeSpan.Zero) != (s.End == TimeSpan.Zero)))
				throw new InvalidOperationException("Worker schedules require both start and end times.");
			if (schedules.Any(s => s.Start != TimeSpan.Zero && s.Start >= s.End))
				throw new InvalidOperationException("Schedule end time must be later than start time.");
		}
	}
}
