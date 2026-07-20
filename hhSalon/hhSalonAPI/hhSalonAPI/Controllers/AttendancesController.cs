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
	public class AttendancesController : ControllerBase
	{
		private readonly IAttendancesService _attendancesService;

		public AttendancesController(IAttendancesService attendancesService)
		{
			_attendancesService = attendancesService;
		}

		[HttpPost]
		[Authorize(Roles = UserRoles.Client)]
		public async Task<ActionResult> NewAttendance([FromBody] NewAttendanceVM newAttendanceVM)
		{
			try
			{
				await _attendancesService.AddNewAttendanceAsync(newAttendanceVM, CurrentUserId());
				return Ok();
			}
			catch (InvalidOperationException ex)
			{
				return Conflict(new { Message = ex.Message });
			}
		}

		[HttpGet("my-not-rendered-not-paid-attendances/{userId}")]
		[Authorize(Roles = $"{UserRoles.Client},{UserRoles.Admin}")]
		public async Task<ActionResult> MyNotRenderedNotPaidAttendances(string userId)
		{
			if (!CanAccessClient(userId)) return Forbid();
			return Ok(await _attendancesService.MyNotRenderedNotPaidAttendances(userId));
		}

		[HttpGet("my-not-rendered-is-paid-attendances/{userId}")]
		[Authorize(Roles = $"{UserRoles.Client},{UserRoles.Admin}")]
		public async Task<ActionResult> MyNotRenderedIsPaidAttendances(string userId)
		{
			if (!CanAccessClient(userId)) return Forbid();
			return Ok(await _attendancesService.MyNotRenderedIsPaidAttendances(userId));
		}

		[HttpGet("my-history/{userId}")]
		[Authorize(Roles = $"{UserRoles.Client},{UserRoles.Admin}")]
		public async Task<ActionResult> MyHistory(string userId)
		{
			if (!CanAccessClient(userId)) return Forbid();
			return Ok(await _attendancesService.MyIsRenderedAttendances(userId));
		}

		[HttpGet("worker-history/{workerId}")]
		[Authorize(Roles = $"{UserRoles.Worker},{UserRoles.Admin}")]
		public async Task<ActionResult> WorkerHistory(string workerId)
		{
			if (!CanAccessWorker(workerId)) return Forbid();
			return Ok(await _attendancesService.WorkerIsRenderedAttendances(workerId));
		}

		[HttpGet("worker-not-rendered-not-paid-attendances/{workerId}")]
		[Authorize(Roles = $"{UserRoles.Worker},{UserRoles.Admin}")]
		public async Task<ActionResult> WorkerNotRenderedNotPaid(string workerId)
		{
			if (!CanAccessWorker(workerId)) return Forbid();
			return Ok(await _attendancesService.WorkerNotRenderedNotPaidAttendances(workerId));
		}

		[HttpGet("worker-not-rendered-is-paid-attendances/{workerId}")]
		[Authorize(Roles = $"{UserRoles.Worker},{UserRoles.Admin}")]
		public async Task<ActionResult> WorkerNotRenderedIsPaid(string workerId)
		{
			if (!CanAccessWorker(workerId)) return Forbid();
			return Ok(await _attendancesService.WorkerNotRenderedIsPaidAttendances(workerId));
		}

		[HttpGet("worker-not-rendered-attendances/{workerId}")]
		[Authorize(Roles = $"{UserRoles.Worker},{UserRoles.Admin}")]
		public async Task<ActionResult> WorkerNotRendered(string workerId)
		{
			if (!CanAccessWorker(workerId)) return Forbid();
			return Ok(await _attendancesService.WorkerNotRenderedAttendances(workerId));
		}

		[AllowAnonymous]
		[HttpGet("time-slots/{workerId}/{day}")]
		public async Task<ActionResult> GetFreeTimeSlots(string workerId, DateTime day)
		{
			return Ok(await _attendancesService.GetFreeTimeSlots(workerId, day));
		}

		[Authorize(Roles = UserRoles.Admin)]
		[HttpPut("update-attendances")]
		public async Task<ActionResult> UpdateAttendances([FromBody] List<AttendanceUpdateDto> attendances)
		{
			try
			{
				await _attendancesService.UpdateAttendances(attendances);
				return Ok(new { Message = "Updated successfully!" });
			}
			catch (Exception ex) when (ex is KeyNotFoundException || ex is InvalidOperationException)
			{
				return Conflict(new { Message = ex.Message });
			}
		}

		[Authorize(Roles = UserRoles.Admin)]
		[HttpPut]
		public async Task<ActionResult> UpdateAttendance([FromBody] AttendanceUpdateDto attendance)
		{
			try
			{
				await _attendancesService.UpdateAttendance(attendance);
				return Ok(new { Message = "Updated successfully!" });
			}
			catch (KeyNotFoundException ex)
			{
				return NotFound(new { Message = ex.Message });
			}
			catch (InvalidOperationException ex)
			{
				return Conflict(new { Message = ex.Message });
			}
		}

		[Authorize(Roles = UserRoles.Worker)]
		[HttpPut("worker-status")]
		public async Task<ActionResult> UpdateWorkerAttendanceStatus([FromBody] WorkerAttendanceUpdateDto attendance)
		{
			try
			{
				await _attendancesService.UpdateWorkerAttendanceStatus(attendance, CurrentUserId());
				return Ok(new { Message = "Appointment workflow status updated." });
			}
			catch (UnauthorizedAccessException)
			{
				return Forbid();
			}
			catch (KeyNotFoundException ex)
			{
				return NotFound(new { Message = ex.Message });
			}
			catch (InvalidOperationException ex)
			{
				return BadRequest(new { Message = ex.Message });
			}
		}

		[Authorize(Roles = $"{UserRoles.Client},{UserRoles.Admin}")]
		[HttpDelete("{id}")]
		public async Task<ActionResult> DeleteAttendance(int id)
		{
			var attendance = await _attendancesService.GetAttendanceByIdAsync(id);
			if (attendance == null) return NotFound(new { Message = "Not found!" });
			if (!User.IsInRole(UserRoles.Admin) && attendance.ClientId != CurrentUserId())
				return Forbid();
			if (!User.IsInRole(UserRoles.Admin)
				&& (attendance.IsPaid == YesNo.Yes || attendance.IsRendered == YesNo.Yes || attendance.Date.Date < DateTime.Today))
				return Conflict(new { Message = "Paid, completed or past appointments require administrator handling." });

			await _attendancesService.DeleteAsync(id);
			return Ok(new { Message = "Deleted successfully!" });
		}

		[Authorize(Roles = UserRoles.Admin)]
		[HttpGet("all-attendances")]
		public async Task<ActionResult> GetAllAttendances()
		{
			return Ok(await _attendancesService.GetAllAttendances());
		}

		[Authorize(Roles = UserRoles.Admin)]
		[HttpGet]
		public async Task<ActionResult> FilterAttendances([FromQuery] string content)
		{
			return Ok(string.IsNullOrWhiteSpace(content)
				? await _attendancesService.GetAllAttendances()
				: await _attendancesService.GetAttendancesBySearch(content));
		}

		private string CurrentUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier);
		private bool CanAccessClient(string userId) => User.IsInRole(UserRoles.Admin) || CurrentUserId() == userId;
		private bool CanAccessWorker(string workerId) => User.IsInRole(UserRoles.Admin) || CurrentUserId() == workerId;
	}
}
