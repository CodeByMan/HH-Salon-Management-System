using System.ComponentModel.DataAnnotations;

namespace hhSalon.Services.Models.Dto
{
	public class ChatMessageInputDto
	{
		[Required]
		public string ToId { get; set; }

		[Required, StringLength(2000)]
		public string Content { get; set; }
	}

	public class ChatReadDto
	{
		[Required]
		public int Id { get; set; }
	}

	public class ChatMessageDto
	{
		public int Id { get; set; }
		public string FromId { get; set; }
		public UserSummaryDto FromUser { get; set; }
		public string ToId { get; set; }
		public UserSummaryDto ToUser { get; set; }
		public string Content { get; set; }
		public DateTime? Date { get; set; }
		public bool IsRead { get; set; }
	}
}
