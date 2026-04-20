using System.Net;
using System.Net.Mail;

namespace HeartCathAPI.Services
{
    public class EmailService : IEmailService
    {
        private readonly IConfiguration _configuration;

        public EmailService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task SendOtpAsync(string email, string otp)
        {
            var fromEmail = _configuration["Email:Address"];
            var password = _configuration["Email:Password"];

            var smtp = new SmtpClient("smtp.gmail.com")
            {
                Port = 587,
                Credentials = new NetworkCredential(fromEmail, password),
                EnableSsl = true
            };

            var message = new MailMessage(
                fromEmail!,
                email,
                "Password Reset OTP",
                $"Your OTP code is: {otp}"
            );

            await smtp.SendMailAsync(message);
        }
    }
}