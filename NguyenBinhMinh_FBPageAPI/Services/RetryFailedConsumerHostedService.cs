using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.Options;
using NguyenBinhMinh_FBPageAPI.Models;

namespace NguyenBinhMinh_FBPageAPI.Services
{
    public class RetryFailedConsumerHostedService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly KafkaOptions _options;
        private readonly ILogger<RetryFailedConsumerHostedService> _logger;

        public RetryFailedConsumerHostedService(
            IServiceScopeFactory scopeFactory,
            IOptions<KafkaOptions> options,
            ILogger<RetryFailedConsumerHostedService> logger)
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
                GroupId = $"{_options.ConsumerGroupId}-retry",
                AutoOffsetReset = AutoOffsetReset.Earliest,
                EnableAutoCommit = false
            };

            using var consumer = new ConsumerBuilder<Ignore, string>(config).Build();

            consumer.Subscribe(_options.TopicSendFailed);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var message = consumer.Consume(stoppingToken);

                    var evt = JsonSerializer.Deserialize<NormalizedEvent>(message.Message.Value);

                    if (evt == null || evt.RetryCount > 3)
                    {
                        consumer.Commit(message);
                        continue;
                    }

                    await Task.Delay(TimeSpan.FromSeconds(10 * evt.RetryCount), stoppingToken);

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
                    _logger.LogError(ex, "Retry consumer error");
                    await Task.Delay(3000, stoppingToken);
                }
            }

            consumer.Close();
        }
    }
}