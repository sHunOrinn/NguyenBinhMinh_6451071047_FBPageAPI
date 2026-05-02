using NguyenBinhMinh_FBPageAPI.Models;

namespace NguyenBinhMinh_FBPageAPI.Services
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
                IsSpam = spam.IsSpam,
                SpamLevel = spam.SpamLevel,
                SpamReason = spam.Reason,
                Intent = ai.Intent,
                Sentiment = ai.Sentiment,
                ReplyMessage = ai.ReplyMessage,
                ProcessedAtUtc = DateTime.UtcNow
            };

            if (spam.IsSpam && spam.SpamLevel == "light")
            {
                result.Status = "processed";
                result.ShouldHideComment = true;
                result.ShouldReply = false;
                return result;
            }

            if (spam.IsSpam && spam.SpamLevel == "repeat")
            {
                result.Status = "processed";
                result.ShouldBlacklistUser = true;
                result.ShouldReply = false;
                return result;
            }

            if (spam.IsSpam && spam.SpamLevel == "heavy")
            {
                result.Status = "processed";
                result.ShouldHideComment = true;
                result.ShouldReviewManually = true;
                result.ShouldReply = false;
                return result;
            }

            if (ai.Intent == "khieu_nai_ho_tro" && ai.Sentiment == "negative")
            {
                result.Status = "processed";
                result.ShouldReviewManually = true;
                result.ShouldReply = true;
                return result;
            }

            result.Status = "processed";
            result.ShouldReply = true;
            return result;
        }
    }
}