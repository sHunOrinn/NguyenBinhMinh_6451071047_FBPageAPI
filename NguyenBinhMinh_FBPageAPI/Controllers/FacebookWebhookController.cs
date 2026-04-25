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
        private readonly FacebookWebhookOptions _options;
        private readonly FacebookSignatureService _signatureService;
        private readonly FacebookEventNormalizer _normalizer;
        private readonly KafkaProducerService _kafkaProducer;
        private readonly ILogger<FacebookWebhookController> _logger;

        public FacebookWebhookController(
            IOptions<FacebookWebhookOptions> options,
            FacebookSignatureService signatureService,
            FacebookEventNormalizer normalizer,
            KafkaProducerService kafkaProducer,
            ILogger<FacebookWebhookController> logger)
        {
            _options = options.Value;
            _signatureService = signatureService;
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
            if (mode == "subscribe" && verifyToken == _options.VerifyToken)
            {
                return Content(challenge ?? "");
            }

            return Forbid();
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

            var signatureHeader = Request.Headers["X-Hub-Signature-256"].FirstOrDefault();

            if (!_signatureService.Verify(rawBody, signatureHeader))
            {
                return Unauthorized(new { message = "Invalid signature" });
            }

            var normalizedEvents = _normalizer.Normalize(rawBody);

            foreach (var evt in normalizedEvents)
            {
                await _kafkaProducer.PublishRawEventAsync(evt, cancellationToken);
            }

            _logger.LogInformation("Webhook received. Published {Count} event(s) to Kafka.", normalizedEvents.Count);

            return Ok(new
            {
                message = "Webhook received",
                published = normalizedEvents.Count
            });
        }
    }
}