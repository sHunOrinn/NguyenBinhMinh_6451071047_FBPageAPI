using System.Text.Json;
using Microsoft.Extensions.Logging;
using NguyenBinhMinh_FBPageAPI.Models;

namespace NguyenBinhMinh_FBPageAPI.Services
{
    public class FacebookEventNormalizer
    {
        private readonly ILogger<FacebookEventNormalizer> _logger;

        public FacebookEventNormalizer(ILogger<FacebookEventNormalizer> logger)
        {
            _logger = logger;
        }

        public List<NormalizedEvent> Normalize(string rawJson)
        {
            var results = new List<NormalizedEvent>();

            using var doc = JsonDocument.Parse(rawJson);
            var root = doc.RootElement;

            // 1. Payload test mẫu từ Meta: { "sample": {...} }
            if (root.TryGetProperty("sample", out var sample))
            {
                var field = GetString(sample, "field");

                if ((field == "feed" || field == "group_feed") &&
                    sample.TryGetProperty("value", out var value))
                {
                    var normalized = BuildNormalizedEventFromElement(
                        field,
                        value,
                        pageIdFallback: GetString(GetPropertyOrDefault(value, "from"), "id"),
                        defaultCreatedAt: DateTime.UtcNow,
                        rawJson: sample.GetRawText());

                    if (normalized != null)
                    {
                        results.Add(normalized);
                    }
                }

                return results;
            }

            // 2. Payload thật từ Facebook: { "object": "page", "entry": [...] }
            if (!root.TryGetProperty("entry", out var entries) ||
                entries.ValueKind != JsonValueKind.Array)
            {
                return results;
            }

            foreach (var entry in entries.EnumerateArray())
            {
                var pageId = GetString(entry, "id");

                var entryTime = entry.TryGetProperty("time", out var timeEl) &&
                                TryGetInt64(timeEl, out var unixTime)
                    ? ParseUnixTime(unixTime)
                    : DateTime.UtcNow;

                // Nhánh changes[]: payload Page feed/comment thật
                if (entry.TryGetProperty("changes", out var changes) &&
                    changes.ValueKind == JsonValueKind.Array)
                {
                    foreach (var change in changes.EnumerateArray())
                    {
                        var field = GetString(change, "field");

                        if ((field == "feed" || field == "group_feed") &&
                            change.TryGetProperty("value", out var value))
                        {
                            var normalized = BuildNormalizedEventFromElement(
                                field,
                                value,
                                pageIdFallback: pageId,
                                defaultCreatedAt: entryTime,
                                rawJson: change.GetRawText());

                            if (normalized != null)
                            {
                                results.Add(normalized);
                            }
                        }
                    }
                }

                // Nhánh messaging[] nếu có payload dạng khác
                if (entry.TryGetProperty("messaging", out var messaging) &&
                    messaging.ValueKind == JsonValueKind.Array)
                {
                    foreach (var msg in messaging.EnumerateArray())
                    {
                        var field = GetString(msg, "field");

                        if (field == "feed" || field == "group_feed")
                        {
                            var normalized = BuildNormalizedEventFromElement(
                                field,
                                msg,
                                pageIdFallback: pageId,
                                defaultCreatedAt: entryTime,
                                rawJson: msg.GetRawText());

                            if (normalized != null)
                            {
                                results.Add(normalized);
                            }
                        }
                    }
                }
            }

            return results;
        }

        private NormalizedEvent? BuildNormalizedEventFromElement(
            string field,
            JsonElement element,
            string? pageIdFallback,
            DateTime defaultCreatedAt,
            string rawJson)
        {
            var postId = GetString(element, "post_id");
            var commentId = GetString(element, "comment_id");
            var message = GetString(element, "message");

            var item = GetString(element, "item");
            var verb = GetString(element, "verb");

            if (string.IsNullOrWhiteSpace(item) &&
                !string.IsNullOrWhiteSpace(commentId))
            {
                item = "comment";
            }

            if (string.IsNullOrWhiteSpace(verb) &&
                (!string.IsNullOrWhiteSpace(message) ||
                 !string.IsNullOrWhiteSpace(commentId)))
            {
                verb = "add";
            }

            if (item != "comment" || verb != "add")
            {
                return null;
            }

            var fromEl = GetPropertyOrDefault(element, "from");
            var actorId = GetString(fromEl, "id");
            var actorName = GetString(fromEl, "name");

            var pageId = !string.IsNullOrWhiteSpace(pageIdFallback)
                ? pageIdFallback
                : string.Empty;

            // QUAN TRỌNG:
            // Nếu comment do chính Page tạo ra thì bỏ qua.
            // Nếu không bỏ qua, Page sẽ tự reply vào reply của chính nó.
            if (!string.IsNullOrWhiteSpace(pageId) &&
                !string.IsNullOrWhiteSpace(actorId) &&
                actorId == pageId)
            {
                _logger.LogWarning(
                    "Skip page self comment. PageId: {PageId}, CommentId: {CommentId}, ActorName: {ActorName}",
                    pageId,
                    commentId,
                    actorName);

                return null;
            }

            if (string.IsNullOrWhiteSpace(commentId))
            {
                _logger.LogInformation("Skip event because comment_id is empty");
                return null;
            }

            if (string.IsNullOrWhiteSpace(message))
            {
                _logger.LogInformation(
                    "Skip empty comment. CommentId: {CommentId}",
                    commentId);

                return null;
            }

            DateTime createdAt = defaultCreatedAt;

            if (element.TryGetProperty("created_time", out var createdTimeEl) &&
                TryGetInt64(createdTimeEl, out var createdUnix))
            {
                createdAt = ParseUnixTime(createdUnix);
            }

            return new NormalizedEvent
            {
                EventId = commentId,
                Source = "facebook",
                EventType = $"{field}.{item}.{verb}",
                PageId = pageId,
                PostId = postId,
                CommentId = commentId,
                ActorId = actorId,
                ActorName = actorName,
                Message = message,
                CreatedAtUtc = createdAt,
                RawJson = rawJson
            };
        }

        private static DateTime ParseUnixTime(long unixTime)
        {
            if (unixTime > 9999999999)
            {
                return DateTimeOffset.FromUnixTimeMilliseconds(unixTime).UtcDateTime;
            }

            return DateTimeOffset.FromUnixTimeSeconds(unixTime).UtcDateTime;
        }

        private static string? GetString(JsonElement element, string propertyName)
        {
            if (element.ValueKind == JsonValueKind.Undefined ||
                element.ValueKind == JsonValueKind.Null)
            {
                return null;
            }

            if (!element.TryGetProperty(propertyName, out var prop))
            {
                return null;
            }

            return prop.ValueKind switch
            {
                JsonValueKind.String => prop.GetString(),
                JsonValueKind.Number => prop.ToString(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                _ => null
            };
        }

        private static JsonElement GetPropertyOrDefault(
            JsonElement element,
            string propertyName)
        {
            if (element.ValueKind != JsonValueKind.Object)
            {
                return default;
            }

            return element.TryGetProperty(propertyName, out var prop)
                ? prop
                : default;
        }

        private static bool TryGetInt64(JsonElement element, out long value)
        {
            value = 0;

            if (element.ValueKind == JsonValueKind.Number)
            {
                return element.TryGetInt64(out value);
            }

            if (element.ValueKind == JsonValueKind.String)
            {
                return long.TryParse(element.GetString(), out value);
            }

            return false;
        }
    }
}