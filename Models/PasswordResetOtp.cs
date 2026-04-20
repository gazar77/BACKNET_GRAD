using System.ComponentModel.DataAnnotations;

namespace HeartCathAPI.Models
{
    public class PasswordResetOtp
    {
        [Key]
        public int Id { get; set; }

        public string Email { get; set; } = null!;

        public string Otp { get; set; } = null!;

        public DateTime ExpireAt { get; set; }

        public bool IsUsed { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
