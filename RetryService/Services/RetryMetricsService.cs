namespace RetryServices.Services
{
    public class RetryMetricsService
    {
        private long _processedCount;
        private long _retriedCount;
        private long _deadLetterCount;
        private long _failedCount;

        private readonly object _lock = new();

        public string? LastCommandId { get; private set; }
        public string? LastAction { get; private set; }
        public int LastRetryCount { get; private set; }
        public string? LastError { get; private set; }
        public DateTime? LastProcessedAtUtc { get; private set; }

        public void MarkProcessed(
            string? commandId,
            string? action,
            int retryCount)
        {
            Interlocked.Increment(ref _processedCount);

            lock (_lock)
            {
                LastCommandId = commandId;
                LastAction = action;
                LastRetryCount = retryCount;
                LastError = null;
                LastProcessedAtUtc = DateTime.UtcNow;
            }
        }

        public void MarkRetried(
            string? commandId,
            string? action,
            int retryCount)
        {
            Interlocked.Increment(ref _retriedCount);

            lock (_lock)
            {
                LastCommandId = commandId;
                LastAction = action;
                LastRetryCount = retryCount;
                LastError = null;
                LastProcessedAtUtc = DateTime.UtcNow;
            }
        }

        public void MarkDeadLetter(
            string? commandId,
            string? action,
            int retryCount,
            string? error)
        {
            Interlocked.Increment(ref _deadLetterCount);

            lock (_lock)
            {
                LastCommandId = commandId;
                LastAction = action;
                LastRetryCount = retryCount;
                LastError = error;
                LastProcessedAtUtc = DateTime.UtcNow;
            }
        }

        public void MarkFailed(
            string? commandId,
            string? action,
            int retryCount,
            string? error)
        {
            Interlocked.Increment(ref _failedCount);

            lock (_lock)
            {
                LastCommandId = commandId;
                LastAction = action;
                LastRetryCount = retryCount;
                LastError = error;
                LastProcessedAtUtc = DateTime.UtcNow;
            }
        }

        public RetryMetricsSnapshot GetMetrics()
        {
            lock (_lock)
            {
                return new RetryMetricsSnapshot
                {
                    ProcessedCount = Interlocked.Read(ref _processedCount),
                    RetriedCount = Interlocked.Read(ref _retriedCount),
                    DeadLetterCount = Interlocked.Read(ref _deadLetterCount),
                    FailedCount = Interlocked.Read(ref _failedCount),
                    LastCommandId = LastCommandId,
                    LastAction = LastAction,
                    LastRetryCount = LastRetryCount,
                    LastError = LastError,
                    LastProcessedAtUtc = LastProcessedAtUtc
                };
            }
        }
    }

    public class RetryMetricsSnapshot
    {
        public long ProcessedCount { get; set; }
        public long RetriedCount { get; set; }
        public long DeadLetterCount { get; set; }
        public long FailedCount { get; set; }

        public string? LastCommandId { get; set; }
        public string? LastAction { get; set; }
        public int LastRetryCount { get; set; }
        public string? LastError { get; set; }
        public DateTime? LastProcessedAtUtc { get; set; }
    }
}