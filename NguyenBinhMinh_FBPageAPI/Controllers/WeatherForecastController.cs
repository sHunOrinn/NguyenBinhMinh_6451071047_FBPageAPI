using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using NguyenBinhMinh_FBPageAPI.Models;

namespace NguyenBinhMinh_FBPageAPI.Controllers
{
    [ApiController]
    [Route("api/page")]
    public class PageController : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly FacebookOptions _facebookOptions;

        public PageController(
            IHttpClientFactory httpClientFactory,
            IOptions<FacebookOptions> facebookOptions)
        {
            _httpClientFactory = httpClientFactory;
            _facebookOptions = facebookOptions.Value;
        }

        private string BaseUrl => $"https://graph.facebook.com/{_facebookOptions.GraphVersion}";

        [HttpGet("{pageId}")]
        public async Task<IActionResult> GetPageInfo(string pageId)
        {
            try
            {
                var client = _httpClientFactory.CreateClient();

                var url =
                    $"{BaseUrl}/{pageId}" +
                    $"?fields=id,name,about,category,fan_count,followers_count" +
                    $"&access_token={Uri.EscapeDataString(_facebookOptions.PageAccessToken)}";

                var response = await client.GetAsync(url);
                var content = await response.Content.ReadAsStringAsync();

                return Content(content, "application/json");
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    message = "Failed to get page info",
                    error = ex.Message
                });
            }
        }

        [HttpGet("{pageId}/posts")]
        public async Task<IActionResult> GetPosts(string pageId)
        {
            try
            {
                var client = _httpClientFactory.CreateClient();

                var url =
                    $"{BaseUrl}/{pageId}/posts" +
                    $"?access_token={Uri.EscapeDataString(_facebookOptions.PageAccessToken)}";

                var response = await client.GetAsync(url);
                var content = await response.Content.ReadAsStringAsync();

                return Content(content, "application/json");
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    message = "Failed to get posts",
                    error = ex.Message
                });
            }
        }

        [HttpPost("{pageId}/posts")]
        public async Task<IActionResult> CreatePost(string pageId, [FromBody] CreatePostRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Message))
            {
                return BadRequest(new
                {
                    message = "Message is required"
                });
            }

            try
            {
                var client = _httpClientFactory.CreateClient();

                var url = $"{BaseUrl}/{pageId}/feed";

                var formData = new Dictionary<string, string>
                {
                    ["message"] = request.Message,
                    ["access_token"] = _facebookOptions.PageAccessToken
                };

                var content = new FormUrlEncodedContent(formData);
                var response = await client.PostAsync(url, content);
                var responseBody = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    return StatusCode((int)response.StatusCode, new
                    {
                        message = "Facebook API returned an error",
                        facebookResponse = TryParseJson(responseBody)
                    });
                }

                return Content(responseBody, "application/json");
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    message = "Failed to create post",
                    error = ex.Message
                });
            }
        }

        [HttpDelete("post/{postId}")]
        public async Task<IActionResult> DeletePost(string postId)
        {
            try
            {
                var client = _httpClientFactory.CreateClient();

                var url =
                    $"{BaseUrl}/{postId}" +
                    $"?access_token={Uri.EscapeDataString(_facebookOptions.PageAccessToken)}";

                var request = new HttpRequestMessage(HttpMethod.Delete, url);
                var response = await client.SendAsync(request);
                var content = await response.Content.ReadAsStringAsync();

                return Content(content, "application/json");
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    message = "Failed to delete post",
                    error = ex.Message
                });
            }
        }

        [HttpGet("{pageId}/insights")]
        public async Task<IActionResult> GetInsights(
        string pageId,
        [FromQuery] int days = 7,
        [FromQuery] string metric = "page_post_engagements")
        {
            try
            {
                if (days <= 0 || days > 365)
                {
                    return BadRequest(new
                    {
                        message = "days must be between 1 and 365"
                    });
                }

                var client = _httpClientFactory.CreateClient();

                var until = DateTimeOffset.UtcNow;
                var since = until.AddDays(-days);

                var url =
                    $"{BaseUrl}/{pageId}/insights" +
                    $"?metric={Uri.EscapeDataString(metric)}" +
                    $"&since={since.ToUnixTimeSeconds()}" +
                    $"&until={until.ToUnixTimeSeconds()}" +
                    $"&access_token={Uri.EscapeDataString(_facebookOptions.PageAccessToken)}";

                var response = await client.GetAsync(url);
                var content = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    return StatusCode((int)response.StatusCode, new
                    {
                        message = "Facebook API returned an error",
                        facebookResponse = TryParseJson(content)
                    });
                }

                return Content(content, "application/json");
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    message = "Failed to get insights",
                    error = ex.Message
                });
            }
        }

        [HttpGet("post/{postId}/comments")]
        public async Task<IActionResult> GetComments(string postId)
        {
            try
            {
                var client = _httpClientFactory.CreateClient();

                var url =
                    $"{BaseUrl}/{postId}/comments" +
                    $"?access_token={Uri.EscapeDataString(_facebookOptions.PageAccessToken)}";

                var response = await client.GetAsync(url);
                var content = await response.Content.ReadAsStringAsync();

                return Content(content, "application/json");
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    message = "Failed to get comments",
                    error = ex.Message
                });
            }
        }

        [HttpGet("post/{postId}/likes")]
        public async Task<IActionResult> GetLikes(string postId)
        {
            try
            {
                var client = _httpClientFactory.CreateClient();

                var url =
                    $"{BaseUrl}/{postId}/likes" +
                    $"?access_token={Uri.EscapeDataString(_facebookOptions.PageAccessToken)}";

                var response = await client.GetAsync(url);
                var content = await response.Content.ReadAsStringAsync();

                return Content(content, "application/json");
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    message = "Failed to get likes",
                    error = ex.Message
                });
            }
        }

        private static object TryParseJson(string raw)
        {
            try
            {
                using var doc = JsonDocument.Parse(raw);
                return JsonSerializer.Deserialize<object>(doc.RootElement.GetRawText()) ?? raw;
            }
            catch
            {
                return raw;
            }
        }
    }
}