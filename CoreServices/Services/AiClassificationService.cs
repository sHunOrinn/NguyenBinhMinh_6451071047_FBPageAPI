using System.Text;
using System.Text.Json;
using CoreServices.Models;
using Microsoft.Extensions.Options;

namespace CoreServices.Services
{
    public class AiClassificationService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly GeminiOptions _options;
        private readonly ILogger<AiClassificationService> _logger;

        public AiClassificationService(
            IHttpClientFactory httpClientFactory,
            IOptions<GeminiOptions> options,
            ILogger<AiClassificationService> logger)
        {
            _httpClientFactory = httpClientFactory;
            _options = options.Value;
            _logger = logger;
        }

        public async Task<AiClassificationResult> ClassifyAsync(
            NormalizedEvent evt,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(_options.ApiKey))
            {
                _logger.LogWarning("Gemini API key is empty. Use fallback classification.");
                return Fallback(evt);
            }

            try
            {
                var client = _httpClientFactory.CreateClient();

                var url =
                    $"https://generativelanguage.googleapis.com/v1beta/models/{_options.Model}:generateContent?key={Uri.EscapeDataString(_options.ApiKey)}";

                var prompt = BuildPrompt(evt.Message);

                var requestBody = new
                {
                    contents = new[]
                    {
                        new
                        {
                            role = "user",
                            parts = new[]
                            {
                                new { text = prompt }
                            }
                        }
                    },
                    generationConfig = new
                    {
                        temperature = 0.2,
                        responseMimeType = "application/json"
                    }
                };

                var json = JsonSerializer.Serialize(requestBody);

                using var content = new StringContent(
                    json,
                    Encoding.UTF8,
                    "application/json");

                var response = await client.PostAsync(
                    url,
                    content,
                    cancellationToken);

                var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError(
                        "Gemini API error. StatusCode: {StatusCode}, Body: {Body}",
                        response.StatusCode,
                        responseBody);

                    return Fallback(evt);
                }

                var resultText = ExtractGeminiText(responseBody);

                if (string.IsNullOrWhiteSpace(resultText))
                {
                    _logger.LogWarning("Gemini response text is empty. Body: {Body}", responseBody);
                    return Fallback(evt);
                }

                var result = JsonSerializer.Deserialize<AiClassificationResult>(
                    resultText,
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                if (result == null)
                {
                    return Fallback(evt);
                }

                result.Intent = NormalizeIntent(result.Intent);
                result.Sentiment = NormalizeSentiment(result.Sentiment);
                result.UsedAi = true;

                if (string.IsNullOrWhiteSpace(result.ReplyMessage) &&
                    !result.IsSpam &&
                    !result.IsInappropriate)
                {
                    result.ReplyMessage = BuildDefaultReply(result.Intent);
                }

                _logger.LogInformation(
                    "Gemini classified comment. CommentId: {CommentId}, Intent: {Intent}, Sentiment: {Sentiment}, IsSpam: {IsSpam}, IsInappropriate: {IsInappropriate}",
                    evt.CommentId,
                    result.Intent,
                    result.Sentiment,
                    result.IsSpam,
                    result.IsInappropriate);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Gemini classification failed. Use fallback.");
                return Fallback(evt);
            }
        }

        private static string BuildPrompt(string? message)
        {
            return $$"""
            Bạn là hệ thống AI quản lý bình luận Facebook Page cho một shop bán hàng.

            Hãy phân tích bình luận sau và trả về JSON thuần, không markdown, không giải thích.

            Bình luận:
            "{{message}}"

            Quy tắc:
            - intent chỉ được chọn một trong: hoi_gia, khieu_nai_ho_tro, khen, spam, unknown
            - sentiment chỉ được chọn một trong: positive, neutral, negative
            - isSpam = true nếu bình luận quảng cáo, lặp lại, chứa link đáng ngờ, mời gọi click link, cá cược, vay tiền, lừa đảo
            - isInappropriate = true nếu bình luận chửi tục, xúc phạm, thô tục, khiêu dâm, công kích cá nhân
            - Nếu isSpam hoặc isInappropriate là true thì replyMessage để chuỗi rỗng
            - Nếu bình luận bình thường thì tạo replyMessage ngắn gọn, lịch sự, tự nhiên bằng tiếng Việt
            - Không hứa thông tin cụ thể nếu bình luận không đủ dữ liệu
            - Nếu hỏi giá thì hướng khách inbox để tư vấn chi tiết
            - Nếu khiếu nại thì xin lỗi và đề nghị khách inbox mã đơn/số điện thoại
            - Nếu khen thì cảm ơn khách
            - Nếu unknown thì replyMessage có thể để rỗng

            JSON format:
            {
              "intent": "hoi_gia",
              "sentiment": "neutral",
              "isSpam": false,
              "isInappropriate": false,
              "replyMessage": "Shop cảm ơn bạn đã quan tâm. Bạn vui lòng inbox để shop tư vấn giá chi tiết nhé.",
              "reason": "..."
            }
            """;
        }

        private static string ExtractGeminiText(string responseBody)
        {
            using var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;

            if (!root.TryGetProperty("candidates", out var candidates) ||
                candidates.ValueKind != JsonValueKind.Array ||
                candidates.GetArrayLength() == 0)
            {
                return string.Empty;
            }

            var first = candidates[0];

            if (!first.TryGetProperty("content", out var content))
            {
                return string.Empty;
            }

            if (!content.TryGetProperty("parts", out var parts) ||
                parts.ValueKind != JsonValueKind.Array ||
                parts.GetArrayLength() == 0)
            {
                return string.Empty;
            }

            if (!parts[0].TryGetProperty("text", out var text))
            {
                return string.Empty;
            }

            return text.GetString() ?? string.Empty;
        }

        private AiClassificationResult Fallback(NormalizedEvent evt)
        {
            var text = evt.Message?.ToLowerInvariant() ?? string.Empty;

            if (text.Contains("giá") || text.Contains("bao nhiêu") || text.Contains("bn"))
            {
                return new AiClassificationResult
                {
                    Intent = "hoi_gia",
                    Sentiment = "neutral",
                    ReplyMessage = "Shop cảm ơn bạn đã quan tâm. Bạn vui lòng inbox để shop tư vấn giá chi tiết nhé.",
                    Reason = "fallback_price",
                    UsedAi = false
                };
            }

            if (text.Contains("lỗi") || text.Contains("chưa nhận") || text.Contains("khiếu nại"))
            {
                return new AiClassificationResult
                {
                    Intent = "khieu_nai_ho_tro",
                    Sentiment = "negative",
                    ReplyMessage = "Shop xin lỗi vì trải nghiệm chưa tốt. Bạn vui lòng inbox mã đơn để shop kiểm tra ngay nhé.",
                    Reason = "fallback_support",
                    UsedAi = false
                };
            }

            if (text.Contains("đẹp") || text.Contains("tốt") || text.Contains("ưng"))
            {
                return new AiClassificationResult
                {
                    Intent = "khen",
                    Sentiment = "positive",
                    ReplyMessage = "Shop cảm ơn bạn rất nhiều ạ.",
                    Reason = "fallback_praise",
                    UsedAi = false
                };
            }

            return new AiClassificationResult
            {
                Intent = "unknown",
                Sentiment = "neutral",
                ReplyMessage = string.Empty,
                Reason = "fallback_unknown",
                UsedAi = false
            };
        }

        private static string NormalizeIntent(string? intent)
        {
            return intent switch
            {
                "hoi_gia" => "hoi_gia",
                "khieu_nai_ho_tro" => "khieu_nai_ho_tro",
                "khen" => "khen",
                "spam" => "spam",
                _ => "unknown"
            };
        }

        private static string NormalizeSentiment(string? sentiment)
        {
            return sentiment switch
            {
                "positive" => "positive",
                "negative" => "negative",
                _ => "neutral"
            };
        }

        private static string BuildDefaultReply(string intent)
        {
            return intent switch
            {
                "hoi_gia" =>
                    "Shop cảm ơn bạn đã quan tâm. Bạn vui lòng inbox để shop tư vấn giá chi tiết nhé.",

                "khieu_nai_ho_tro" =>
                    "Shop xin lỗi vì trải nghiệm chưa tốt. Bạn vui lòng inbox mã đơn để shop kiểm tra ngay nhé.",

                "khen" =>
                    "Shop cảm ơn bạn rất nhiều ạ.",

                _ => string.Empty
            };
        }
    }
}