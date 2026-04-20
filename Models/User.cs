using System.ComponentModel.DataAnnotations;

namespace HeartCathAPI.Models
{
    public class User
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(150)]
        public string FullName { get; set; } = null!;
        public string? ProfileImageUrl { get; set; }
        [Required]
        [EmailAddress]
        [MaxLength(256)]
        public string Email { get; set; } = null!;

        [Required]
        public string PasswordHash { get; set; } = null!;

        public string? Hospital { get; set; }
        public string? Title { get; set; }
        public string? Mobile { get; set; }
        public string? Extension { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public ICollection<Study> Studies { get; set; } = new List<Study>();
        public ICollection<Patient> Patients { get; set; } = new List<Patient>();
    }
}
