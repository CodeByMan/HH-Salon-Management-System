using hhSalon.Services.Services.Interfaces;
using hhSalon.Services.ViewModels;
using MailKit.Net.Smtp;
using Microsoft.Extensions.Configuration;
using MimeKit;

namespace hhSalon.Services.Services.Implementations
{
	public class EmailService : IEmailService
	{
		private readonly IConfiguration _config;
		public EmailService(IConfiguration config)
		{
			_config = config;
		}

		public void SendEmail(EmailModel emailModel)
		{
			var from = _config["EmailSettings:From"];
			var username = _config["EmailSettings:Username"];
			var password = _config["EmailSettings:Password"];
			var smtpServer = _config["EmailSettings:SmtpServer"];
			var port = _config.GetValue<int?>("EmailSettings:Port") ?? 465;
			if (string.IsNullOrWhiteSpace(from) || string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(smtpServer))
				throw new InvalidOperationException("Email settings must be supplied through environment variables or user secrets.");

			var emailMessage = new MimeMessage();
			emailMessage.From.Add(new MailboxAddress("hhSalon", from));
			emailMessage.To.Add(new MailboxAddress(emailModel.To, emailModel.To));
			emailMessage.Subject = emailModel.Subject;
			emailMessage.Body = new TextPart(MimeKit.Text.TextFormat.Html) { Text = emailModel.Content };

			using var client = new SmtpClient();
			client.Connect(smtpServer, port, true);
			client.Authenticate(username, password);
			client.Send(emailMessage);
			client.Disconnect(true);
		}
	}
}
