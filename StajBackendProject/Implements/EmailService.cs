using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using StajBackendProject.Interfaces;

namespace StajBackendProject.Implements
{
    public class EmailService : IEmailService
    {
        private readonly IConfiguration _configuration;

        public EmailService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public void SendEmail(string toEmail, string subject, string message)
        {
            try
            {
                var emailMessage = new MimeMessage();

                var senderName = _configuration["EmailSettings:SenderName"];
                var senderEmail = _configuration["EmailSettings:SenderEmail"];

                emailMessage.From.Add(new MailboxAddress(senderName, senderEmail));
                emailMessage.To.Add(new MailboxAddress("", toEmail));
                emailMessage.Subject = subject;

                var bodyBuilder = new BodyBuilder
                {
                    HtmlBody = $"<h3>Hello!</h3><p>{message}</p><br><p><b>Staj Backend Team</b></p>",
                    TextBody = message
                };

                emailMessage.Body = bodyBuilder.ToMessageBody();

                using var client = new SmtpClient();
                int port = int.Parse(_configuration["EmailSettings:SmtpPort"] ?? "587");
                client.Connect(_configuration["EmailSettings:SmtpServer"], port, SecureSocketOptions.StartTls);
                client.Authenticate(senderEmail, _configuration["EmailSettings:SenderPassword"]);

                client.Send(emailMessage);
                client.Disconnect(true);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Mail delivery error: {ex.Message}");
            }
        }
    }
}