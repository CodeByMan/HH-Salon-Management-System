using hhSalon.Services.Models.Dto;

namespace hhSalon.Services.ViewModels
{
	public class ChatItem
	{
		public string UserId { get; set; }
		public UserSummaryDto User { get; set; }
		public int MessageUnreadCount { get; set; }
		public string LastMessage { get; set; }
		public bool IsRead { get; set; }
		public DateTime? Date { get; set; }
		public string ToUserId { get; set; }
		public UserSummaryDto ToUser { get; set; }
	}
}
