using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.Options;
using NguyenBinhMinh_FBPageAPI.Models;

namespace NguyenBinhMinh_FBPageAPI.Services
{
    public class RawEventsConsumerHostedService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly KafkaOptions _options;
        private readonly ILogger<RawEventsConsumerHostedService> _logger;

        public RawEventsConsumerHostedService(
            IServiceScopeFactory scopeFactory,
            IOptions<KafkaOptions> options,
            ILogger<RawEventsConsumerHostedService> logger)
        {
            _scopeFactory = scopeFactory;
            _options = options.Value;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var config = new ConsumerConfig
            {
                BootstrapServers = _options.BootstrapServers,
                GroupId = _options.ConsumerGroupId,
                AutoOffsetReset = AutoOffsetReset.Earliest,
                EnableAutoCommit = false
            };

            using var consumer = new ConsumerBuilder<Ignore, string>(config).Build();

            consumer.Subscribe(_options.TopicRawEvents);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var message = consumer.Consume(stoppingToken);

                    var evt = JsonSerializer.Deserialize<NormalizedEvent>(message.Message.Value);

                    if (evt == null)
                    {
                        consumer.Commit(message);
                        continue;
                    }

                    using var scope = _scopeFactory.CreateScope();

                    var processor = scope.ServiceProvider
                        .GetRequiredService<CoreEventProcessorService>();

                    await processor.ProcessAsync(evt, stoppingToken);

                    consumer.Commit(message);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Raw events consumer error");
                    await Task.Delay(3000, stoppingToken);
                }
            }

            consumer.Close();
        }
    }
}