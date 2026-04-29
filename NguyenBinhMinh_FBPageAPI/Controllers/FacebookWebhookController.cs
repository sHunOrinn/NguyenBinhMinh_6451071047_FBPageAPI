using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using NguyenBinhMinh_FBPageAPI.Models;
using NguyenBinhMinh_FBPageAPI.Services;
using System.Text;

namespace NguyenBinhMinh_FBPageAPI.Controllers
{
    [ApiController]
    [Route("webhook")]
    public class FacebookWebhookController : ControllerBase
    {
        private readonly FacebookEventNormalizer _normalizer;
        private readonly KafkaProducerService _kafkaProducer;
        private readonly ILogger<FacebookWebhookController> _logger;

        public FacebookWebhookController(
            FacebookEventNormalizer normalizer,
            KafkaProducerService kafkaProducer,
            ILogger<FacebookWebhookController> logger)
        {
            _normalizer = normalizer;
            _kafkaProducer = kafkaProducer;
            _logger = logger;
        }

        [HttpGet]
        public IActionResult VerifyWebhook(
            [FromQuery(Name = "hub.mode")] string? mode,
            [FromQuery(Name = "hub.verify_token")] string? verifyToken,
            [FromQuery(Name = "hub.challenge")] string? challenge)
        {
            if (mode == "subscribe" && verifyToken == "my_verify_token")
            {
                return Content(challenge ?? "");
            }

            return StatusCode(403, "Invalid verify token");
        }

        [HttpPost]
        public async Task<IActionResult> ReceiveWebhook(CancellationToken cancellationToken)
        {
            Request.EnableBuffering();

            string rawBody;
            using (var reader = new StreamReader(Request.Body, Encoding.UTF8, leaveOpen: true))
            {
                rawBody = await reader.ReadToEndAsync(cancellationToken);
                Request.Body.Position = 0;
            }

            _logger.LogInformation("=== WEBHOOK HIT ===");
            _logger.LogInformation("Raw webhook payload: {Payload}", rawBody);

            var normalizedEvents = _normalizer.Normalize(rawBody);
            _logger.LogInformation("Normalized events count: {Count}", normalizedEvents.Count);

            foreach (var evt in normalizedEvents)
            {
                _logger.LogInformation("Publishing event: {EventType}", evt.EventType);
                await _kafkaProducer.PublishRawEventAsync(evt, cancellationToken);
            }

            return Ok(new
            {
                message = "Webhook received",
                published = normalizedEvents.Count
            });
        }
    }
}