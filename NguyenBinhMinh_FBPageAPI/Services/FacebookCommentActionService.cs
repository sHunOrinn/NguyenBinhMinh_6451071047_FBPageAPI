using System.Text.Json;
using Microsoft.Extensions.Options;
using NguyenBinhMinh_FBPageAPI.Models;

namespace NguyenBinhMinh_FBPageAPI.Services
{
    public class FacebookCommentActionService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly FacebookOptions _facebookOptions;
        private readonly ILogger<FacebookCommentActionService> _logger;

        public FacebookCommentActionService(
            IHttpClientFactory httpClientFactory,
            IOptions<FacebookOptions> facebookOptions,
            ILogger<FacebookCommentActionService> logger)
        {
            _httpClientFactory = httpClientFactory;
            _facebookOptions = facebookOptions.Value;
            _logger = logger;
        }

        private string BaseUrl => $"https://graph.facebook.com/{_facebookOptions.GraphVersion}";

        public async Task ReplyCommentAsync(string commentId, string message, CancellationToken cancellationToken)
        {
            var client = _httpClientFactory.CreateClient();

            var formData = new Dictionary<string, string>
            {
                ["message"] = message,
                ["access_token"] = _facebookOptions.PageAccessToken
            };

            var response = await client.PostAsync(
                $"{BaseUrl}/{commentId}/comments",
                new FormUrlEncodedContent(formData),
                cancellationToken);

            await EnsureSuccess(response, "reply comment");
        }

        public async Task HideCommentAsync(string commentId, CancellationToken cancellationToken)
        {
            var client = _httpClientFactory.CreateClient();

            var formData = new Dictionary<string, string>
            {
                ["is_hidden"] = "true",
                ["access_token"] = _facebookOptions.PageAccessToken
            };

            var response = await client.PostAsync(
                $"{BaseUrl}/{commentId}",
                new FormUrlEncodedContent(formData),
                cancellationToken);

            await EnsureSuccess(response, "hide comment");
        }

        public async Task BlockUserAsync(string pageId, string userId, CancellationToken cancellationToken)
        {
            var client = _httpClientFactory.CreateClient();

            var formData = new Dictionary<string, string>
            {
                ["user"] = userId,
                ["access_token"] = _facebookOptions.PageAccessToken
            };

            var response = await client.PostAsync(
                $"{BaseUrl}/{pageId}/blocked",
                new FormUrlEncodedContent(formData),
                cancellationToken);

            await EnsureSuccess(response, "block user");
        }

        private async Task EnsureSuccess(HttpResponseMessage response, string action)
        {
            var body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Facebook action failed: {Action}. Response: {Body}", action, body);
                throw new Exception($"Facebook action failed: {action}. Response: {body}");
            }
        }
    }
}