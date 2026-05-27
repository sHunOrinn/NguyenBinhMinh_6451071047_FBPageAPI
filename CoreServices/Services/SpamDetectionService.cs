using CoreServices.Models;
using System.Text.RegularExpressions;

namespace CoreServices.Services
{
    public class SpamDetectionService
    {
        private readonly ILogger<SpamDetectionService> _logger;

        private static readonly string[] BadWords =
        {
            "đm", "dm", "địt", "dit", "cc", "cặc", "cac",
            "lồn", "lon", "ngu", "chó", "cho", "mẹ mày",
            "mẹ m", "clm", "vcl", "vl", "đéo", "deo"
        };

        private static readonly string[] SpamKeywords =
        {
            "kiếm tiền nhanh",
            "nhận quà ngay",
            "trúng thưởng",
            "click vào link",
            "vào web",
            "casino",
            "cá cược",
            "tài xỉu",
            "cho vay",
            "web sex",
            "xxx"
        };

        public SpamDetectionService(ILogger<SpamDetectionService> logger)
        {
            _logger = logger;
        }

        public SpamDetectionResult Detect(NormalizedEvent evt)
        {
            var message = evt.Message?.Trim() ?? string.Empty;
            var lower = message.ToLowerInvariant();

            var result = new SpamDetectionResult
            {
                IsSpam = false,
                IsInappropriate = false,
                Score = 0,
                Reason = "clean"
            };

            if (string.IsNullOrWhiteSpace(lower))
            {
                result.Score += 1;
                result.Reason = "empty_message";
                return result;
            }

            if (ContainsUrl(lower))
            {
                result.IsSpam = true;
                result.Score += 5;
                result.Reason = "contains_url";
            }

            if (ContainsRepeatedText(lower))
            {
                result.IsSpam = true;
                result.Score += 4;
                result.Reason = "repeated_content";
            }

            if (SpamKeywords.Any(k => lower.Contains(k)))
            {
                result.IsSpam = true;
                result.Score += 5;
                result.Reason = "spam_keyword";
            }

            if (BadWords.Any(w => lower.Contains(w)))
            {
                result.IsInappropriate = true;
                result.Score += 8;
                result.Reason = "inappropriate_content";
            }

            if (IsMostlySymbols(lower))
            {
                result.IsSpam = true;
                result.Score += 3;
                result.Reason = "too_many_symbols";
            }

            if (result.IsSpam || result.IsInappropriate)
            {
                _logger.LogWarning(
                    "Spam/Inappropriate detected. CommentId: {CommentId}, ActorId: {ActorId}, Reason: {Reason}, Score: {Score}, Message: {Message}",
                    evt.CommentId,
                    evt.ActorId,
                    result.Reason,
                    result.Score,
                    evt.Message);
            }

            return result;
        }

        private static bool ContainsUrl(string text)
        {
            return Regex.IsMatch(
                text,
                @"(http:\/\/|https:\/\/|www\.|\.com|\.net|\.org|\.vn|bit\.ly|t\.me|zalo\.me)",
                RegexOptions.IgnoreCase);
        }

        private static bool ContainsRepeatedText(string text)
        {
            var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (words.Length < 4)
                return false;

            var repeated = words
                .GroupBy(w => w)
                .Any(g => g.Count() >= 4);

            return repeated;
        }

        private static bool IsMostlySymbols(string text)
        {
            if (text.Length < 10)
                return false;

            var symbolCount = text.Count(c => !char.IsLetterOrDigit(c) && !char.IsWhiteSpace(c));

            return symbolCount >= text.Length * 0.5;
        }
    }
}