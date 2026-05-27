namespace CoreServices.Models
{
    public class AiClassificationResult
    {
        public string Intent { get; set; } = "unknown";
        public string Sentiment { get; set; } = "neutral";
        public string ReplyMessage { get; set; } = string.Empty;
        public bool IsSpam { get; set; }
        public bool IsInappropriate { get; set; }
        public string Reason { get; set; } = string.Empty;
        public bool UsedAi { get; set; }
    }
}
