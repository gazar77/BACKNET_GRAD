namespace HeartCathAPI.DTOs.Request
{
    public class UploadStudyRequest
    {
        public int PatientId { get; set; }

        public IFormFile File { get; set; }
    }
}
