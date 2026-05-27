namespace CoreServices.Models
{
    public class EventProcessResult
    {
        public string EventId { get; set; } = string.Empty;

        public string? PageId { get; set; }
        public string? PostId { get; set; }
        public string? CommentId { get; set; }
        public string? ActorId { get; set; }

        public string Status { get; set; } = "received";

        public bool IsSpam { get; set; }
        public string SpamLevel { get; set; } = "none";
        public string SpamReason { get; set; } = string.Empty;

        public string Intent { get; set; } = "unknown";
        public string Sentiment { get; set; } = "neutral";

        public bool ShouldReply { get; set; }
        public bool ShouldHideComment { get; set; }
        public bool ShouldReviewManually { get; set; }
        public bool ShouldBlacklistUser { get; set; }
        public bool ShouldBlockUser { get; set; }

        public string? ReplyMessage { get; set; }
        public string? DecisionReason { get; set; }
        public string? ErrorMessage { get; set; }

        public DateTime ProcessedAtUtc { get; set; } = DateTime.UtcNow;
    }
}