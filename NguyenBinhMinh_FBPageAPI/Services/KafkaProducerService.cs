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

        public KafkaProducerService(IOptions<KafkaOptions> options)
        {
            _options = options.Value;

            var config = new ProducerConfig
            {
                BootstrapServers = _options.BootstrapServers
            };

            _producer = new ProducerBuilder<Null, string>(config).Build();
        }

        public async Task PublishRawEventAsync(NormalizedEvent evt, CancellationToken cancellationToken = default)
        {
            var payload = JsonSerializer.Serialize(evt);

            await _producer.ProduceAsync(
                _options.TopicRawEvents,
                new Message<Null, string> { Value = payload },
                cancellationToken);
        }

        public void Dispose()
        {
            _producer.Flush(TimeSpan.FromSeconds(5));
            _producer.Dispose();
        }
    }
}