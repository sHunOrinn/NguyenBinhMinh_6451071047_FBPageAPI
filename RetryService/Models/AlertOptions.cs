namespace RetryServices.Models
{
    public class AlertOptions
    {
        public bool EnableEmail { get; set; }
        public bool EnableSlack { get; set; }

        public string SmtpHost { get; set; } = "smtp.gmail.com";
        public int SmtpPort { get; set; } = 587;
        public string SmtpUser { get; set; } = string.Empty;
        public string SmtpPassword { get; set; } = string.Empty;
        public string FromEmail { get; set; } = string.Empty;
        public string ToEmail { get; set; } = string.Empty;

        public string SlackWebhookUrl { get; set; } = string.Empty;
    }
}