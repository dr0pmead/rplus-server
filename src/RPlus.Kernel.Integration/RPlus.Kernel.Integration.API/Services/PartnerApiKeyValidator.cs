using Microsoft.Extensions.Options;
using RPlus.Kernel.Integration.Application.Services;
using RPlus.SDK.Infrastructure.Integration;

namespace RPlus.Kernel.Integration.Api.Services;

public interface IPartnerApiKeyValidator
{
    Task<PartnerApiKeyValidationResult> ValidateAsync(string rawKey, CancellationToken cancellationToken);
}

public sealed record PartnerApiKeyContext(ApiKeyMetadata Metadata);

public sealed record PartnerApiKeyValidationResult(bool Success, int StatusCode, string Error, PartnerApiKeyContext? Context)
{
    public static PartnerApiKeyValidationResult Ok(ApiKeyMetadata metadata) =>
        new(true, StatusCodes.Status200OK, string.Empty, new PartnerApiKeyContext(metadata));

    public static PartnerApiKeyValidationResult Fail(int statusCode, string error) =>
        new(false, statusCode, error, null);
}

public sealed class PartnerApiKeyValidator : IPartnerApiKeyValidator
{
    private readonly IApiKeyStore _apiKeyStore;
    private readonly IIntegrationPartnerCache _partnerCache;
    private readonly IIntegRateLimiter _rateLimiter;
    private readonly IOptionsMonitor<IntegrationScanOptions> _options;

    public PartnerApiKeyValidator(
        IApiKeyStore apiKeyStore,
        IIntegrationPartnerCache partnerCache,
        IIntegRateLimiter rateLimiter,
        IOptionsMonitor<IntegrationScanOptions> options)
    {
        _apiKeyStore = apiKeyStore;
        _partnerCache = partnerCache;
        _rateLimiter = rateLimiter;
        _options = options;
    }

    public async Task<PartnerApiKeyValidationResult> ValidateAsync(string rawKey, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(rawKey))
            return PartnerApiKeyValidationResult.Fail(StatusCodes.Status401Unauthorized, "missing_integration_key");

        if (!TryParseKey(rawKey, out var env, out var secret))
            return PartnerApiKeyValidationResult.Fail(StatusCodes.Status401Unauthorized, "invalid_integration_key");

        var metadata = await _apiKeyStore.GetBySecretAsync(secret, env, cancellationToken);
        if (metadata is null || !metadata.IsActive || IsExpired(metadata))
            return PartnerApiKeyValidationResult.Fail(StatusCodes.Status401Unauthorized, "invalid_integration_key");

        if (!metadata.PartnerId.HasValue || metadata.PartnerId.Value == Guid.Empty)
            return PartnerApiKeyValidationResult.Fail(StatusCodes.Status401Unauthorized, "invalid_integration_key");

        var partnerEntry = await _partnerCache.GetAsync(metadata.PartnerId.Value, cancellationToken);
        if (partnerEntry is null || !partnerEntry.IsActive)
            return PartnerApiKeyValidationResult.Fail(StatusCodes.Status403Forbidden, "partner_inactive");

        var scope = _options.CurrentValue.RequiredPermission;
        if (!string.IsNullOrWhiteSpace(scope))
        {
            var rateOk = await _rateLimiter.IsAllowedAsync(metadata, scope, cancellationToken);
            if (!rateOk)
                return PartnerApiKeyValidationResult.Fail(StatusCodes.Status429TooManyRequests, "rate_limited");
        }

        var quotaOk = await _rateLimiter.IsWithinQuotaAsync(metadata, cancellationToken);
        if (!quotaOk)
            return PartnerApiKeyValidationResult.Fail(StatusCodes.Status429TooManyRequests, "quota_exceeded");

        return PartnerApiKeyValidationResult.Ok(metadata);
    }

    private static bool IsExpired(ApiKeyMetadata metadata) =>
        metadata.ExpiresAt.HasValue && metadata.ExpiresAt.Value <= DateTimeOffset.UtcNow;

    private static bool TryParseKey(string raw, out string env, out string secret)
    {
        env = string.Empty;
        secret = string.Empty;

        var segments = raw.Split('_', 5, StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length != 5 || !segments[0].Equals("rp", StringComparison.OrdinalIgnoreCase))
            return false;

        env = segments[1];
        secret = segments[4];
        return true;
    }
}
