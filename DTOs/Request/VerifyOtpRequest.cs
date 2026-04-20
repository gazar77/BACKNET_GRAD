namespace HeartCathAPI.DTOs.Request
{
    public class VerifyOtpRequest
    {
        public string Email { get; set; } = null!;
        public string Otp { get; set; } = null!;

    }
}
