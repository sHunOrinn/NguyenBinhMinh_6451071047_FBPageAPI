namespace NguyenBinhMinh_FBPageAPI.Models
{
    public class KafkaOptions
    {
        public string BootstrapServers { get; set; } = "localhost:9092";
        public string TopicRawEvents { get; set; } = "raw_events";
    }
}