using NguyenBinhMinh_FBPageAPI.Models;

namespace NguyenBinhMinh_FBPageAPI.Services
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

        public async Task ProcessAsync(NormalizedEvent evt, CancellationToken cancellationToken)
        {
            try
            {
                var spam = _spamDetectionService.Detect(evt);
                var ai = await _aiClassificationService.ClassifyAsync(evt);

                var decision = _eventDecisionService.Decide(evt, spam, ai);
                _eventStateStore.Save(decision);

                var command = new ReplyCommand
                {
                    EventId = evt.EventId,
                    PageId = evt.PageId,
                    PostId = evt.PostId,
                    CommentId = evt.CommentId,
                    UserId = evt.ActorId,
                    Intent = decision.Intent,
                    Sentiment = decision.Sentiment,
                    ReplyText = decision.ReplyMessage,
                    Status = "processed"
                };

                if (decision.ShouldHideComment)
                {
                    command.Action = "hide_comment";
                    command.ReplyText = null;
                }
                else if (decision.ShouldReviewManually)
                {
                    command.Action = "pending_review";
                    command.ReplyText = null;
                }
                else if (decision.ShouldReply)
                {
                    command.Action = "reply";
                }
                else
                {
                    command.Action = "none";
                    command.ReplyText = null;
                }

                await _kafkaProducer.PublishReplyCommandAsync(command, cancellationToken);

                _logger.LogInformation(
                    "Processed event {EventId}, action={Action}, intent={Intent}, sentiment={Sentiment}",
                    evt.EventId,
                    command.Action,
                    command.Intent,
                    command.Sentiment);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Core service failed to process event {EventId}", evt.EventId);

                var failed = new EventProcessResult
                {
                    EventId = evt.EventId,
                    Status = "failed",
                    ErrorMessage = ex.Message
                };

                _eventStateStore.Save(failed);
            }
        }
    }
}