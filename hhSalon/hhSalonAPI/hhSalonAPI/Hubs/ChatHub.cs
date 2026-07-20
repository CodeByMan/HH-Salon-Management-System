using hhSalon.Domain.Entities;
using hhSalon.Services.Models.Dto;
using hhSalon.Services.Services.Implementations;
using hhSalon.Services.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace hhSalonAPI.Hubs
{
	[Authorize]
	public class ChatHub : Hub
	{
		private readonly ChatService _chatService;
		private readonly IChatDataService _chatDataService;
		private readonly ChatMessageRateLimiter _messageRateLimiter;

		public ChatHub(ChatService chatService, IChatDataService chatDataService, ChatMessageRateLimiter messageRateLimiter)
		{
			_chatService = chatService;
			_chatDataService = chatDataService;
			_messageRateLimiter = messageRateLimiter;
		}

		public override async Task OnConnectedAsync()
		{
			_chatService.AddUserConnectionId(CurrentUserId(), Context.ConnectionId);
			await Clients.Caller.SendAsync("UserConnected");
			await base.OnConnectedAsync();
		}

		public override async Task OnDisconnectedAsync(Exception exception)
		{
			_chatService.RemoveConnection(Context.ConnectionId);
			await base.OnDisconnectedAsync(exception);
		}

		public Task AddUserConnectionId(string ignoredUserId = null)
		{
			_chatService.AddUserConnectionId(CurrentUserId(), Context.ConnectionId);
			return Task.CompletedTask;
		}

		public async Task ReceivePrivateMessage(ChatMessageInputDto message)
		{
			var senderId = CurrentUserId();
			if (!_messageRateLimiter.TryAcquire(senderId))
				throw new HubException("Too many chat messages. Please try again shortly.");

			var savedMessage = await _chatDataService.SaveMessage(new Chat
			{
				FromId = senderId,
				ToId = message.ToId,
				Content = message.Content
			});

			var recipientConnections = _chatService.GetConnectionIdsByUserId(message.ToId);
			if (recipientConnections.Count > 0)
				await Clients.Clients(recipientConnections).SendAsync("NewPrivateMessage", savedMessage);
		}

		public Task RemovePrivateChat(string ignoredUserId = null)
		{
			_chatService.RemoveConnection(Context.ConnectionId);
			return Task.CompletedTask;
		}

		public async Task ReadPrivateMessage(ChatReadDto message)
		{
			var storedMessage = await _chatDataService.GetMessageById(message.Id);
			if (storedMessage == null || storedMessage.ToId != CurrentUserId())
				throw new HubException("The message does not belong to the authenticated recipient.");

			await _chatDataService.MarkMessageRead(message.Id, CurrentUserId());
			var senderConnections = _chatService.GetConnectionIdsByUserId(storedMessage.FromId);
			if (senderConnections.Count > 0)
				await Clients.Clients(senderConnections).SendAsync("MyMessageIsRead", message);
		}

		private string CurrentUserId()
			=> Context.User?.FindFirstValue(ClaimTypes.NameIdentifier)
				?? throw new HubException("Authenticated user identity is unavailable.");
	}
}
