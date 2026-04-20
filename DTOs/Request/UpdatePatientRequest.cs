namespace HeartCathAPI.Areas.Doctor.DTOs
{
    public class UpdatePatientRequest
    {
        public string? FullName { get; set; } = null!;
        public int? Age { get; set; }
        public DateTime? DateOfBirth { get; set; }
        public string? Gender { get; set; }
        public string? PhoneNumber { get; set; }
        public string? MedicalRecordNumber { get; set; }
        public string? Notes { get; set; }
        public string? ChronicDiseases { get; set; }
    }

}
