using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text;

namespace RPlus.Hunter.API.Waba;

/// <summary>
/// Validates HMAC-SHA256 signature on incoming 360dialog/Meta webhooks.
/// 
/// Security: Without this, anyone can POST fake messages to our webhook endpoint
/// and inject phantom "I'm interested!" messages into admin panel.
///
/// Meta signs every webhook request with X-Hub-Signature-256 header:
///   X-Hub-Signature-256: sha256={HMAC-SHA256(payload, app_secret)}
/// 
/// We verify this using our Waba:ClientSecret.
/// </summary>
public sealed class WabaSignatureValidator
{
    private readonly byte[] _secretBytes;
    private readonly ILogger<WabaSignatureValidator> _logger;

    public WabaSignatureValidator(
        IOptions<WabaOptions> options,
        ILogger<WabaSignatureValidator> logger)
    {
        _secretBytes = Encoding.UTF8.GetBytes(options.Value.AppSecret);
        _logger = logger;
    }

    /// <summary>
    /// Validates the X-Hub-Signature-256 header against request body.
    /// </summary>
    /// <param name="signatureHeader">Value of X-Hub-Signature-256 header (e.g., "sha256=abc123...").</param>
    /// <param name="body">Raw request body bytes.</param>
    /// <returns>True if signature is valid.</returns>
    public bool Validate(string? signatureHeader, byte[] body)
    {
        if (string.IsNullOrEmpty(signatureHeader))
        {
            _logger.LogWarning("Webhook request missing X-Hub-Signature-256 header");
            return false;
        }

        if (_secretBytes.Length == 0)
        {
            // If no secret configured, skip validation (dev mode)
            _logger.LogWarning("Waba:ClientSecret not configured — webhook signature validation DISABLED (dangerous in production!)");
            return true;
        }

        // Header format: "sha256=<hex>"
        const string prefix = "sha256=";
        if (!signatureHeader.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Invalid signature header format: {Header}", signatureHeader[..Math.Min(signatureHeader.Length, 30)]);
            return false;
        }

        var expectedHex = signatureHeader[prefix.Length..];

        using var hmac = new HMACSHA256(_secretBytes);
        var computedHash = hmac.ComputeHash(body);
        var computedHex = Convert.ToHexStringLower(computedHash);

        // Constant-time comparison to prevent timing attacks
        var isValid = CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(computedHex),
            Encoding.ASCII.GetBytes(expectedHex.ToLowerInvariant())
        );

        if (!isValid)
        {
            _logger.LogWarning("Webhook HMAC-SHA256 signature mismatch. Expected: {Expected}, Got: {Got}",
                expectedHex[..Math.Min(expectedHex.Length, 10)] + "...",
                computedHex[..10] + "...");
        }

        return isValid;
    }
}
