namespace HeartCathAPI.Areas.Doctor.DTOs
{
    public class PatientRequest
    {
        public int Id { get; set; }
        public string FullName { get; set; } = null!;
        public int? Age { get; set; }
        public DateTime? DateOfBirth { get; set; }
        public string? Gender { get; set; }
        public string? PhoneNumber { get; set; }
        public string? MedicalRecordNumber { get; set; }
        public string? Notes { get; set; }
        public string? ChronicDiseases { get; set; }
        public DateTime CreatedAt { get; set; }
        public List<StudyDto> Studies { get; set; } = new();
    }

    public class StudyDto
    {
        public int Id { get; set; }
        public string FilePath { get; set; } = null!;
        public string Status { get; set; } = null!;
        public DateTime UploadDate { get; set; }
        public List<AnalysisResultDto> AnalysisResults { get; set; } = new();
    }

    public class AnalysisResultDto
    {
        public int Id { get; set; }
        public double StenosisPercentage { get; set; }
        public string RiskLevel { get; set; } = null!;
        public string ImagePath { get; set; } = null!;
        public string ArteryName { get; set; } = null!;
    }

}
