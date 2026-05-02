namespace NguyenBinhMinh_FBPageAPI.Models
{
    public class SpamDetectionResult
    {
        public bool IsSpam { get; set; }
        public string SpamLevel { get; set; } = "none";
        public string Reason { get; set; } = string.Empty;
    }
}