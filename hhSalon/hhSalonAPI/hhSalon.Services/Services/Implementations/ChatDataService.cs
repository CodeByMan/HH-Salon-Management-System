using hhSalon.Domain.Entities;
using hhSalon.Services.Models.Dto;
using hhSalon.Services.Services.Interfaces;
using hhSalon.Services.ViewModels;
using hhSalonAPI.Domain.Concrete;
using Microsoft.EntityFrameworkCore;

namespace hhSalon.Services.Services.Implementations
{
	public class ChatDataService : IChatDataService
	{
		private readonly AppDbContext _context;
		public ChatDataService(AppDbContext context)
		{
			_context = context;
		}

		public IEnumerable<ChatItem> GetUserMessagesList(string userId)
		{
			var messages = _context.Chats.AsNoTracking()
				.Where(c => c.FromId == userId || c.ToId == userId)
				.Include(c => c.FromUser)
				.Include(c => c.ToUser)
				.OrderBy(c => c.Date)
				.ToList();

			return messages
				.GroupBy(c => c.FromId == userId ? c.ToId : c.FromId)
				.Select(group =>
				{
					var last = group.Last();
					return new ChatItem
					{
						UserId = last.FromId,
						User = ToSummary(last.FromUser),
						ToUserId = last.ToId,
						ToUser = ToSummary(last.ToUser),
						MessageUnreadCount = group.Count(c => c.ToId == userId && !c.IsRead),
						LastMessage = last.Content,
						IsRead = last.IsRead,
						Date = last.Date
					};
				})
				.OrderByDescending(item => item.Date)
				.ToList();
		}

		public async Task<IEnumerable<ChatMessageDto>> GetMessagesOfUser(string userId, string otherUserId)
		{
			return await _context.Chats.AsNoTracking()
				.Where(c => (c.FromId == otherUserId && c.ToId == userId) || (c.FromId == userId && c.ToId == otherUserId))
				.OrderBy(c => c.Date)
				.Select(c => new ChatMessageDto
				{
					Id = c.Id,
					FromId = c.FromId,
					FromUser = new UserSummaryDto
					{
						Id = c.FromUser.Id,
						FirstName = c.FromUser.FirstName,
						LastName = c.FromUser.LastName,
						UserName = c.FromUser.UserName
					},
					ToId = c.ToId,
					ToUser = new UserSummaryDto
					{
						Id = c.ToUser.Id,
						FirstName = c.ToUser.FirstName,
						LastName = c.ToUser.LastName,
						UserName = c.ToUser.UserName
					},
					Content = c.Content,
					Date = c.Date,
					IsRead = c.IsRead
				})
				.ToListAsync();
		}

		public async Task<Chat> GetMessageById(int id)
			=> await _context.Chats.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id);

		public async Task<ChatMessageDto> SaveMessage(Chat message)
		{
			if (string.IsNullOrWhiteSpace(message.FromId) || string.IsNullOrWhiteSpace(message.ToId) || string.IsNullOrWhiteSpace(message.Content))
				throw new ArgumentException("Sender, recipient and message content are required.");
			if (message.Content.Length > 2000)
				throw new ArgumentException("Message content cannot exceed 2000 characters.");
			if (!await _context.Users.AnyAsync(u => u.Id == message.ToId))
				throw new KeyNotFoundException("Recipient was not found.");

			var chat = new Chat
			{
				FromId = message.FromId,
				ToId = message.ToId,
				Content = message.Content.Trim(),
				Date = DateTime.UtcNow,
				IsRead = false
			};
			await _context.Chats.AddAsync(chat);
			await _context.SaveChangesAsync();

			return (await GetMessagesOfUser(chat.FromId, chat.ToId)).Last(c => c.Id == chat.Id);
		}

		public async Task<bool> MarkMessageRead(int messageId, string recipientId)
		{
			var message = await _context.Chats.FirstOrDefaultAsync(c => c.Id == messageId && c.ToId == recipientId);
			if (message == null)
				return false;
			message.IsRead = true;
			await _context.SaveChangesAsync();
			return true;
		}

		private static UserSummaryDto ToSummary(User user)
		{
			if (user == null) return null;
			return new UserSummaryDto
			{
				Id = user.Id,
				FirstName = user.FirstName,
				LastName = user.LastName,
				UserName = user.UserName
			};
		}
	}
}
