using Microsoft.Extensions.Options;
using RPlus.Kernel.Integration.Application.Services;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace RPlus.Kernel.Integration.Api.Services;

public sealed class ScanContextResolver : IScanContextResolver
{
    private readonly IIntegrationPartnerCache _partnerCache;
    private readonly IOptionsMonitor<IntegrationScanOptions> _options;

    public ScanContextResolver(IIntegrationPartnerCache partnerCache, IOptionsMonitor<IntegrationScanOptions> options)
    {
        _partnerCache = partnerCache;
        _options = options;
    }

    public async Task<ScanContextResult> ResolveAsync(
        Guid integrationId,
        Guid apiKeyId,
        IReadOnlyDictionary<string, string> headers,
        CancellationToken cancellationToken)
    {
        var partner = await _partnerCache.GetAsync(integrationId, cancellationToken);
        var contextType = ResolveContextType(partner, headers);
        var contextId = ResolveContextId(integrationId, contextType, headers);
        var policy = ResolvePolicy(contextType);

        return new ScanContextResult(contextType, contextId, policy, integrationId, apiKeyId);
    }

    private static string ResolveContextType(IntegrationPartnerCacheEntry? partner, IReadOnlyDictionary<string, string> headers)
    {
        if (headers.TryGetValue("x-scan-context", out var raw) && !string.IsNullOrWhiteSpace(raw))
        {
            var normalized = raw.Trim().ToLowerInvariant();
            if (normalized is "mp" or "partner" or "access")
                return normalized;
        }

        if (headers.TryGetValue("x-device-type", out var deviceType) && !string.IsNullOrWhiteSpace(deviceType))
        {
            var normalized = deviceType.Trim().ToLowerInvariant();
            if (normalized is "mp" or "access")
                return normalized;
        }

        if (partner is not null)
        {
            if (partner.IsDiscountPartner)
                return "partner";

            if (string.Equals(partner.AccessLevel, "system", StringComparison.OrdinalIgnoreCase))
                return "mp";
        }

        return "partner";
    }

    private static string ResolveContextId(Guid integrationId, string contextType, IReadOnlyDictionary<string, string> headers)
    {
        if (headers.TryGetValue("x-context-id", out var contextId) && !string.IsNullOrWhiteSpace(contextId))
            return contextId.Trim();

        if (headers.TryGetValue("x-device-id", out var deviceId) && !string.IsNullOrWhiteSpace(deviceId))
            return deviceId.Trim();

        if (contextType != "partner")
            throw new ScanContextResolutionException("missing_context_id");

        return integrationId.ToString("D");
    }

    private ScanContextPolicy ResolvePolicy(string contextType)
    {
        var invalidate = contextType is "partner" or "access" or "mp";
        int? ttlSeconds = null;

        if (contextType == "mp")
        {
            ttlSeconds = (int)TimeSpan.FromHours(_options.CurrentValue.MpVisitTtlHours).TotalSeconds;
        }

        return new ScanContextPolicy(invalidate, ttlSeconds);
    }
}
