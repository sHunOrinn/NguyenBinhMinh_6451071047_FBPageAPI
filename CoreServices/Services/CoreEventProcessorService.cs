using CoreServices.Models;

namespace CoreServices.Services
{
    public class CoreEventProcessorService
    {
        private readonly SpamDetectionService _spamDetectionService;
        private readonly AiClassificationService _aiClassificationService;
        private readonly EventDecisionService _eventDecisionService;
        private readonly KafkaProducerService _kafkaProducer;
        private readonly EventStateStore _eventStateStore;
        private readonly ILogger<CoreEventProcessorService> _logger;

        public CoreEventProcessorService(
            SpamDetectionService spamDetectionService,
            AiClassificationService aiClassificationService,
            EventDecisionService eventDecisionService,
            KafkaProducerService kafkaProducer,
            EventStateStore eventStateStore,
            ILogger<CoreEventProcessorService> logger)
        {
            _spamDetectionService = spamDetectionService;
            _aiClassificationService = aiClassificationService;
            _eventDecisionService = eventDecisionService;
            _kafkaProducer = kafkaProducer;
            _eventStateStore = eventStateStore;
            _logger = logger;
        }

        public async Task ProcessAsync(
            NormalizedEvent evt,
            CancellationToken cancellationToken)
        {
            try
            {
                if (evt == null)
                {
                    _logger.LogWarning("Skip null event");
                    return;
                }

                // Chặn vòng lặp: Page reply xong Facebook lại bắn webhook về.
                // Nếu ActorId chính là PageId thì đây là comment của Page, không xử lý nữa.
                if (!string.IsNullOrWhiteSpace(evt.PageId) &&
                    !string.IsNullOrWhiteSpace(evt.ActorId) &&
                    evt.ActorId == evt.PageId)
                {
                    _logger.LogWarning(
                        "Skip page self event. EventId: {EventId}, CommentId: {CommentId}, ActorId: {ActorId}, PageId: {PageId}",
                        evt.EventId,
                        evt.CommentId,
                        evt.ActorId,
                        evt.PageId);

                    return;
                }

                if (string.IsNullOrWhiteSpace(evt.CommentId))
                {
                    _logger.LogWarning(
                        "Skip event because CommentId is empty. EventId: {EventId}",
                        evt.EventId);

                    return;
                }

                if (string.IsNullOrWhiteSpace(evt.EventId))
                {
                    evt.EventId = $"{evt.PageId}_{evt.CommentId}";
                }

                var spam = _spamDetectionService.Detect(evt);
                var ai = await _aiClassificationService.ClassifyAsync(evt);

                var decision = _eventDecisionService.Decide(evt, spam, ai);

                _eventStateStore.Save(decision);

                string action;
                string? replyText = null;

                if (decision.ShouldHideComment)
                {
                    action = "hide_comment";
                }
                else if (decision.ShouldReviewManually)
                {
                    action = "pending_review";
                }
                else if (decision.ShouldReply)
                {
                    action = "reply";
                    replyText = decision.ReplyMessage;
                }
                else
                {
                    action = "none";
                }

                // Không cần gửi command nếu không có hành động thực tế
                if (action == "none" || action == "pending_review")
                {
                    _logger.LogInformation(
                        "No action required. EventId: {EventId}, CommentId: {CommentId}, intent={Intent}, sentiment={Sentiment}",
                        evt.EventId,
                        evt.CommentId,
                        decision.Intent,
                        decision.Sentiment);

                    return;
                }

                var command = new ReplyCommand
                {
                    CommandId = $"{evt.EventId}:{evt.CommentId}:{action}",
                    EventId = evt.EventId,
                    Action = action,
                    PageId = evt.PageId,
                    PostId = evt.PostId,
                    CommentId = evt.CommentId,
                    UserId = evt.ActorId,
                    Intent = decision.Intent,
                    Sentiment = decision.Sentiment,
                    ReplyText = replyText,
                    RetryCount = 0,
                    Status = "processed",
                    LastError = null,
                    CreatedAtUtc = DateTime.UtcNow
                };

                await _kafkaProducer.PublishReplyCommandAsync(
                    command,
                    cancellationToken);

                _logger.LogInformation(
                    "Processed event {EventId}, commandId={CommandId}, action={Action}, intent={Intent}, sentiment={Sentiment}",
                    evt.EventId,
                    command.CommandId,
                    command.Action,
                    command.Intent,
                    command.Sentiment);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Core service failed to process event {EventId}",
                    evt?.EventId);

                var failed = new EventProcessResult
                {
                    EventId = evt?.EventId ?? string.Empty,
                    Status = "failed",
                    ErrorMessage = ex.Message,
                    ProcessedAtUtc = DateTime.UtcNow
                };

                _eventStateStore.Save(failed);
            }
        }
    }
}