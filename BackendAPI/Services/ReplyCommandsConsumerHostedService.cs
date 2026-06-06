using System.Text.Json;
using BackendAPI.Models;
using Confluent.Kafka;
using Microsoft.Extensions.Options;

namespace BackendAPI.Services
{
    public class ReplyCommandsConsumerHostedService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly KafkaOptions _options;
        private readonly KafkaProducerService _kafkaProducer;
        private readonly SupabaseIdempotencyService _idempotencyService;
        private readonly CommandLogService _commandLogService;
        private readonly ILogger<ReplyCommandsConsumerHostedService> _logger;

        public ReplyCommandsConsumerHostedService(
            IServiceScopeFactory scopeFactory,
            IOptions<KafkaOptions> options,
            KafkaProducerService kafkaProducer,
            SupabaseIdempotencyService idempotencyService,
            CommandLogService commandLogService,
            ILogger<ReplyCommandsConsumerHostedService> logger)
        {
            _scopeFactory = scopeFactory;
            _options = options.Value;
            _kafkaProducer = kafkaProducer;
            _idempotencyService = idempotencyService;
            _commandLogService = commandLogService;
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

            consumer.Subscribe(new[]
            {
                _options.TopicReplyCommands,
                _options.TopicSendRetry
            });

            _logger.LogInformation(
                "Backend consumer started. Topics: {Topic1}, {Topic2}, GroupId: {GroupId}",
                _options.TopicReplyCommands,
                _options.TopicSendRetry,
                _options.ConsumerGroupId);

            while (!stoppingToken.IsCancellationRequested)
            {
                ConsumeResult<Ignore, string>? message = null;
                ReplyCommand? command = null;
                bool facebookActionSucceeded = false;

                try
                {
                    message = consumer.Consume(stoppingToken);

                    if (message?.Message?.Value == null)
                    {
                        continue;
                    }

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

                    if (string.IsNullOrWhiteSpace(command.CommentId))
                    {
                        _logger.LogWarning(
                            "Invalid command. Missing CommentId. CommandId: {CommandId}",
                            command.CommandId);

                        consumer.Commit(message);
                        continue;
                    }

                    var canProcess = await _idempotencyService.TryStartAsync(
                        command,
                        stoppingToken);

                    if (!canProcess)
                    {
                        _logger.LogWarning(
                            "Duplicate command ignored. EventId: {EventId}, CommentId: {CommentId}, Action: {Action}",
                            command.EventId,
                            command.CommentId,
                            command.Action);

                        //await _commandLogService.SaveAsync(
                        //    command,
                        //    "duplicate_ignored",
                        //    null,
                        //    stoppingToken);

                        consumer.Commit(message);
                        continue;
                    }

                    using var scope = _scopeFactory.CreateScope();

                    var facebookService = scope.ServiceProvider
                        .GetRequiredService<FacebookCommentActionService>();

                    var action = command.Action?.Trim().ToLowerInvariant();

                    if (action == "reply")
                    {
                        if (string.IsNullOrWhiteSpace(command.ReplyText))
                        {
                            throw new Exception("ReplyText is empty");
                        }

                        await facebookService.ReplyCommentAsync(
                            command.CommentId,
                            command.ReplyText,
                            stoppingToken);
                        facebookActionSucceeded = true;
                    }
                    //else if (action == "delete_comment" || action == "delete")
                    //{
                    //    await facebookService.DeleteCommentAsync(
                    //        command.CommentId,
                    //        stoppingToken);
                    //}
                    else if (action == "hide_comment" || action == "hide")
                    {
                        await facebookService.HideCommentAsync(
                            command.CommentId,
                            stoppingToken);
                        //facebookActionSucceeded = true;
                    }
                    else
                    {
                        _logger.LogInformation(
                            "Skip command. CommandId: {CommandId}, Action: {Action}",
                            command.CommandId,
                            command.Action);

                        await _idempotencyService.RemoveAsync(
                            command,
                            stoppingToken);

                        await _commandLogService.SaveAsync(
                            command,
                            "skipped",
                            null,
                            stoppingToken);

                        consumer.Commit(message);
                        continue;
                    }

                    command.Status = "processed";
                    command.LastError = null;

                    await _idempotencyService.MarkProcessedAsync(
                        command,
                        stoppingToken);

                    await _commandLogService.SaveAsync(
                        command,
                        "processed",
                        null,
                        stoppingToken);

                    consumer.Commit(message);

                    _logger.LogInformation(
                        "Command processed successfully. CommandId: {CommandId}, CommentId: {CommentId}, Action: {Action}, RetryCount: {RetryCount}",
                        command.CommandId,
                        command.CommentId,
                        command.Action,
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
                        command.LastError = ex.Message;

                        if (facebookActionSucceeded)
                        {
                            // Facebook đã xử lý thành công rồi.
                            // Không được publish send_failed nữa, nếu không sẽ retry và reply trùng.
                            command.Status = "processed_but_log_failed";

                            await _commandLogService.SaveAsync(
                                command,
                                "processed_but_log_failed",
                                ex.Message,
                                stoppingToken);

                            if (message != null)
                            {
                                consumer.Commit(message);
                            }

                            continue;
                        }

                        command.Status = "failed";

                        await _idempotencyService.RemoveAsync(
                            command,
                            stoppingToken);

                        await _commandLogService.SaveAsync(
                            command,
                            "failed",
                            ex.Message,
                            stoppingToken);

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