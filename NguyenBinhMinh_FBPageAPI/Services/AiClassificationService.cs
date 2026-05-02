using NguyenBinhMinh_FBPageAPI.Models;

namespace NguyenBinhMinh_FBPageAPI.Services
{
    public class AiClassificationService
    {
        public Task<AiClassificationResult> ClassifyAsync(NormalizedEvent evt)
        {
            var message = (evt.Message ?? "").ToLower();

            if (message.Contains("giá") || message.Contains("bao nhiêu") || message.Contains("price"))
            {
                return Task.FromResult(new AiClassificationResult
                {
                    Intent = "hoi_gia",
                    Sentiment = "neutral",
                    ReplyMessage = "Shop cảm ơn bạn đã quan tâm. Bạn vui lòng inbox để shop tư vấn giá chi tiết nhé."
                });
            }

            if (message.Contains("chưa nhận") || message.Contains("lỗi") || message.Contains("khiếu nại") || message.Contains("tệ"))
            {
                return Task.FromResult(new AiClassificationResult
                {
                    Intent = "khieu_nai_ho_tro",
                    Sentiment = "negative",
                    ReplyMessage = "Shop xin lỗi vì trải nghiệm chưa tốt. Bạn vui lòng inbox mã đơn để shop kiểm tra ngay nhé."
                });
            }

            if (message.Contains("hay quá") || message.Contains("tốt") || message.Contains("đẹp") || message.Contains("ưng"))
            {
                return Task.FromResult(new AiClassificationResult
                {
                    Intent = "khen",
                    Sentiment = "positive",
                    ReplyMessage = "Shop cảm ơn bạn rất nhiều ạ."
                });
            }

            return Task.FromResult(new AiClassificationResult
            {
                Intent = "unknown",
                Sentiment = "neutral",
                ReplyMessage = "Shop cảm ơn bạn đã bình luận. Shop sẽ phản hồi bạn sớm nhất nhé."
            });
        }
    }
}