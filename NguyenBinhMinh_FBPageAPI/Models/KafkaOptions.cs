namespace NguyenBinhMinh_FBPageAPI.Models
{
    public class KafkaOptions
    {
        public string BootstrapServers { get; set; } = "localhost:9092";
        public string TopicRawEvents { get; set; } = "raw_events";
        public string TopicSendFailed { get; set; } = "send_failed";
        public string ConsumerGroupId { get; set; } = "core-service";
    }
}