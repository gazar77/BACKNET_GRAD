using System.ComponentModel.DataAnnotations;

namespace HeartCathAPI.DTOs.Request
{
    public class UpdateEmailRequest
    {
        [Required]
        [EmailAddress]
        public string NewEmail { get; set; } = null!;
    }
}
