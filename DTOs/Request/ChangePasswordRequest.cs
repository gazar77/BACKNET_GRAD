using System.ComponentModel.DataAnnotations;

namespace HeartCathAPI.DTOs.Request
{
    public class ChangePasswordRequest
    {
        [Required]
        public string OldPassword { get; set; } = null!;

        [Required]
        [MinLength(8)]
        public string NewPassword { get; set; } = null!;
    }
}
