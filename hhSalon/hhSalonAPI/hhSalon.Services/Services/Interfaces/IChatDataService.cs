using hhSalon.Domain.Entities;
using hhSalon.Services.Models.Dto;
using hhSalon.Services.ViewModels;

namespace hhSalon.Services.Services.Interfaces
{
	public interface IChatDataService
	{
		IEnumerable<ChatItem> GetUserMessagesList(string userId);
		Task<IEnumerable<ChatMessageDto>> GetMessagesOfUser(string userId, string otherUserId);
		Task<Chat> GetMessageById(int id);
		Task<ChatMessageDto> SaveMessage(Chat message);
		Task<bool> MarkMessageRead(int messageId, string recipientId);
	}
}
