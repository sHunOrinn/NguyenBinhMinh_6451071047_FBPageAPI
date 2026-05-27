namespace CoreServices.Models
{
    public class ReplyCommand
    {
        public int SchemaVersion { get; set; } = 1;
        public string CommandId { get; set; } = Guid.NewGuid().ToString();
        public string EventId { get; set; } = string.Empty;

        public string Action { get; set; } = "reply";

        public string PageId { get; set; } = string.Empty;
        public string? PostId { get; set; }
        public string? CommentId { get; set; }
        public string? UserId { get; set; }

        public string? ReplyText { get; set; }

        public string Intent { get; set; } = "unknown";
        public string Sentiment { get; set; } = "neutral";

        public string? LastError { get; set; }

        public int RetryCount { get; set; } = 0;
        public string Status { get; set; } = "processed";

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    }
}
