using BackendAPI.Models;
using Microsoft.Extensions.Options;

namespace BackendAPI.Services
{
    public class FacebookCommentActionService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly FacebookOptions _options;
        private readonly ILogger<FacebookCommentActionService> _logger;

        public FacebookCommentActionService(
            IHttpClientFactory httpClientFactory,
            IOptions<FacebookOptions> options,
            ILogger<FacebookCommentActionService> logger)
        {
            _httpClientFactory = httpClientFactory;
            _options = options.Value;
            _logger = logger;
        }

        public async Task ReplyCommentAsync(
            string commentId,
            string message,
            CancellationToken cancellationToken = default)
        {
            var client = _httpClientFactory.CreateClient();

            var url =
                $"https://graph.facebook.com/{_options.GraphVersion}/{commentId}/comments";

            var formData = new Dictionary<string, string>
            {
                ["message"] = message,
                ["access_token"] = _options.PageAccessToken
            };

            var response = await client.PostAsync(
                url,
                new FormUrlEncodedContent(formData),
                cancellationToken);

            await EnsureSuccess(response, "reply comment", cancellationToken);

            _logger.LogInformation(
                "Reply comment successfully. CommentId: {CommentId}",
                commentId);
        }

        //public async Task HideCommentAsync(
        //    string commentId,
        //    CancellationToken cancellationToken = default)
        //{
        //    var client = _httpClientFactory.CreateClient();

        //    var url =
        //        $"https://graph.facebook.com/{_options.GraphVersion}/{commentId}";

        //    var formData = new Dictionary<string, string>
        //    {
        //        ["is_hidden"] = "true",
        //        ["access_token"] = _options.PageAccessToken
        //    };

        //    var response = await client.PostAsync(
        //        url,
        //        new FormUrlEncodedContent(formData),
        //        cancellationToken);

        //    await EnsureSuccess(response, "hide comment", cancellationToken);

        //    _logger.LogInformation(
        //        "Hide comment successfully. CommentId: {CommentId}",
        //        commentId);
        //}

        private static async Task EnsureSuccess(
            HttpResponseMessage response,
            string action,
            CancellationToken cancellationToken)
        {
            var content = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception(
                    $"Facebook action failed: {action}. Response: {content}");
            }
        }

        public async Task HideCommentAsync(
        string commentId,
        CancellationToken cancellationToken = default)
        {
            var client = _httpClientFactory.CreateClient();

            var url =
                $"https://graph.facebook.com/{_options.GraphVersion}/{commentId}";

            var formData = new Dictionary<string, string>
            {
                ["is_hidden"] = "true",
                ["access_token"] = _options.PageAccessToken
            };

            var response = await client.PostAsync(
                url,
                new FormUrlEncodedContent(formData),
                cancellationToken);

            await EnsureSuccess(response, "hide comment", cancellationToken);

            _logger.LogInformation(
                "Hide comment successfully. CommentId: {CommentId}",
                commentId);
        }
    }
}