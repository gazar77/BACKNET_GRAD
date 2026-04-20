namespace HeartCathAPI.DTOs.Response
{
    public class StudyResponse
    {
        public int Id { get; set; }

        public int PatientId { get; set; }

        public string FilePath { get; set; }

        public string Status { get; set; }

        public DateTime UploadDate { get; set; }
    }
}
