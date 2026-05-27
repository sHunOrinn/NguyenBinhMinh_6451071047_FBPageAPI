using System.Collections.Concurrent;
using BackendAPI.Models;

namespace BackendAPI.Services
{
    public class InMemoryIdempotencyService
    {
        private readonly ConcurrentDictionary<string, string> _keys = new();

        public string BuildKey(ReplyCommand command)
        {
            return $"{command.EventId}:{command.CommentId}:{command.Action}";
        }

        public bool TryStart(ReplyCommand command)
        {
            var key = BuildKey(command);

            // Khóa ngay trước khi gọi Facebook API
            // Nếu key đã tồn tại thì không xử lý nữa để tránh reply trùng
            return _keys.TryAdd(key, "processing");
        }

        public void MarkProcessed(ReplyCommand command)
        {
            var key = BuildKey(command);
            _keys[key] = "processed";
        }

        public void Remove(ReplyCommand command)
        {
            var key = BuildKey(command);
            _keys.TryRemove(key, out _);
        }
    }
}