namespace NguyenBinhMinh_FBPageAPI.Models
{
    public class NormalizedEvent
    {
        public string EventId { get; set; } = string.Empty;
        public string Source { get; set; } = "facebook";
        public string EventType { get; set; } = string.Empty;

        public string PageId { get; set; } = string.Empty;
        public string? PostId { get; set; }
        public string? CommentId { get; set; }

        public string? ActorId { get; set; }
        public string? ActorName { get; set; }

        public string? Message { get; set; }
        public DateTime CreatedAtUtc { get; set; }

        public string Status { get; set; } = "received";
        public int RetryCount { get; set; } = 0;
        public string? LastError { get; set; }

        public string RawJson { get; set; } = string.Empty;
    }
}