using NguyenBinhMinh_FBPageAPI.Models;

namespace NguyenBinhMinh_FBPageAPI.Services
{
    public class CoreEventProcessorService
    {
        private readonly SpamDetectionService _spamDetectionService;
        private readonly AiClassificationService _aiClassificationService;
        private readonly EventDecisionService _eventDecisionService;
        private readonly FacebookCommentActionService _facebookActionService;
        private readonly EventStateStore _eventStateStore;
        private readonly KafkaProducerService _kafkaProducer;
        private readonly ILogger<CoreEventProcessorService> _logger;

        public CoreEventProcessorService(
            SpamDetectionService spamDetectionService,
            AiClassificationService aiClassificationService,
            EventDecisionService eventDecisionService,
            FacebookCommentActionService facebookActionService,
            EventStateStore eventStateStore,
            KafkaProducerService kafkaProducer,
            ILogger<CoreEventProcessorService> logger)
        {
            _spamDetectionService = spamDetectionService;
            _aiClassificationService = aiClassificationService;
            _eventDecisionService = eventDecisionService;
            _facebookActionService = facebookActionService;
            _eventStateStore = eventStateStore;
            _kafkaProducer = kafkaProducer;
            _logger = logger;
        }

        public async Task ProcessAsync(NormalizedEvent evt, CancellationToken cancellationToken)
        {
            var result = new EventProcessResult
            {
                EventId = evt.EventId,
                Status = "received"
            };

            _eventStateStore.Save(result);

            try
            {
                var spam = _spamDetectionService.Detect(evt);
                var ai = await _aiClassificationService.ClassifyAsync(evt);
                result = _eventDecisionService.Decide(evt, spam, ai);

                if (result.ShouldHideComment && !string.IsNullOrWhiteSpace(evt.CommentId))
                {
                    await _facebookActionService.HideCommentAsync(evt.CommentId, cancellationToken);
                }

                if (result.ShouldReply &&
                    !string.IsNullOrWhiteSpace(evt.CommentId) &&
                    !string.IsNullOrWhiteSpace(result.ReplyMessage))
                {
                    await _facebookActionService.ReplyCommentAsync(
                        evt.CommentId,
                        result.ReplyMessage,
                        cancellationToken);
                }

                if (result.ShouldBlockUser &&
                    !string.IsNullOrWhiteSpace(evt.PageId) &&
                    !string.IsNullOrWhiteSpace(evt.ActorId))
                {
                    await _facebookActionService.BlockUserAsync(
                        evt.PageId,
                        evt.ActorId,
                        cancellationToken);
                }

                _eventStateStore.Save(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Process event failed: {EventId}", evt.EventId);

                result.Status = "failed";
                result.ErrorMessage = ex.Message;
                _eventStateStore.Save(result);

                evt.Status = "failed";
                evt.LastError = ex.Message;
                evt.RetryCount++;

                await _kafkaProducer.PublishSendFailedAsync(evt, cancellationToken);
            }
        }
    }
}