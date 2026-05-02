using NguyenBinhMinh_FBPageAPI.Models;
using System.Text.RegularExpressions;

namespace NguyenBinhMinh_FBPageAPI.Services
{
    public class SpamDetectionService
    {
        private readonly Dictionary<string, List<DateTime>> _userCommentHistory = new();

        public SpamDetectionResult Detect(NormalizedEvent evt)
        {
            var message = evt.Message ?? "";

            if (string.IsNullOrWhiteSpace(message))
            {
                return new SpamDetectionResult();
            }

            var linkCount = Regex.Matches(message, @"https?://|www\.").Count;

            if (linkCount >= 2)
            {
                return new SpamDetectionResult
                {
                    IsSpam = true,
                    SpamLevel = "heavy",
                    Reason = "Comment chứa nhiều liên kết"
                };
            }

            if (IsRepeatedUser(evt))
            {
                return new SpamDetectionResult
                {
                    IsSpam = true,
                    SpamLevel = "repeat",
                    Reason = "Người dùng spam lặp lại nhiều lần trong 24 giờ"
                };
            }

            if (message.Length > 20 && HasRepeatedContent(message))
            {
                return new SpamDetectionResult
                {
                    IsSpam = true,
                    SpamLevel = "light",
                    Reason = "Comment lặp nội dung nhiều lần"
                };
            }

            return new SpamDetectionResult();
        }

        private bool IsRepeatedUser(NormalizedEvent evt)
        {
            if (string.IsNullOrWhiteSpace(evt.ActorId))
                return false;

            var now = DateTime.UtcNow;

            if (!_userCommentHistory.ContainsKey(evt.ActorId))
            {
                _userCommentHistory[evt.ActorId] = new List<DateTime>();
            }

            _userCommentHistory[evt.ActorId].Add(now);

            _userCommentHistory[evt.ActorId] = _userCommentHistory[evt.ActorId]
                .Where(x => x >= now.AddHours(-24))
                .ToList();

            return _userCommentHistory[evt.ActorId].Count >= 3;
        }

        private bool HasRepeatedContent(string message)
        {
            var words = message
                .ToLower()
                .Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (words.Length < 6)
                return false;

            return words.GroupBy(x => x).Any(g => g.Count() >= 4);
        }
    }
}