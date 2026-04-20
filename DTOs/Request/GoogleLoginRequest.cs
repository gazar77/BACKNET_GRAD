using System.ComponentModel.DataAnnotations;

namespace HeartCathAPI.DTOs.Request
{
    public class GoogleLoginRequest
    {
        [Required]
        public string IdToken { get; set; } = string.Empty;
    }

    public class FacebookLoginRequest
    {
        [Required]
        public string AccessToken { get; set; } = string.Empty;
    }
}
