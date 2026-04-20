namespace HeartCathAPI.DTOs.Request
{
    public class UpdateDoctorRequest
    {
        public string? FullName { get; set; }

        public string? Hospital { get; set; }
        public string? Title { get; set; }
        public string? Mobile { get; set; }
        public string? Extension { get; set; }
        public IFormFile? ProfileImage { get; set; }
    }
}
