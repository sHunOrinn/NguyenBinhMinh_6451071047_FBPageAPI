using CoreServices.Models;

namespace CoreServices.Services
{
    public class EventDecisionService
    {
        public EventProcessResult Decide(
            NormalizedEvent evt,
            SpamDetectionResult spam,
            AiClassificationResult ai)
        {
            var result = new EventProcessResult
            {
                EventId = evt.EventId,
                CommentId = evt.CommentId,
                PostId = evt.PostId,
                PageId = evt.PageId,
                ActorId = evt.ActorId,
                Intent = ai.Intent,
                Sentiment = ai.Sentiment,
                Status = "processed",
                ProcessedAtUtc = DateTime.UtcNow
            };

            if (spam.IsSpam ||
                spam.IsInappropriate ||
                ai.IsSpam ||
                ai.IsInappropriate ||
                ai.Intent == "spam")
            {
                result.IsSpam = true;
                result.SpamLevel = "high";
                result.SpamReason = !string.IsNullOrWhiteSpace(spam.Reason)
                    ? spam.Reason
                    : ai.Reason;

                result.ShouldHideComment = true;
                result.ShouldReply = false;
                result.ShouldReviewManually = false;
                result.ReplyMessage = null;
                result.DecisionReason = "delete_spam_or_inappropriate_comment";

                return result;
            }

            if (!string.IsNullOrWhiteSpace(ai.ReplyMessage))
            {
                result.ShouldReply = true;
                result.ShouldHideComment = false;
                result.ShouldReviewManually = false;
                result.ReplyMessage = ai.ReplyMessage;
                result.DecisionReason = "ai_reply";

                return result;
            }

            result.ShouldReply = false;
            result.ShouldHideComment = false;
            result.ShouldReviewManually = false;
            result.DecisionReason = "no_action";

            return result;
        }
    }
}