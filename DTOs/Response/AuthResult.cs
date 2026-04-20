namespace HeartCathAPI.DTOs.Response
{
    public class AuthResult
    {
        public bool Success { get; set; }

        public string Message { get; set; } = string.Empty;

        public string? Token { get; set; }

        public object? User { get; set; }
    }
}
