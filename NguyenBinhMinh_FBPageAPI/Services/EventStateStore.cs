using NguyenBinhMinh_FBPageAPI.Models;

namespace NguyenBinhMinh_FBPageAPI.Services
{
    public class EventStateStore
    {
        private readonly Dictionary<string, EventProcessResult> _states = new();

        public void Save(EventProcessResult result)
        {
            _states[result.EventId] = result;
        }

        public EventProcessResult? Get(string eventId)
        {
            return _states.TryGetValue(eventId, out var result) ? result : null;
        }

        public List<EventProcessResult> GetAll()
        {
            return _states.Values
                .OrderByDescending(x => x.ProcessedAtUtc)
                .ToList();
        }
    }
}