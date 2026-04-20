namespace HeartCathAPI.DTOs.Response
{
    public class StudyDetailsResponse
    {
        public int StudyId { get; set; }

        public PatientInfo Patient { get; set; }

        public string VideoPath { get; set; }

        public string Status { get; set; }

        public AnalysisInfo? Analysis { get; set; }
    }

    public class PatientInfo
    {
        public int Id { get; set; }

        public string Name { get; set; }

        public string? Notes { get; set; }
    }

    public class AnalysisInfo
    {
        public double Percentage { get; set; }

        public string Report { get; set; }

        public string Image { get; set; }
    }
}
