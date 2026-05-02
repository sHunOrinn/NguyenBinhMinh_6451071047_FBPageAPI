namespace NguyenBinhMinh_FBPageAPI.Models
{
    public class AiClassificationResult
    {
        public string Intent { get; set; } = "unknown";
        public string Sentiment { get; set; } = "neutral";
        public string ReplyMessage { get; set; } = string.Empty;
    }
}