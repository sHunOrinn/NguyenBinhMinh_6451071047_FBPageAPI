using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.Options;
using RetryServices.Models;

namespace RetryServices.Services
{
    public class RetryFailedConsumerHostedService : BackgroundService
    {
        private readonly KafkaOptions _options;
        private readonly KafkaProducerService _kafkaProducer;
        private readonly ILogger<RetryFailedConsumerHostedService> _logger;

        private const int MaxRetry = 3;

        public RetryFailedConsumerHostedService(
            IOptions<KafkaOptions> options,
            KafkaProducerService kafkaProducer,
            ILogger<RetryFailedConsumerHostedService> logger)
        {
            _options = options.Value;
            _kafkaProducer = kafkaProducer;
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

            consumer.Subscribe(_options.TopicSendFailed);

            _logger.LogInformation(
                "Retry service started. Topic: {Topic}, GroupId: {GroupId}",
                _options.TopicSendFailed,
                _options.ConsumerGroupId);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var message = consumer.Consume(stoppingToken);

                    if (message?.Message?.Value == null)
                        continue;

                    var command = JsonSerializer.Deserialize<ReplyCommand>(
                        message.Message.Value,
                        new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        });

                    if (command == null)
                    {
                        _logger.LogWarning("Cannot deserialize failed command");
                        consumer.Commit(message);
                        continue;
                    }

                    if (command.RetryCount >= MaxRetry)
                    {
                        command.Status = "dead_letter";

                        await _kafkaProducer.PublishDeadLetterAsync(
                            command,
                            stoppingToken);

                        _logger.LogError(
                            "Command moved to dead_letter. CommandId: {CommandId}, RetryCount: {RetryCount}",
                            command.CommandId,
                            command.RetryCount);

                        consumer.Commit(message);
                        continue;
                    }

                    command.RetryCount++;
                    command.Status = "retrying";

                    var delaySeconds = Math.Pow(2, command.RetryCount);

                    _logger.LogWarning(
                        "Retry command later. CommandId: {CommandId}, RetryCount: {RetryCount}, Delay: {Delay}s",
                        command.CommandId,
                        command.RetryCount,
                        delaySeconds);

                    await Task.Delay(TimeSpan.FromSeconds(delaySeconds), stoppingToken);

                    await _kafkaProducer.PublishSendRetryAsync(
                        command,
                        stoppingToken);

                    consumer.Commit(message);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (ConsumeException ex)
                {
                    _logger.LogError(ex, "Kafka consume send_failed error");
                    await Task.Delay(3000, stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Retry service error");
                    await Task.Delay(3000, stoppingToken);
                }
            }

            consumer.Close();

            _logger.LogInformation("Retry service stopped");
        }
    }
}