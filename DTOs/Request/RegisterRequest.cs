using System.ComponentModel.DataAnnotations;

namespace HeartCathAPI.DTOs.Request
{
    public class RegisterRequest
    {
        [Required]
        public string Username { get; set; } = string.Empty;
        
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;
        
        [Required]
        [MinLength(6)]
        public string Password { get; set; } = string.Empty;
        
        [Required]
        [Compare("Password", ErrorMessage = "Password and confirmation password do not match.")]
        public string ConfirmPassword { get; set; } = string.Empty;

        public string? Title { get; set; }
        public string? Hospital { get; set; }
        public string? Mobile { get; set; }
        public string? Extension { get; set; }
    }
}