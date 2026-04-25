using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using NguyenBinhMinh_FBPageAPI.Models;

namespace NguyenBinhMinh_FBPageAPI.Services
{
    public class FacebookSignatureService
    {
        private readonly FacebookWebhookOptions _options;

        public FacebookSignatureService(IOptions<FacebookWebhookOptions> options)
        {
            _options = options.Value;
        }

        public bool Verify(string rawBody, string? signatureHeader)
        {
            if (string.IsNullOrWhiteSpace(_options.AppSecret))
                return false;

            if (string.IsNullOrWhiteSpace(signatureHeader))
                return false;

            const string prefix = "sha256=";
            if (!signatureHeader.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return false;

            var received = signatureHeader.Substring(prefix.Length);

            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_options.AppSecret));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(rawBody));
            var expected = Convert.ToHexString(hash).ToLowerInvariant();

            return CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(received),
                Encoding.UTF8.GetBytes(expected));
        }
    }
}