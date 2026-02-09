using Grpc.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RPlus.Kernel.Integration.Application;
using RPlus.Kernel.Integration.Application.Events;
using RPlus.Kernel.Integration.Application.Services;
using RPlus.Kernel.Integration.Domain.Entities;
using RPlus.SDK.Contracts.Events;
using RPlus.SDK.Core.Errors;
using RPlus.SDK.Eventing.Abstractions;
using RPlus.SDK.Infrastructure.Integration;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Globalization;

namespace RPlus.Kernel.Integration.Api.Services;

public interface IPartnerScanService
{
    Task<PartnerScanResult> ScanAsync(
        string integrationKey,
        string qrToken,
        string? signature,
        string? signatureTimestamp,
        string? clientIp,
        string? traceId,
        IReadOnlyDictionary<string, string> headers,
        CancellationToken cancellationToken);
}

public sealed record PartnerScanResult(
    bool Success,
    int StatusCode,
    string Error,
    object? Response)
{
    public static PartnerScanResult Ok(object? response) =>
        new(true, StatusCodes.Status200OK, string.Empty, response);

    public static PartnerScanResult Fail(int statusCode, string error) =>
        new(false, statusCode, error, null);
}

public sealed class PartnerScanService : IPartnerScanService
{
    private readonly IPartnerApiKeyValidator _apiKeyValidator;
    private readonly IUserTokenResolver _tokenResolver;
    private readonly IQrTokenStore _qrTokenStore;
    private readonly IHrProfileClient _hrClient;
    private readonly ILoyaltyProfileClient _loyaltyClient;
    private readonly IScanContextResolver _scanContextResolver;
    private readonly IScanVisitStore _visitStore;
    private readonly IIntegrationAuditService _auditService;
    private readonly IIntegrationStatsPublisher _statsPublisher;
    private readonly IOptionsMonitor<IntegrationScanOptions> _options;
    private readonly IIntegrationDbContext _db;
    private readonly IEventPublisher _events;
    private readonly Infrastructure.Services.IScanProfileCache _scanCache;
    private readonly IScanProfileAggregator _scanAggregator;
    private readonly IDiscountCalculator _discountCalculator;

    public PartnerScanService(
        IPartnerApiKeyValidator apiKeyValidator,
        IUserTokenResolver tokenResolver,
        IQrTokenStore qrTokenStore,
        IHrProfileClient hrClient,
        ILoyaltyProfileClient loyaltyClient,
        IScanContextResolver scanContextResolver,
        IScanVisitStore visitStore,
        IIntegrationAuditService auditService,
        IIntegrationStatsPublisher statsPublisher,
        IOptionsMonitor<IntegrationScanOptions> options,
        IIntegrationDbContext db,
        IEventPublisher events,
        Infrastructure.Services.IScanProfileCache scanCache,
        IScanProfileAggregator scanAggregator,
        IDiscountCalculator discountCalculator)
    {
        _apiKeyValidator = apiKeyValidator;
        _tokenResolver = tokenResolver;
        _qrTokenStore = qrTokenStore;
        _hrClient = hrClient;
        _loyaltyClient = loyaltyClient;
        _scanContextResolver = scanContextResolver;
        _visitStore = visitStore;
        _auditService = auditService;
        _statsPublisher = statsPublisher;
        _options = options;
        _db = db;
        _events = events;
        _scanCache = scanCache;
        _scanAggregator = scanAggregator;
        _discountCalculator = discountCalculator;
    }

    public async Task<PartnerScanResult> ScanAsync(
        string integrationKey,
        string qrToken,
        string? signature,
        string? signatureTimestamp,
        string? clientIp,
        string? traceId,
        IReadOnlyDictionary<string, string> headers,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var statusCode = StatusCodes.Status500InternalServerError;
        var error = "internal_error";
        Guid? partnerId = null;
        Guid? apiKeyId = null;
        string environment = "unknown";
        string userId = string.Empty;
        IReadOnlyCollection<string> profileFields = Array.Empty<string>();
        ScanContextResult? scanContext = null;
        string? scanEventType = null;
        var warnings = new List<string>();
        var allowPartialResponse = _options.CurrentValue.AllowPartialResponse;
        string scanMethod = "qr"; // Default, will be updated based on token type

        var keyResult = await _apiKeyValidator.ValidateAsync(integrationKey, cancellationToken);
        if (!keyResult.Success)
        {
            statusCode = keyResult.StatusCode;
            error = keyResult.Error;
            await LogScanAsync(partnerId, apiKeyId, userId, scanContext, scanEventType, clientIp, traceId, statusCode, error, stopwatch.ElapsedMilliseconds, cancellationToken);
            await PublishScanEventAsync(partnerId, apiKeyId, environment, userId, statusCode, error, profileFields, cancellationToken);
            await PublishStatsAsync(partnerId, apiKeyId, environment, statusCode, stopwatch.ElapsedMilliseconds, traceId, error, cancellationToken);
            return PartnerScanResult.Fail(keyResult.StatusCode, keyResult.Error);
        }

        var metadata = keyResult.Context!.Metadata;

        // Unified token resolution (QR or ShortCode)
        var tokenResult = await _tokenResolver.ResolveAsync(qrToken, cancellationToken);
        scanMethod = tokenResult.Type == TokenType.ShortCode ? "otp" : "qr";
        
        if (!tokenResult.Success)
        {
            statusCode = tokenResult.Error == "auth_public_keys_unavailable"
                ? StatusCodes.Status503ServiceUnavailable
                : StatusCodes.Status400BadRequest;
            error = tokenResult.Error ?? "token_validation_failed";
            await LogScanAsync(partnerId, apiKeyId, userId, scanContext, scanEventType, clientIp, traceId, statusCode, error, stopwatch.ElapsedMilliseconds, cancellationToken);
            await PublishScanEventAsync(partnerId, apiKeyId, environment, userId, statusCode, error, profileFields, cancellationToken, scanMethod);
            await PublishStatsAsync(partnerId, apiKeyId, environment, statusCode, stopwatch.ElapsedMilliseconds, traceId, error, cancellationToken);
            return PartnerScanResult.Fail(statusCode, tokenResult.Error ?? "token_validation_failed");
        }

        partnerId = metadata.PartnerId;
        apiKeyId = metadata.KeyId;
        environment = metadata.Env ?? "unknown";
        userId = tokenResult.UserId.ToString();

        if (partnerId.HasValue && apiKeyId.HasValue)
        {
            try
            {
                scanContext = await _scanContextResolver.ResolveAsync(
                    partnerId.Value,
                    apiKeyId.Value,
                    headers,
                    cancellationToken);

                scanEventType = await ResolveEventTypeAsync(scanContext, userId, cancellationToken);
            }
            catch (ScanContextResolutionException ex)
            {
                statusCode = StatusCodes.Status400BadRequest;
                error = ex.Error;
                await LogScanAsync(partnerId, apiKeyId, userId, scanContext, scanEventType, clientIp, traceId, statusCode, error, stopwatch.ElapsedMilliseconds, cancellationToken);
                await PublishScanEventAsync(partnerId, apiKeyId, environment, userId, statusCode, error, profileFields, cancellationToken);
                await PublishStatsAsync(partnerId, apiKeyId, environment, statusCode, stopwatch.ElapsedMilliseconds, traceId, error, cancellationToken);
                return PartnerScanResult.Fail(StatusCodes.Status400BadRequest, error);
            }
        }

        var partner = await _db.Partners
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == partnerId && p.DeletedAt == null, cancellationToken);

        if (partner == null || !partner.IsActive || !partner.IsDiscountPartner)
        {
            statusCode = StatusCodes.Status403Forbidden;
            error = "partner_integration_disabled";
            await LogScanAsync(partnerId, apiKeyId, userId, scanContext, scanEventType, clientIp, traceId, statusCode, error, stopwatch.ElapsedMilliseconds, cancellationToken);
            await PublishScanEventAsync(partnerId, apiKeyId, environment, userId, statusCode, error, profileFields, cancellationToken);
            await PublishStatsAsync(partnerId, apiKeyId, environment, statusCode, stopwatch.ElapsedMilliseconds, traceId, error, cancellationToken);
            await PublishScanContextEventAsync(scanContext, userId, traceId, scanEventType, cancellationToken);
            await InvalidateQrTokenIfNeededAsync(scanContext, qrToken, userId, cancellationToken);
            return PartnerScanResult.Fail(StatusCodes.Status403Forbidden, "partner_integration_disabled");
        }

        profileFields = new[]
        {
            "user.firstName",
            "user.lastName",
            "user.avatarUrl",
            "discountUser",
            "discountPartner"
        };

        // ========== CACHE-FIRST PATTERN (~2ms on hit, ~80ms on miss) ==========
        // Step 1: Try Redis cache first (fast path)
        var cachedProfile = await _scanCache.GetAsync(tokenResult.UserId, cancellationToken);
        
        string? firstName = null;
        string? lastName = null;
        string? avatarUrl = null;
        int currentLevel = 1;
        int totalLevels = 1;
        decimal rplusDiscount = 0m;
        bool needsRefetch = false;
        
        if (cachedProfile is not null)
        {
            // v3.0 CRITICAL: If TotalLevels == 0, this is stale v2 cache entry
            // Force re-fetch to get correct TotalLevels for ratio calculation
            if (cachedProfile.TotalLevels <= 0)
            {
                needsRefetch = true;
                warnings.Add("stale_cache_v2");
            }
            else
            {
                // Cache HIT - use cached data (~1ms)
                firstName = cachedProfile.FirstName;
                lastName = cachedProfile.LastName;
                avatarUrl = cachedProfile.AvatarUrl;
                currentLevel = cachedProfile.CurrentLevel > 0 ? cachedProfile.CurrentLevel : 1;
                totalLevels = cachedProfile.TotalLevels;
                rplusDiscount = cachedProfile.RPlusDiscount;
            }
        }
        
        if (cachedProfile is null || needsRefetch)
        {
            // Cache MISS or STALE - fallback to REST aggregation (slow path, ~80ms)
            // This will also populate the cache with v3 data for next time
            var aggregated = await _scanAggregator.FetchAndCacheAsync(tokenResult.UserId, cancellationToken);
            
            if (aggregated is null)
            {
                statusCode = StatusCodes.Status404NotFound;
                error = "user_not_found";
                await LogScanAsync(partnerId, apiKeyId, userId, scanContext, scanEventType, clientIp, traceId, statusCode, error, stopwatch.ElapsedMilliseconds, cancellationToken);
                await PublishScanEventAsync(partnerId, apiKeyId, environment, userId, statusCode, error, profileFields, cancellationToken);
                await PublishStatsAsync(partnerId, apiKeyId, environment, statusCode, stopwatch.ElapsedMilliseconds, traceId, error, cancellationToken);
                return PartnerScanResult.Fail(StatusCodes.Status404NotFound, "user_not_found");
            }
            
            firstName = aggregated.FirstName;
            lastName = aggregated.LastName;
            avatarUrl = aggregated.AvatarUrl;
            currentLevel = aggregated.CurrentLevel > 0 ? aggregated.CurrentLevel : 1;
            totalLevels = aggregated.TotalLevels > 0 ? aggregated.TotalLevels : 1;
            rplusDiscount = aggregated.RPlusDiscount;
            if (!needsRefetch) warnings.Add("cache_miss"); // Only add if not already stale
        }

        // ========== DYNAMIC LEVEL-BASED DISCOUNT CALCULATION ==========
        var userProfile = new CachedUserProfile(currentLevel, totalLevels, rplusDiscount);
        var partnerConfig = new PartnerDiscountConfig(
            partner.DiscountStrategy,
            partner.PartnerCategory,
            partner.MaxDiscount,
            partner.DiscountPartner,
            partner.HappyHoursConfigJson);
        
        var discountResult = _discountCalculator.Calculate(userProfile, partnerConfig);

        // v3.0: Clean response - only discounts, no meta block
        var payload = new
        {
            user = new
            {
                firstName,
                lastName,
                avatarUrl
            },
            discounts = new
            {
                rplus = discountResult.RPlusDiscount,
                partner = discountResult.PartnerDiscount,
                total = discountResult.TotalDiscount
            },
            warnings = warnings.Count > 0 ? warnings : null
        };
        statusCode = StatusCodes.Status200OK;
        error = string.Empty;
        await LogScanAsync(partnerId, apiKeyId, userId, scanContext, scanEventType, clientIp, traceId, statusCode, error, stopwatch.ElapsedMilliseconds, cancellationToken);
        await PublishScanEventAsync(partnerId, apiKeyId, environment, userId, statusCode, error, profileFields, cancellationToken, scanMethod);
        await PublishStatsAsync(partnerId, apiKeyId, environment, statusCode, stopwatch.ElapsedMilliseconds, traceId, error, cancellationToken);
        await PublishScanContextEventAsync(scanContext, userId, traceId, scanEventType, cancellationToken);
        await InvalidateQrTokenIfNeededAsync(scanContext, qrToken, userId, cancellationToken);
        return PartnerScanResult.Ok(payload);
    }


    private async Task<string> ResolveEventTypeAsync(
        ScanContextResult scanContext,
        string userId,
        CancellationToken cancellationToken)
    {
        if (scanContext.ContextType == "mp")
        {
            var ttl = TimeSpan.FromHours(_options.CurrentValue.MpVisitTtlHours);
            var entered = await _visitStore.ToggleVisitAsync(scanContext.ContextId, userId, ttl, cancellationToken);
            return entered ? "mp_entered" : "mp_left";
        }

        if (scanContext.ContextType == "access")
            return "access_granted";

        return "partner_scan";
    }

    private async Task LogScanAsync(
        Guid? partnerId,
        Guid? apiKeyId,
        string userId,
        ScanContextResult? scanContext,
        string? scanEventType,
        string? clientIp,
        string? traceId,
        int statusCode,
        string error,
        long latencyMs,
        CancellationToken cancellationToken)
    {
        try
        {
            string? details = null;
            if (!string.IsNullOrWhiteSpace(userId) || scanContext is not null || !string.IsNullOrWhiteSpace(scanEventType))
            {
                var detailMap = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["user_id"] = string.IsNullOrWhiteSpace(userId) ? null : userId,
                    ["scan_event_type"] = string.IsNullOrWhiteSpace(scanEventType) ? null : scanEventType,
                    ["integration_id"] = scanContext?.IntegrationId.ToString(),
                    ["context_id"] = scanContext?.ContextId
                };
                details = System.Text.Json.JsonSerializer.Serialize(detailMap);
            }

            var log = new IntegrationAuditLog
            {
                Id = Guid.NewGuid(),
                PartnerId = partnerId,
                ApiKeyId = apiKeyId,
                Action = "scan",
                Details = details,
                CreatedAt = DateTime.UtcNow,
                IpAddress = clientIp,
                ErrorMessage = string.IsNullOrWhiteSpace(error) ? null : error,
                TraceId = traceId,
                RequestMethod = "POST",
                RequestPath = "/api/integration/v1/scan",
                TargetService = "integration",
                Timestamp = DateTime.UtcNow,
                StatusCode = statusCode,
                DurationMs = latencyMs,
                ClientIp = clientIp ?? string.Empty
            };

            await _auditService.LogAsync(log, cancellationToken);
        }
        catch
        {
            // logging must not break scan flow
        }
    }

    private async Task PublishScanEventAsync(
        Guid? partnerId,
        Guid? apiKeyId,
        string environment,
        string userId,
        int statusCode,
        string error,
        IReadOnlyCollection<string> fields,
        CancellationToken cancellationToken,
        string scanMethod = "qr")
    {
        if (!partnerId.HasValue || !apiKeyId.HasValue)
        {
            return;
        }

        try
        {
            var evt = new IntegrationScanEvent(
                partnerId.Value,
                apiKeyId.Value,
                environment,
                userId,
                statusCode,
                error,
                fields,
                DateTime.UtcNow)
            {
                ScanMethod = scanMethod
            };

            await _events.PublishAsync(evt, IntegrationScanEvent.EventName, apiKeyId.Value.ToString(), cancellationToken);
        }
        catch
        {
            // ignore instrumentation errors
        }
    }

    private async Task PublishStatsAsync(
        Guid? partnerId,
        Guid? apiKeyId,
        string environment,
        int statusCode,
        long latencyMs,
        string? traceId,
        string error,
        CancellationToken cancellationToken)
    {
        if (!partnerId.HasValue || !apiKeyId.HasValue)
        {
            return;
        }

        try
        {
            var evt = new IntegrationStatsEvent(
                partnerId.Value,
                apiKeyId.Value,
                string.IsNullOrWhiteSpace(environment) ? "unknown" : environment,
                "scan",
                "/api/integration/v1/scan",
                statusCode,
                latencyMs,
                traceId ?? string.Empty,
                MapErrorCategory(statusCode, error));

            await _statsPublisher.PublishAsync(evt, cancellationToken);
        }
        catch
        {
            // ignore stats failures
        }
    }

    private async Task PublishScanContextEventAsync(
        ScanContextResult? scanContext,
        string userId,
        string? requestId,
        string? eventType,
        CancellationToken cancellationToken)
    {
        if (scanContext is null || string.IsNullOrWhiteSpace(eventType))
            return;

        try
        {
            var evt = new IntegrationScanContextEvent(
                scanContext.IntegrationId,
                scanContext.ContextId,
                userId,
                eventType,
                requestId ?? string.Empty,
                DateTime.UtcNow);

            await _events.PublishAsync(evt, IntegrationScanContextEvent.EventName, scanContext.IntegrationId.ToString(), cancellationToken);
        }
        catch
        {
            // ignore instrumentation errors
        }
    }

    private async Task InvalidateQrTokenIfNeededAsync(
        ScanContextResult? scanContext,
        string qrToken,
        string userId,
        CancellationToken cancellationToken)
    {
        if (scanContext is null || scanContext.Policy.Invalidate is false)
            return;

        if (string.IsNullOrWhiteSpace(qrToken) || string.IsNullOrWhiteSpace(userId))
            return;

        try
        {
            await _qrTokenStore.InvalidateAsync(qrToken, userId, cancellationToken);
        }
        catch
        {
            // ignore invalidation failures
        }
    }

    private static object BuildDegradedProfilePayload(string reason)
    {
        return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["meta"] = new Dictionary<string, object?>
            {
                ["degraded"] = true,
                ["reason"] = reason
            }
        };
    }

    private static ErrorCategory MapErrorCategory(int statusCode, string error)
    {
        if (string.Equals(error, "missing_permission", StringComparison.OrdinalIgnoreCase))
            return ErrorCategory.Forbidden;
        if (string.Equals(error, "invalid_integration_key", StringComparison.OrdinalIgnoreCase))
            return ErrorCategory.Unauthorized;
        if (string.Equals(error, "hr_timeout", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(error, "hr_unavailable", StringComparison.OrdinalIgnoreCase))
            return ErrorCategory.DownstreamUnavailable;

        return statusCode switch
        {
            >= 500 => ErrorCategory.InternalError,
            429 => ErrorCategory.RateLimitExceeded,
            404 => ErrorCategory.NotFound,
            403 => ErrorCategory.Forbidden,
            401 => ErrorCategory.Unauthorized,
            >= 400 => ErrorCategory.ValidationFailed,
            _ => ErrorCategory.None
        };
    }

    private static string? ValidateSignature(
        string secret,
        string qrToken,
        string? signature,
        string? signatureTimestamp,
        int toleranceSeconds)
    {
        if (string.IsNullOrWhiteSpace(signature) || string.IsNullOrWhiteSpace(signatureTimestamp))
            return "missing_signature";

        if (!long.TryParse(signatureTimestamp, out var timestampSeconds))
            return "invalid_signature_timestamp";

        var nowSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (Math.Abs(nowSeconds - timestampSeconds) > toleranceSeconds)
            return "signature_timestamp_out_of_range";

        var payload = $"{timestampSeconds}.{qrToken}";
        var computed = ComputeHmacHex(secret, payload);

        return FixedTimeEquals(signature, computed) ? null : "invalid_signature";
    }

    private static string ComputeHmacHex(string secret, string payload)
    {
        using var hmac = new System.Security.Cryptography.HMACSHA256(System.Text.Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static bool FixedTimeEquals(string left, string right)
    {
        // Use .NET's built-in constant-time comparison to prevent timing attacks.
        // This handles length differences securely without leaking information.
        var leftBytes = System.Text.Encoding.UTF8.GetBytes(left ?? string.Empty);
        var rightBytes = System.Text.Encoding.UTF8.GetBytes(right ?? string.Empty);
        return System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
    }
}
