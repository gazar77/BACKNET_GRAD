namespace HeartCathAPI.Services
{
    public interface IEmailService
    {
        Task SendOtpAsync(string email, string otp);
    }
}
