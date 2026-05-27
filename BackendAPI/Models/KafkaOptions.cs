namespace BackendAPI.Models
{
    public class KafkaOptions
    {
        public string BootstrapServers { get; set; } = "localhost:9092";

        public string TopicReplyCommands { get; set; } = "reply_commands";
        public string TopicSendRetry { get; set; } = "send_retry";
        public string TopicSendFailed { get; set; } = "send_failed";

        public string ConsumerGroupId { get; set; } = "backend-api";
    }
}