using HeartCathAPI.DTOs.Request;

namespace HeartCathAPI.Services.AuthServices
{
    public class AuthResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? Token { get; set; }
        public object? User { get; set; }
    }

    public interface IAuthService
    {
        Task<AuthResult> RegisterAsync(RegisterRequest model);
        Task<AuthResult> LoginAsync(LoginRequest model);
        Task<AuthResult> GoogleLoginAsync(GoogleLoginRequest model);
        Task<AuthResult> FacebookLoginAsync(FacebookLoginRequest model);
        Task<AuthResult> SendOtpAsync(ForgotPasswordRequest model);
        Task<AuthResult> VerifyOtpAsync(VerifyOtpRequest model);
        Task<AuthResult> ResetPasswordAsync(ResetPasswordRequest model);
        Task<AuthResult> ChangePasswordAsync(ChangePasswordRequest model, string userId);
        Task<AuthResult> UpdateEmailAsync(UpdateEmailRequest model, string userId);
    }
}
