namespace CoreServices.Models
{
    public class SpamDetectionResult
    {
        public bool IsSpam { get; set; }
        public bool IsInappropriate { get; set; }
        public string Reason { get; set; } = string.Empty;
        public int Score { get; set; }
    }
}