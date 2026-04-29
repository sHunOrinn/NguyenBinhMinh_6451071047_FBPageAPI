using System.Text.Json;
using NguyenBinhMinh_FBPageAPI.Models;

namespace NguyenBinhMinh_FBPageAPI.Services
{
    public class FacebookEventNormalizer
    {
        public List<NormalizedEvent> Normalize(string rawJson)
        {
            var results = new List<NormalizedEvent>();

            using var doc = JsonDocument.Parse(rawJson);
            var root = doc.RootElement;

            // 1) Payload test mẫu từ Meta: { "sample": {...} }
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

            // 2) Payload thật từ Facebook: { "object": "...", "entry": [...] }
            if (!root.TryGetProperty("entry", out var entries) || entries.ValueKind != JsonValueKind.Array)
            {
                return results;
            }

            foreach (var entry in entries.EnumerateArray())
            {
                var entryId = GetString(entry, "id");
                var entryTime = entry.TryGetProperty("time", out var timeEl) && TryGetInt64(timeEl, out var unixTime)
                    ? ParseUnixTime(unixTime)
                    : DateTime.UtcNow;

                // 2a) Nhánh changes[]
                if (entry.TryGetProperty("changes", out var changes) && changes.ValueKind == JsonValueKind.Array)
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
                                pageIdFallback: entryId,
                                defaultCreatedAt: entryTime,
                                rawJson: change.GetRawText());

                            if (normalized != null)
                            {
                                results.Add(normalized);
                            }
                        }
                    }
                }

                // 2b) Nhánh messaging[] — payload thực tế bạn đang nhận
                if (entry.TryGetProperty("messaging", out var messaging) && messaging.ValueKind == JsonValueKind.Array)
                {
                    foreach (var msg in messaging.EnumerateArray())
                    {
                        var field = GetString(msg, "field");

                        if (field == "feed" || field == "group_feed")
                        {
                            var normalized = BuildNormalizedEventFromElement(
                                field,
                                msg,
                                pageIdFallback: entryId,
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

        private static NormalizedEvent? BuildNormalizedEventFromElement(
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

            if (string.IsNullOrWhiteSpace(item) && !string.IsNullOrWhiteSpace(commentId))
            {
                item = "comment";
            }

            if (string.IsNullOrWhiteSpace(verb) &&
                (!string.IsNullOrWhiteSpace(message) || !string.IsNullOrWhiteSpace(commentId)))
            {
                verb = "add";
            }

            if (string.IsNullOrWhiteSpace(item) || string.IsNullOrWhiteSpace(verb))
            {
                return null;
            }

            if (verb != "add")
            {
                return null;
            }

            var fromEl = GetPropertyOrDefault(element, "from");
            var actorId = GetString(fromEl, "id");
            var actorName = GetString(fromEl, "name");

            DateTime createdAt = defaultCreatedAt;
            if (element.TryGetProperty("created_time", out var createdTimeEl) &&
                TryGetInt64(createdTimeEl, out var createdUnix))
            {
                createdAt = ParseUnixTime(createdUnix);
            }

            var eventId =
                commentId ??
                postId ??
                Guid.NewGuid().ToString("N");

            var pageId =
                pageIdFallback ??
                actorId ??
                string.Empty;

            return new NormalizedEvent
            {
                EventId = eventId,
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
            // Nếu lớn hơn 10 chữ số thì là milliseconds
            if (unixTime > 9999999999)
            {
                return DateTimeOffset.FromUnixTimeMilliseconds(unixTime).UtcDateTime;
            }

            return DateTimeOffset.FromUnixTimeSeconds(unixTime).UtcDateTime;
        }

        private static string? GetString(JsonElement element, string propertyName)
        {
            if (element.ValueKind == JsonValueKind.Undefined || element.ValueKind == JsonValueKind.Null)
            {
                return null;
            }

            if (element.TryGetProperty(propertyName, out var prop))
            {
                return prop.ValueKind switch
                {
                    JsonValueKind.String => prop.GetString(),
                    JsonValueKind.Number => prop.ToString(),
                    JsonValueKind.True => "true",
                    JsonValueKind.False => "false",
                    _ => null
                };
            }

            return null;
        }

        private static JsonElement GetPropertyOrDefault(JsonElement element, string propertyName)
        {
            if (element.ValueKind != JsonValueKind.Object)
            {
                return default;
            }

            return element.TryGetProperty(propertyName, out var prop) ? prop : default;
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