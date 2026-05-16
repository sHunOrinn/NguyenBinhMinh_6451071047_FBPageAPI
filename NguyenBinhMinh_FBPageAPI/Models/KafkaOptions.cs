namespace NguyenBinhMinh_FBPageAPI.Models
{
    public class KafkaOptions
    {
        public string BootstrapServers { get; set; } = "localhost:9092";
        public string TopicRawEvents { get; set; } = "raw_events";
        public string TopicReplyCommands { get; set; } = "reply_commands";
        public string TopicSendFailed { get; set; } = "send_failed";
        public string TopicSendRetry { get; set; } = "send_retry";
        public string TopicDeadLetter { get; set; } = "dead_letter";
        public string CoreConsumerGroupId { get; set; } = "core-service";
    }
}