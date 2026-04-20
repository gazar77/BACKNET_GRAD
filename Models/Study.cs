using HeartCathAPI.Models.Enums;

namespace HeartCathAPI.Models
{
    public class Study
    {
        public int Id { get; set; }

        public int UserId { get; set; }
        public User User { get; set; } = null!;

        public int PatientId { get; set; }
        public Patient Patient { get; set; } = null!;

        public string FilePath { get; set; } = null!;

        public StudyFileType FileType { get; set; }

        public StudyStatus Status { get; set; } = StudyStatus.Uploaded;

        public string? Notes { get; set; }

        public DateTime UploadDate { get; set; } = DateTime.UtcNow;

        public ICollection<AnalysisResult> AnalysisResults { get; set; } = new List<AnalysisResult>();
    }
}

