using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RPlus.Kernel.Integration.Application;

namespace RPlus.Kernel.Integration.Infrastructure.Security;

/// <summary>
/// HMAC signature verification attribute for partner API endpoints.
/// Validates X-Signature and X-Signature-Timestamp headers.
/// Provides replay attack protection (±5 minute window).
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public class PartnerSignatureAuthAttribute : Attribute, IAsyncActionFilter
{
    private static readonly TimeSpan MaxTimestampDrift = TimeSpan.FromMinutes(5);

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var request = context.HttpContext.Request;
        var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<PartnerSignatureAuthAttribute>>();

        // ========== Step 1: Check Required Headers ==========
        if (!request.Headers.TryGetValue("X-Signature", out var signatureHeader) ||
            string.IsNullOrEmpty(signatureHeader))
        {
            logger.LogWarning("Missing X-Signature header");
            context.Result = new UnauthorizedObjectResult(new { error = "missing_signature" });
            return;
        }

        if (!request.Headers.TryGetValue("X-Signature-Timestamp", out var timestampHeader) ||
            string.IsNullOrEmpty(timestampHeader))
        {
            logger.LogWarning("Missing X-Signature-Timestamp header");
            context.Result = new UnauthorizedObjectResult(new { error = "missing_timestamp" });
            return;
        }

        if (!request.Headers.TryGetValue("X-Integration-Key", out var integrationKey) ||
            string.IsNullOrEmpty(integrationKey))
        {
            logger.LogWarning("Missing X-Integration-Key header");
            context.Result = new UnauthorizedObjectResult(new { error = "missing_api_key" });
            return;
        }

        // ========== Step 2: Validate Timestamp (Replay Protection) ==========
        if (!long.TryParse(timestampHeader, out var timestamp))
        {
            logger.LogWarning("Invalid timestamp format: {Timestamp}", timestampHeader.ToString());
            context.Result = new UnauthorizedObjectResult(new { error = "invalid_timestamp" });
            return;
        }

        var requestTime = DateTimeOffset.FromUnixTimeSeconds(timestamp);
        var now = DateTimeOffset.UtcNow;
        var drift = (now - requestTime).Duration();

        if (drift > MaxTimestampDrift)
        {
            logger.LogWarning("Request timestamp expired: drift={Drift}s", drift.TotalSeconds);
            context.Result = new UnauthorizedObjectResult(new { error = "request_expired", driftSeconds = drift.TotalSeconds });
            return;
        }

        // ========== Step 3: Get Partner Secret ==========
        var db = context.HttpContext.RequestServices.GetRequiredService<IIntegrationDbContext>();

        // The partner sends the full key: prefix + secret (e.g. "rp_live_v1_sk_abc123...")
        // KeyHash in DB is SHA-256(secret_without_prefix), so we need to:
        // 1. Strip the prefix (rp_live_v1_sk_ or rp_test_v1_sk_)
        // 2. Hash the remainder
        // 3. Look up by hash
        var fullKey = integrationKey.ToString();
        var secret = StripPrefix(fullKey);
        var keyHash = Services.SecretHasher.Hash(secret);

        var apiKey = await db.ApiKeys
            .AsNoTracking()
            .Include(k => k.Partner)
            .FirstOrDefaultAsync(k => k.KeyHash == keyHash);

        if (apiKey is null)
        {
            logger.LogWarning("API key not found: {Key}", integrationKey.ToString().Substring(0, 8) + "...");
            context.Result = new UnauthorizedObjectResult(new { error = "invalid_api_key" });
            return;
        }

        if (!apiKey.RequireSignature)
        {
            // Signature not required for this key - skip validation
            logger.LogDebug("Signature not required for key {KeyId}, skipping", apiKey.Id);
            await next();
            return;
        }

        // Decrypt the partner secret (SecretProtected is AES-GCM encrypted)
        var protector = context.HttpContext.RequestServices.GetRequiredService<Services.ISecretProtector>();
        string partnerSecret;
        try
        {
            partnerSecret = protector.Unprotect(apiKey.SecretProtected);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to decrypt secret for key {KeyId}", apiKey.Id);
            context.Result = new UnauthorizedObjectResult(new { error = "secret_decryption_failed" });
            return;
        }

        // ========== Step 4: Read and Compute Signature ==========
        request.EnableBuffering(); // CRITICAL: Allows reading body multiple times

        string body;
        using (var reader = new StreamReader(request.Body, Encoding.UTF8, leaveOpen: true))
        {
            body = await reader.ReadToEndAsync();
            request.Body.Position = 0; // Rewind for controller
        }

        // Signature payload: timestamp + method + normalizedPath + body
        // Plugin normalizes path to start from /api/ (strips gateway prefix)
        // Example: "1678900000POST/api/partners/orders/closed{...}"
        var normalizedPath = NormalizePath(request.Path);
        var payload = $"{timestampHeader}{request.Method.ToUpperInvariant()}{normalizedPath}{body}";
        var expectedSignature = HmacCalculator.Compute(partnerSecret, payload);

        // ========== Step 5: Compare Signatures (Timing-Attack Safe) ==========
        var providedSignature = signatureHeader.ToString().ToLowerInvariant();

        if (!HmacCalculator.FixedTimeEquals(expectedSignature, providedSignature))
        {
            logger.LogWarning("Signature mismatch for key {KeyId}", apiKey.Id);
            context.Result = new UnauthorizedObjectResult(new { error = "invalid_signature" });
            return;
        }

        logger.LogDebug("Signature verified for key {KeyId}", apiKey.Id);
        await next();
    }

    /// <summary>
    /// Normalize path: strip gateway prefix so only /api/... remains.
    /// Plugin signs "/api/partners/orders/closed" but gateway may route
    /// through "/integration/api/partners/orders/closed".
    /// </summary>
    private static string NormalizePath(string path)
    {
        const string apiPrefix = "/api/";
        var idx = path.IndexOf(apiPrefix, StringComparison.OrdinalIgnoreCase);
        return idx >= 0 ? path[idx..] : path;
    }

    /// <summary>
    /// Strip the known API key prefix to extract the raw secret.
    /// Prefix format: "rp_{env}_v1_sk_" (e.g. "rp_live_v1_sk_", "rp_test_v1_sk_")
    /// If no known prefix found, returns the original value (backward compat).
    /// </summary>
    private static string StripPrefix(string fullKey)
    {
        // Known prefixes: rp_live_v1_sk_ and rp_test_v1_sk_
        const string livePrefix = "rp_live_v1_sk_";
        const string testPrefix = "rp_test_v1_sk_";

        if (fullKey.StartsWith(livePrefix, StringComparison.Ordinal))
            return fullKey[livePrefix.Length..];

        if (fullKey.StartsWith(testPrefix, StringComparison.Ordinal))
            return fullKey[testPrefix.Length..];

        // Fallback: no known prefix — assume raw secret (backward compat)
        return fullKey;
    }
}
