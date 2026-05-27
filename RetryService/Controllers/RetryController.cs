using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using RetryServices.Models;
using RetryServices.Services;

namespace RetryService.Controllers
{
    [ApiController]
    [Route("api/retry")]
    public class RetryController : ControllerBase
    {
        private readonly KafkaOptions _kafkaOptions;
        private readonly RetryMetricsService _metricsService;

        public RetryController(
            IOptions<KafkaOptions> kafkaOptions,
            RetryMetricsService metricsService)
        {
            _kafkaOptions = kafkaOptions.Value;
            _metricsService = metricsService;
        }

        [HttpGet("status")]
        public IActionResult GetStatus()
        {
            var metrics = _metricsService.GetMetrics();

            return Ok(new
            {
                service = "retry-service",
                port = 3003,

                consumes = new[]
                {
                    _kafkaOptions.TopicSendFailed
                },

                publishes = new[]
                {
                    _kafkaOptions.TopicSendRetry,
                    _kafkaOptions.TopicDeadLetter
                },

                backoff = new
                {
                    strategy = "exponential-backoff",
                    maxRetry = 3
                },

                counters = new
                {
                    processedCount = metrics.ProcessedCount,
                    retriedCount = metrics.RetriedCount,
                    deadLetterCount = metrics.DeadLetterCount,
                    failedCount = metrics.FailedCount
                },

                last = new
                {
                    lastCommandId = metrics.LastCommandId,
                    lastAction = metrics.LastAction,
                    lastRetryCount = metrics.LastRetryCount,
                    lastError = metrics.LastError,
                    lastProcessedAtUtc = metrics.LastProcessedAtUtc
                },

                status = "running",
                checkedAtUtc = DateTime.UtcNow
            });
        }
    }
}