using System.Net;
using System.Net.Mail;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using RetryServices.Models;

namespace RetryServices.Services
{
    public class AlertService
    {
        private readonly AlertOptions _options;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<AlertService> _logger;

        public AlertService(
            IOptions<AlertOptions> options,
            IHttpClientFactory httpClientFactory,
            ILogger<AlertService> logger)
        {
            _options = options.Value;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public async Task SendDeadLetterAlertAsync(
            ReplyCommand command,
            CancellationToken cancellationToken = default)
        {
            var subject = "[ALERT] Message moved to dead_letter";

            var body = $"""
            Một command đã bị chuyển vào dead_letter sau khi retry thất bại.

            CommandId: {command.CommandId}
            EventId: {command.EventId}
            CommentId: {command.CommentId}
            Action: {command.Action}
            RetryCount: {command.RetryCount}
            Status: {command.Status}
            LastError: {command.LastError}
            Time UTC: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}
            """;

            if (_options.EnableEmail)
            {
                await SendEmailAsync(subject, body, cancellationToken);
            }

            if (_options.EnableSlack)
            {
                await SendSlackAsync(body, cancellationToken);
            }
        }

        private async Task SendEmailAsync(
            string subject,
            string body,
            CancellationToken cancellationToken)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(_options.SmtpUser) ||
                    string.IsNullOrWhiteSpace(_options.SmtpPassword) ||
                    string.IsNullOrWhiteSpace(_options.FromEmail) ||
                    string.IsNullOrWhiteSpace(_options.ToEmail))
                {
                    _logger.LogWarning("Email alert skipped because SMTP config is missing");
                    return;
                }

                using var smtpClient = new SmtpClient(_options.SmtpHost, _options.SmtpPort)
                {
                    EnableSsl = true,
                    Credentials = new NetworkCredential(
                        _options.SmtpUser,
                        _options.SmtpPassword)
                };

                using var mail = new MailMessage
                {
                    From = new MailAddress(_options.FromEmail),
                    Subject = subject,
                    Body = body,
                    IsBodyHtml = false
                };

                mail.To.Add(_options.ToEmail);

                await smtpClient.SendMailAsync(mail, cancellationToken);

                _logger.LogInformation(
                    "Dead letter email alert sent to {ToEmail}",
                    _options.ToEmail);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Send email alert failed but ignored");
            }
        }

        private async Task SendSlackAsync(
            string message,
            CancellationToken cancellationToken)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(_options.SlackWebhookUrl))
                {
                    _logger.LogWarning("Slack alert skipped because webhook url is missing");
                    return;
                }

                var client = _httpClientFactory.CreateClient();

                var payload = new
                {
                    text = $"🚨 *Dead Letter Alert*\n```{message}```"
                };

                var json = JsonSerializer.Serialize(payload);

                using var content = new StringContent(
                    json,
                    Encoding.UTF8,
                    "application/json");

                var response = await client.PostAsync(
                    _options.SlackWebhookUrl,
                    content,
                    cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

                    _logger.LogError(
                        "Send Slack alert failed. StatusCode: {StatusCode}, Body: {Body}",
                        response.StatusCode,
                        responseBody);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Send Slack alert failed but ignored");
            }
        }
    }
}