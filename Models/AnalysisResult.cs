namespace HeartCathAPI.Models;


public class AnalysisResult
{
    public int Id { get; set; }

    public int StudyId { get; set; }

    public string ImagePath { get; set; } = null!;
    public double StenosisPercentage { get; set; }
    public string Report { get; set; } = null!;
    public string? ArteryName { get; set; }
    public string? RiskLevel { get; set; }
    public string? DiagnosisDetails { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Study Study { get; set; } = null!;
}
