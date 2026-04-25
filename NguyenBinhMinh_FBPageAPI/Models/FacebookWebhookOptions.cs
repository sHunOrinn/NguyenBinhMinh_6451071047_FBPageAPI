namespace NguyenBinhMinh_FBPageAPI.Models
{
    public class FacebookWebhookOptions
    {
        public string VerifyToken { get; set; } = "my_verify_token";
        public string AppSecret { get; set; } = string.Empty;
    }
}