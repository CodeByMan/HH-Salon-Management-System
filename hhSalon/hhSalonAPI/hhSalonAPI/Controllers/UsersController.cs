using hhSalon.Domain.Entities.Static;
using hhSalon.Services.Models.Dto;
using hhSalon.Services.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace hhSalonAPI.Controllers
{
	[Route("api/[controller]")]
	[ApiController]
	[Authorize]
	public class UsersController : ControllerBase
	{
		private readonly IUsersService _usersService;
		public UsersController(IUsersService usersService)
		{
			_usersService = usersService;
		}

		[Authorize(Roles = UserRoles.Admin)]
		[HttpGet]
		public async Task<ActionResult<IEnumerable<UserDto>>> GetAllUser()
		{
			return Ok(await _usersService.GetAllUser());
		}

		[HttpGet("{userId}")]
		public ActionResult GetUserById(string userId)
		{
			var user = _usersService.GetUserById(userId);
			if (user == null)
				return NotFound();

			var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
			var isAdmin = User.IsInRole(UserRoles.Admin);
			if (isAdmin || currentUserId == userId)
			{
				return Ok(new UserDto
				{
					Id = user.Id,
					FirstName = user.FirstName,
					LastName = user.LastName,
					Email = user.Email,
					UserName = user.UserName
				});
			}

			return Ok(new UserSummaryDto
			{
				Id = user.Id,
				FirstName = user.FirstName,
				LastName = user.LastName,
				UserName = user.UserName
			});
		}

		[HttpPut]
		public async Task<ActionResult> UpdateUser([FromBody] UserUpdateDto user)
		{
			var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
			if (!User.IsInRole(UserRoles.Admin) && currentUserId != user.Id)
				return Forbid();

			try
			{
				await _usersService.UpdateUser(user);
				return Ok(new { Message = "User data was updated!" });
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
	}
}
