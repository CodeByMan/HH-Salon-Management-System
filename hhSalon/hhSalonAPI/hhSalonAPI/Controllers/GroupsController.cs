using hhSalon.Domain.Entities;
using hhSalon.Domain.Entities.Static;
using hhSalon.Services.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace hhSalonAPI.Controllers
{
	[Route("api/[controller]")]
	[ApiController]
	public class GroupsController : ControllerBase
	{
		private readonly IGroupsService _groupsService;
		
		public GroupsController(IGroupsService groupsService)
		{
			_groupsService = groupsService;
		}

		[HttpGet]
		public async Task<ActionResult<List<GroupOfServices>>> GetGroups() 
		{
			return Ok(await _groupsService.GetAllAsync());
		}

		
		[HttpPost]
		[Authorize(Roles = UserRoles.Admin)]
		public async Task<ActionResult<List<GroupOfServices>>> CreateGroup(GroupOfServices newGroup)
		{
			try
			{
				await _groupsService.AddAsync(newGroup);

				return Ok(await _groupsService.GetAllAsync());
			}
			catch (DbUpdateException)
			{
				return Conflict(new { Message = "This group already exists." });
			}
			catch (Exception ex)
			{
				return BadRequest(new { Message = ex.Message });
			}
		}


		[HttpGet("{groupId}")]
		public async Task<ActionResult<GroupOfServices>> GetGroupById(int groupId)
		{
			var group = await _groupsService.GetByIdAsync(groupId);


			if (group == null)
				return BadRequest("Group not found");

			return Ok(group);
		}

		[HttpPut]
		[Authorize(Roles = UserRoles.Admin)]
		public async Task<ActionResult<List<GroupOfServices>>> UpdateGroup(GroupOfServices group)
		{
			try
			{
				await _groupsService.UpdateGroupAsync(group);

				return Ok(await _groupsService.GetAllAsync());
			}
			catch (Exception ex)
			{
				return BadRequest(new { Message = ex.Message });
			}
		}

		[HttpDelete("{id}")]
		[Authorize(Roles = UserRoles.Admin)]
		public async Task<ActionResult<List<GroupOfServices>>> DeleteGroupById(int id)
		{
			var group = await _groupsService.GetByIdAsync(id);
			if (group == null)
				return BadRequest("Group not found");

			await _groupsService.DeleteAsync(id);

			return Ok(await _groupsService.GetAllAsync());
		}


		[HttpGet("worker/{workerId}")]
		public async Task<ActionResult<GroupOfServices>> GetGroupsByWorkerId(string workerId)
		{
			var groups = await _groupsService.GetGroupsByWorkerId(workerId);


			if (groups == null)
				return BadRequest("Groups weren't found");

			return Ok(groups);
		}
	}
}
