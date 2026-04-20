namespace HeartCathAPI.DTOs.Responses;

public class AnalysisResultResponse
{
    public int StudyId { get; set; }

    public double StenosisPercentage { get; set; }

    public string Report { get; set; }

    public List<string> Images { get; set; } = new();
}