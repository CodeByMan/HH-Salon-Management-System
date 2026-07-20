using hhSalon.Domain.Entities;
using hhSalon.Domain.Entities.Static;
using hhSalon.Services.Models.Dto;
using hhSalon.Services.Services.Interfaces;
using hhSalon.Services.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace hhSalonAPI.Controllers
{
	[Route("api/[controller]")]
	[ApiController]
	public class WorkersController : ControllerBase
	{
		private readonly IWorkersService _workersService;
		public WorkersController(IWorkersService workersService)
		{
			_workersService = workersService;
		}

		[AllowAnonymous]
		[HttpGet]
		public async Task<ActionResult<IEnumerable<WorkerListDto>>> GetWorkers()
		{
			return Ok(await _workersService.GetWorkersAsync());
		}

		[AllowAnonymous]
		[HttpGet("{groupId:int}")]
		public async Task<ActionResult<IEnumerable<WorkerVM>>> GetWorkersByGroupId(int groupId)
		{
			return Ok(await _workersService.GetWorkersByGroupId(groupId));
		}

		[Authorize(Roles = $"{UserRoles.Admin},{UserRoles.Worker}")]
		[HttpGet("info")]
		public async Task<ActionResult<WorkerVM>> GetWorkerById([FromQuery] string workerId)
		{
			if (!CanManageWorker(workerId))
				return Forbid();
			try
			{
				return Ok(await _workersService.GetWorkerVMByIdAsync(workerId));
			}
			catch (KeyNotFoundException)
			{
				return NotFound();
			}
		}

		[Authorize(Roles = $"{UserRoles.Admin},{UserRoles.Worker}")]
		[HttpPost("schedule/create")]
		public async Task<ActionResult> CreateWorkerSchedule([FromBody] List<Schedule> schedules)
		{
			if (schedules == null || schedules.Count == 0 || !CanManageWorker(schedules[0].WorkerId))
				return Forbid();
			try
			{
				await _workersService.CreateWorkerSchedule(schedules);
				return Ok(new { Message = "Worker schedule was updated!" });
			}
			catch (Exception ex) when (ex is ArgumentException || ex is InvalidOperationException || ex is KeyNotFoundException)
			{
				return BadRequest(new { Message = ex.Message });
			}
		}

		[Authorize(Roles = $"{UserRoles.Admin},{UserRoles.Worker}")]
		[HttpPut]
		public async Task<ActionResult> UpdateWorker([FromBody] WorkerVM workerVM)
		{
			if (!CanManageWorker(workerVM.Id))
				return Forbid();

			try
			{
				if (User.IsInRole(UserRoles.Worker))
				{
					var existing = await _workersService.GetWorkerVMByIdAsync(workerVM.Id);
					workerVM.GroupsIds = existing.GroupsIds;
				}
				await _workersService.UpdateWorkerAsync(workerVM);
				return Ok(new { Message = "Worker data was updated!" });
			}
			catch (Exception ex) when (ex is ArgumentException || ex is InvalidOperationException || ex is KeyNotFoundException)
			{
				return BadRequest(new { Message = ex.Message });
			}
		}

		[Authorize(Roles = UserRoles.Admin)]
		[HttpDelete]
		public async Task<ActionResult<IEnumerable<WorkerListDto>>> Delete([FromQuery] string workerId)
		{
			try
			{
				await _workersService.DeleteAsync(workerId);
				return Ok(await _workersService.GetWorkersAsync());
			}
			catch (Exception ex) when (ex is InvalidOperationException || ex is KeyNotFoundException)
			{
				return BadRequest(new { Message = ex.Message });
			}
		}

		private bool CanManageWorker(string workerId)
			=> User.IsInRole(UserRoles.Admin) || User.FindFirstValue(ClaimTypes.NameIdentifier) == workerId;
	}
}
