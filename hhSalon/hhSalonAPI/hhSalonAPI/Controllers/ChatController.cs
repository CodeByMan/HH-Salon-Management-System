using hhSalon.Domain.Entities;
using hhSalon.Services.Models.Dto;
using hhSalon.Services.Services.Interfaces;
using hhSalon.Services.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;

namespace hhSalonAPI.Controllers
{
	[Route("api/[controller]")]
	[ApiController]
	[Authorize]
	public class ChatController : ControllerBase
	{
		private readonly IUsersService _usersService;
		private readonly IChatDataService _chatDataService;

		public ChatController(IUsersService usersService, IChatDataService chatDataService)
		{
			_usersService = usersService;
			_chatDataService = chatDataService;
		}

		[HttpPost("add-user/{userId}")]
		public ActionResult AddUser(string userId)
		{
			return userId == CurrentUserId() ? Ok() : Forbid();
		}

		[HttpGet("chat/{userId}")]
		public ActionResult<List<ChatItem>> ChatList(string userId)
		{
			if (userId != CurrentUserId()) return Forbid();
			return Ok(_chatDataService.GetUserMessagesList(userId).ToList());
		}

		[HttpGet("chat/messages")]
		public async Task<ActionResult<IEnumerable<ChatMessageDto>>> MessagesOfUser([FromQuery] string user, [FromQuery] string other)
		{
			if (user != CurrentUserId()) return Forbid();
			if (_usersService.GetUserById(other) == null) return NotFound();
			return Ok(await _chatDataService.GetMessagesOfUser(user, other));
		}

		[EnableRateLimiting("chat")]
		[HttpPost("save-message")]
		public async Task<ActionResult<ChatMessageDto>> SaveMessage([FromBody] ChatMessageInputDto message)
		{
			try
			{
				return Ok(await _chatDataService.SaveMessage(new Chat
				{
					FromId = CurrentUserId(),
					ToId = message.ToId,
					Content = message.Content
				}));
			}
			catch (Exception ex) when (ex is ArgumentException || ex is KeyNotFoundException)
			{
				return BadRequest(new { Message = ex.Message });
			}
		}

		[HttpPut]
		public async Task<ActionResult> UpdateMessage([FromBody] ChatReadDto message)
		{
			if (!await _chatDataService.MarkMessageRead(message.Id, CurrentUserId()))
				return Forbid();

			return Ok();
		}

		private string CurrentUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier);
	}
}
