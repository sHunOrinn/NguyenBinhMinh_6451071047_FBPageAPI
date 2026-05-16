using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.Options;
using NguyenBinhMinh_FBPageAPI.Models;

namespace NguyenBinhMinh_FBPageAPI.Services
{
    public class ReplyCommandsConsumerHostedService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly KafkaOptions _options;
        private readonly KafkaProducerService _kafkaProducer;
        private readonly ILogger<ReplyCommandsConsumerHostedService> _logger;

        public ReplyCommandsConsumerHostedService(
            IServiceScopeFactory scopeFactory,
            IOptions<KafkaOptions> options,
            KafkaProducerService kafkaProducer,
            ILogger<ReplyCommandsConsumerHostedService> logger)
        {
            _scopeFactory = scopeFactory;
            _options = options.Value;
            _kafkaProducer = kafkaProducer;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var config = new ConsumerConfig
            {
                BootstrapServers = _options.BootstrapServers,
                GroupId = "backend-api-reply-consumer",
                AutoOffsetReset = AutoOffsetReset.Earliest,
                EnableAutoCommit = false
            };

            using var consumer = new ConsumerBuilder<Ignore, string>(config).Build();

            consumer.Subscribe(new[]
            {
                _options.TopicReplyCommands,
                _options.TopicSendRetry
            });

            _logger.LogInformation(
                "ReplyCommandsConsumerHostedService started. Topics: {Topic1}, {Topic2}",
                _options.TopicReplyCommands,
                _options.TopicSendRetry);

            while (!stoppingToken.IsCancellationRequested)
            {
                ConsumeResult<Ignore, string>? message = null;
                ReplyCommand? command = null;

                try
                {
                    message = consumer.Consume(stoppingToken);

                    if (message?.Message?.Value == null)
                        continue;

                    command = JsonSerializer.Deserialize<ReplyCommand>(
                        message.Message.Value,
                        new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        });

                    if (command == null)
                    {
                        _logger.LogWarning("Cannot deserialize reply command");
                        consumer.Commit(message);
                        continue;
                    }

                    if (command.Action != "reply")
                    {
                        _logger.LogInformation(
                            "Skip command {CommandId}, action={Action}",
                            command.CommandId,
                            command.Action);

                        consumer.Commit(message);
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(command.CommentId) ||
                        string.IsNullOrWhiteSpace(command.ReplyText))
                    {
                        _logger.LogWarning(
                            "Invalid reply command {CommandId}",
                            command.CommandId);

                        consumer.Commit(message);
                        continue;
                    }

                    using var scope = _scopeFactory.CreateScope();

                    var facebookService = scope.ServiceProvider
                        .GetRequiredService<FacebookCommentActionService>();

                    await facebookService.ReplyCommentAsync(
                        command.CommentId,
                        command.ReplyText,
                        stoppingToken);

                    command.Status = "replied";
                    command.LastError = null;

                    consumer.Commit(message);

                    _logger.LogInformation(
                        "Reply command sent successfully. CommandId: {CommandId}, CommentId: {CommentId}, RetryCount: {RetryCount}",
                        command.CommandId,
                        command.CommentId,
                        command.RetryCount);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (ConsumeException ex)
                {
                    _logger.LogError(ex, "Kafka consume reply_commands/send_retry error");
                    await Task.Delay(3000, stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Reply command processing failed");

                    if (command != null)
                    {
                        command.Status = "failed";
                        command.LastError = ex.Message;

                        if (command.RetryCount <= 0)
                        {
                            command.RetryCount = 1;
                        }

                        await _kafkaProducer.PublishSendFailedAsync(
                            command,
                            stoppingToken);

                        _logger.LogWarning(
                            "Reply command published to send_failed. CommandId: {CommandId}, RetryCount: {RetryCount}",
                            command.CommandId,
                            command.RetryCount);
                    }

                    if (message != null)
                    {
                        consumer.Commit(message);
                    }

                    await Task.Delay(3000, stoppingToken);
                }
            }

            consumer.Close();

            _logger.LogInformation("ReplyCommandsConsumerHostedService stopped");
        }
    }
}