using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.Options;
using NguyenBinhMinh_FBPageAPI.Models;

namespace NguyenBinhMinh_FBPageAPI.Services
{
    public class KafkaProducerService : IDisposable
    {
        private readonly IProducer<Null, string> _producer;
        private readonly KafkaOptions _options;
        private readonly ILogger<KafkaProducerService> _logger;

        public KafkaProducerService(
            IOptions<KafkaOptions> options,
            ILogger<KafkaProducerService> logger)
        {
            _options = options.Value;
            _logger = logger;

            var config = new ProducerConfig
            {
                BootstrapServers = _options.BootstrapServers,
                EnableIdempotence = true,
                Acks = Acks.All
            };

            _producer = new ProducerBuilder<Null, string>(config).Build();
        }

        public Task PublishRawEventAsync(NormalizedEvent evt, CancellationToken cancellationToken = default)
        {
            return PublishAsync(_options.TopicRawEvents, evt, cancellationToken);
        }

        public Task PublishSendFailedAsync(NormalizedEvent evt, CancellationToken cancellationToken = default)
        {
            return PublishAsync(_options.TopicSendFailed, evt, cancellationToken);
        }

        private async Task PublishAsync(string topic, NormalizedEvent evt, CancellationToken cancellationToken)
        {
            var payload = JsonSerializer.Serialize(evt);

            var result = await _producer.ProduceAsync(
                topic,
                new Message<Null, string> { Value = payload },
                cancellationToken);

            _logger.LogInformation(
                "Kafka ACK topic={Topic}, partition={Partition}, offset={Offset}",
                result.Topic,
                result.Partition,
                result.Offset);
        }

        public void Dispose()
        {
            _producer.Flush(TimeSpan.FromSeconds(5));
            _producer.Dispose();
        }
    }
}