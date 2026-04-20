namespace HeartCathAPI.Models
{
    public class Patient
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
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // User Link (Doctor)
        public int UserId { get; set; }
        public User User { get; set; } = null!;

        public ICollection<Study> Studies { get; set; } = new List<Study>();
    }
}
