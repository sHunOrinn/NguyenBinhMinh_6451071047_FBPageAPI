using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.Options;
using CoreServices.Models;

namespace CoreServices.Services
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
                GroupId = _options.CoreConsumerGroupId,
                AutoOffsetReset = AutoOffsetReset.Earliest,
                EnableAutoCommit = false
            };

            using var consumer = new ConsumerBuilder<Ignore, string>(config).Build();

            consumer.Subscribe(_options.TopicRawEvents);

            _logger.LogInformation(
                "RawEventsConsumerHostedService started. Topic: {Topic}, GroupId: {GroupId}",
                _options.TopicRawEvents,
                _options.CoreConsumerGroupId);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var consumeResult = consumer.Consume(stoppingToken);

                    if (consumeResult?.Message?.Value == null)
                    {
                        continue;
                    }

                    var evt = JsonSerializer.Deserialize<NormalizedEvent>(
                        consumeResult.Message.Value,
                        new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        });

                    if (evt == null)
                    {
                        _logger.LogWarning("Cannot deserialize raw event message");
                        consumer.Commit(consumeResult);
                        continue;
                    }

                    using var scope = _scopeFactory.CreateScope();

                    var processor = scope.ServiceProvider
                        .GetRequiredService<CoreEventProcessorService>();

                    await processor.ProcessAsync(evt, stoppingToken);

                    consumer.Commit(consumeResult);

                    _logger.LogInformation(
                        "Raw event processed and committed. EventId: {EventId}, Offset: {Offset}",
                        evt.EventId,
                        consumeResult.Offset);
                }
                catch (ConsumeException ex)
                {
                    _logger.LogError(ex, "Kafka consume error");
                    await Task.Delay(3000, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Raw events consumer unexpected error");
                    await Task.Delay(3000, stoppingToken);
                }
            }

            consumer.Close();

            _logger.LogInformation("RawEventsConsumerHostedService stopped");
        }
    }
}