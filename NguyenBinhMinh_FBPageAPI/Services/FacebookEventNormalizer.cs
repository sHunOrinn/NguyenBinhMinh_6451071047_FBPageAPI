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

            if (!root.TryGetProperty("entry", out var entries) || entries.ValueKind != JsonValueKind.Array)
                return results;

            foreach (var entry in entries.EnumerateArray())
            {
                var pageId = entry.TryGetProperty("id", out var pageIdEl) ? pageIdEl.GetString() ?? "" : "";
                var entryTime = entry.TryGetProperty("time", out var timeEl) && timeEl.TryGetInt64(out var unixTime)
                    ? DateTimeOffset.FromUnixTimeSeconds(unixTime).UtcDateTime
                    : DateTime.UtcNow;

                if (!entry.TryGetProperty("changes", out var changes) || changes.ValueKind != JsonValueKind.Array)
                    continue;

                foreach (var change in changes.EnumerateArray())
                {
                    var field = change.TryGetProperty("field", out var fieldEl) ? fieldEl.GetString() ?? "" : "";
                    if (!change.TryGetProperty("value", out var value))
                        continue;

                    if (field == "feed")
                    {
                        var item = value.TryGetProperty("item", out var itemEl) ? itemEl.GetString() ?? "" : "";
                        var verb = value.TryGetProperty("verb", out var verbEl) ? verbEl.GetString() ?? "" : "";

                        if (item == "comment" && verb == "add")
                        {
                            var commentId = value.TryGetProperty("comment_id", out var commentIdEl) ? commentIdEl.GetString() : null;
                            var postId = value.TryGetProperty("post_id", out var postIdEl) ? postIdEl.GetString() : null;
                            var message = value.TryGetProperty("message", out var messageEl) ? messageEl.GetString() : null;

                            string? actorId = null;
                            string? actorName = null;

                            if (value.TryGetProperty("from", out var fromEl))
                            {
                                actorId = fromEl.TryGetProperty("id", out var actorIdEl) ? actorIdEl.GetString() : null;
                                actorName = fromEl.TryGetProperty("name", out var actorNameEl) ? actorNameEl.GetString() : null;
                            }

                            results.Add(new NormalizedEvent
                            {
                                EventId = commentId ?? Guid.NewGuid().ToString("N"),
                                EventType = "comment.created",
                                PageId = pageId,
                                PostId = postId,
                                CommentId = commentId,
                                ActorId = actorId,
                                ActorName = actorName,
                                Message = message,
                                CreatedAtUtc = entryTime,
                                RawJson = change.GetRawText()
                            });
                        }
                    }
                }
            }

            return results;
        }
    }
}