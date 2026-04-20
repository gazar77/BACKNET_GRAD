using HeartCathAPI.Data;
using HeartCathAPI.Models;
using Google.Apis.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using HeartCathAPI.DTOs.Request;

namespace HeartCathAPI.Services.AuthServices
{
    public class AuthService : IAuthService
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly IPasswordHasher<User> _passwordHasher;
        private readonly IEmailService _emailService;
        private readonly IHttpClientFactory _httpClientFactory;

        public AuthService(
            ApplicationDbContext context,
            IConfiguration configuration,
            IPasswordHasher<User> passwordHasher,
            IEmailService emailService,
            IHttpClientFactory httpClientFactory)
        {
            _context = context;
            _configuration = configuration;
            _passwordHasher = passwordHasher;
            _emailService = emailService;
            _httpClientFactory = httpClientFactory;
        }

        // ================= Register =================
        public async Task<AuthResult> RegisterAsync(RegisterRequest model)
        {
            model.Username = model.Username.Trim();
            model.Email = model.Email.Trim().ToLower();

            var exists = await _context.Users
                .AnyAsync(x => x.Email == model.Email);

            if (exists)
                return new AuthResult { Success = false, Message = "Email already exists" };

            var user = new User
            {
                FullName = model.Username,
                Email = model.Email,
                Title = model.Title,
                Hospital = model.Hospital,
                Mobile = model.Mobile,
                Extension = model.Extension
            };

            user.PasswordHash = _passwordHasher.HashPassword(user, model.Password);

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            return new AuthResult
            {
                Success = true,
                Message = "User registered successfully"
            };
        }

        // ================= Login =================
        public async Task<AuthResult> LoginAsync(LoginRequest model)
        {
            model.Email = model.Email.Trim().ToLower();

            var user = await _context.Users
                .FirstOrDefaultAsync(x => x.Email == model.Email);

            if (user == null)
                return new AuthResult { Success = false, Message = "Invalid email or password" };

            var result = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, model.Password);

            if (result == PasswordVerificationResult.Failed)
                return new AuthResult { Success = false, Message = "Invalid email or password" };

            var token = CreateJwtToken(user);

            return new AuthResult
            {
                Success = true,
                Message = "Login successful",
                Token = token,
                User = new { 
                    user.Id, 
                    user.FullName, 
                    user.Email,
                    user.Title,
                    user.Hospital,
                    user.Mobile,
                    user.Extension
                }
            };
        }

        // ================= Google Login =================
        public async Task<AuthResult> GoogleLoginAsync(GoogleLoginRequest model)
        {
            try
            {
                var settings = new GoogleJsonWebSignature.ValidationSettings
                {
                    Audience = new List<string>()
                    {
                        _configuration["Google:ClientId"]!
                    }
                };

                var payload = await GoogleJsonWebSignature.ValidateAsync(model.IdToken, settings);

                if (payload == null)
                    return new AuthResult { Success = false, Message = "Invalid Google token" };

                var user = await _context.Users
                    .FirstOrDefaultAsync(x => x.Email == payload.Email);

                if (user == null)
                {
                    user = new User
                    {
                        Email = payload.Email,
                        FullName = payload.Name ?? payload.Email.Split('@')[0]
                    };

                    var randomPassword = Guid.NewGuid().ToString();

                    user.PasswordHash = _passwordHasher.HashPassword(user, randomPassword);

                    _context.Users.Add(user);
                    await _context.SaveChangesAsync();
                }

                var token = CreateJwtToken(user);

                return new AuthResult
                {
                    Success = true,
                    Message = "Google login successful",
                    Token = token,
                    User = new { 
                        user.Id, 
                        user.FullName, 
                        user.Email,
                        user.Title,
                        user.Hospital,
                        user.Mobile,
                        user.Extension
                    }
                };
            }
            catch (Exception ex)
            {
                return new AuthResult
                {
                    Success = false,
                    Message = $"Google login failed: {ex.Message}"
                };
            }
        }

        // ================= Facebook Login =================
        public async Task<AuthResult> FacebookLoginAsync(FacebookLoginRequest model)
        {
            try
            {
                var client = _httpClientFactory.CreateClient();
                var response = await client.GetAsync($"https://graph.facebook.com/me?fields=id,name,email&access_token={model.AccessToken}");

                if (!response.IsSuccessStatusCode)
                    return new AuthResult { Success = false, Message = "Invalid Facebook token" };

                var content = await response.Content.ReadAsStringAsync();
                var facebookUser = System.Text.Json.JsonSerializer.Deserialize<FacebookUserResponse>(content, new System.Text.Json.JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (facebookUser == null || string.IsNullOrEmpty(facebookUser.Email))
                    return new AuthResult { Success = false, Message = "Could not retrieve email from Facebook" };

                var user = await _context.Users
                    .FirstOrDefaultAsync(x => x.Email == facebookUser.Email);

                if (user == null)
                {
                    user = new User
                    {
                        Email = facebookUser.Email,
                        FullName = facebookUser.Name ?? facebookUser.Email.Split('@')[0]
                    };

                    var randomPassword = Guid.NewGuid().ToString();
                    user.PasswordHash = _passwordHasher.HashPassword(user, randomPassword);

                    _context.Users.Add(user);
                    await _context.SaveChangesAsync();
                }

                var token = CreateJwtToken(user);

                return new AuthResult
                {
                    Success = true,
                    Message = "Facebook login successful",
                    Token = token,
                    User = new { 
                        user.Id, 
                        user.FullName, 
                        user.Email,
                        user.Title,
                        user.Hospital,
                        user.Mobile,
                        user.Extension
                    }
                };
            }
            catch (Exception ex)
            {
                return new AuthResult
                {
                    Success = false,
                    Message = $"Facebook login failed: {ex.Message}"
                };
            }
        }

        private class FacebookUserResponse
        {
            public string Id { get; set; } = null!;
            public string Name { get; set; } = null!;
            public string Email { get; set; } = null!;
        }

        // ================= Create JWT =================
        private string CreateJwtToken(User user)
        {
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.FullName),
                new Claim(ClaimTypes.Email, user.Email)
            };

            var key = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]!));

            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _configuration["Jwt:Issuer"],
                audience: _configuration["Jwt:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddDays(1),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        // ================= Send OTP =================
        public async Task<AuthResult> SendOtpAsync(ForgotPasswordRequest model)
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(x => x.Email == model.Email);

            if (user == null)
                return new AuthResult { Success = false, Message = "Email not found" };

            var lastOtp = await _context.PasswordResetOtps
                .Where(x => x.Email == model.Email)
                .OrderByDescending(x => x.CreatedAt)
                .FirstOrDefaultAsync();

            if (lastOtp != null && lastOtp.CreatedAt > DateTime.UtcNow.AddMinutes(-1))
                return new AuthResult
                {
                    Success = false,
                    Message = "Please wait before requesting another OTP"
                };

            var otp = new Random().Next(100000, 999999).ToString();

            var reset = new PasswordResetOtp
            {
                Email = model.Email,
                Otp = otp,
                ExpireAt = DateTime.UtcNow.AddMinutes(5),
                CreatedAt = DateTime.UtcNow
            };

            _context.PasswordResetOtps.Add(reset);
            await _context.SaveChangesAsync();

            await _emailService.SendOtpAsync(model.Email, otp);

            return new AuthResult
            {
                Success = true,
                Message = "OTP sent to email"
            };
        }

        // ================= Verify OTP =================
        public async Task<AuthResult> VerifyOtpAsync(VerifyOtpRequest model)
        {
            var record = await _context.PasswordResetOtps
                .FirstOrDefaultAsync(x =>
                    x.Email == model.Email &&
                    x.Otp == model.Otp &&
                    x.IsUsed == false &&
                    x.ExpireAt > DateTime.UtcNow);

            if (record == null)
                return new AuthResult
                {
                    Success = false,
                    Message = "Invalid or expired OTP"
                };

            return new AuthResult
            {
                Success = true,
                Message = "OTP verified"
            };
        }

        // ================= Reset Password =================
        public async Task<AuthResult> ResetPasswordAsync(ResetPasswordRequest model)
        {
            var record = await _context.PasswordResetOtps
                .FirstOrDefaultAsync(x =>
                    x.Email == model.Email &&
                    x.Otp == model.Otp &&
                    x.IsUsed == false &&
                    x.ExpireAt > DateTime.UtcNow);

            if (record == null)
                return new AuthResult
                {
                    Success = false,
                    Message = "Invalid OTP"
                };

            var user = await _context.Users
                .FirstOrDefaultAsync(x => x.Email == model.Email);

            if (user == null)
                return new AuthResult { Success = false, Message = "User not found" };

            user.PasswordHash = _passwordHasher.HashPassword(user, model.NewPassword);

            record.IsUsed = true;

            await _context.SaveChangesAsync();

            return new AuthResult
            {
                Success = true,
                Message = "Password updated successfully"
            };
        }

        // ================= Change Password =================
        public async Task<AuthResult> ChangePasswordAsync(ChangePasswordRequest model, string userId)
        {
            var user = await _context.Users.FirstOrDefaultAsync(x => x.Id == int.Parse(userId));
            if (user == null)
                return new AuthResult { Success = false, Message = "User not found" };

            var result = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, model.OldPassword);
            if (result == PasswordVerificationResult.Failed)
                return new AuthResult { Success = false, Message = "Invalid old password" };

            user.PasswordHash = _passwordHasher.HashPassword(user, model.NewPassword);
            await _context.SaveChangesAsync();

            return new AuthResult { Success = true, Message = "Password changed successfully" };
        }

        // ================= Update Email =================
        public async Task<AuthResult> UpdateEmailAsync(UpdateEmailRequest model, string userId)
        {
            model.NewEmail = model.NewEmail.Trim().ToLower();

            var user = await _context.Users.FirstOrDefaultAsync(x => x.Id == int.Parse(userId));
            if (user == null)
                return new AuthResult { Success = false, Message = "User not found" };

            if (user.Email == model.NewEmail)
                return new AuthResult { Success = false, Message = "This is already your email" };

            var exists = await _context.Users.AnyAsync(x => x.Email == model.NewEmail);
            if (exists)
                return new AuthResult { Success = false, Message = "Email is already in use" };

            user.Email = model.NewEmail;
            await _context.SaveChangesAsync();

            return new AuthResult { Success = true, Message = "Email updated successfully" };
        }
    }
}