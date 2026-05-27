namespace CoreServices.Models
{
    public class KafkaOptions
    {
        public string BootstrapServers { get; set; } = "localhost:9092";
        public string TopicRawEvents { get; set; } = "raw_events";
        public string TopicReplyCommands { get; set; } = "reply_commands";
        public string CoreConsumerGroupId { get; set; } = "core-service";
    }
}