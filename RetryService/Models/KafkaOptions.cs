namespace RetryServices.Models
{
    public class KafkaOptions
    {
        public string BootstrapServers { get; set; } = "localhost:9092";

        public string TopicSendFailed { get; set; } = "send_failed";
        public string TopicSendRetry { get; set; } = "send_retry";
        public string TopicDeadLetter { get; set; } = "dead_letter";

        public string ConsumerGroupId { get; set; } = "retry-service";
    }
}