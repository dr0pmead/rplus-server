using Grpc.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RPlusGrpc.Access;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace RPlus.Gateway.Api.Services;

public sealed class ProxyAuthorizationService
{
    private static readonly Regex UnsafeChars = new("[^a-z0-9_]+", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex VersionSegment = new("^v\\d+$", RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private readonly AccessService.AccessServiceClient _access;
    private readonly IMemoryCache _cache;
    private readonly ProxyAuthorizationOptions _options;
    private readonly ILogger<ProxyAuthorizationService> _logger;

    public ProxyAuthorizationService(
        AccessService.AccessServiceClient access,
        IMemoryCache cache,
        IOptions<ProxyAuthorizationOptions> options,
        ILogger<ProxyAuthorizationService> logger)
    {
        _access = access;
        _cache = cache;
        _options = options.Value ?? new ProxyAuthorizationOptions();
        _logger = logger;
    }

    public async Task<(bool Allowed, string? PermissionId, string? Error)> AuthorizeProxyRequestAsync(
        HttpContext context,
        string service,
        string? catchAll,
        CancellationToken ct)
    {
        if (!_options.Enabled)
            return (true, null, null);

        if (HttpMethods.IsOptions(context.Request.Method))
            return (true, null, null);

        var userId = GetUserId(context.User);
        if (string.IsNullOrWhiteSpace(userId))
            return (false, null, "unauthorized");

        var tenantId = ResolveTenantId(context);
        if (tenantId == Guid.Empty)
        {
            // Tenant is optional in current platform; keep default to Guid.Empty.
        }

        var applicationId = ResolveApplicationId(context);

        var permissionId = ResolvePermissionId(service, catchAll, context.Request.Method);
        if (string.IsNullOrWhiteSpace(permissionId))
            return (false, null, "missing_permission");

        var permissionCandidates = BuildPermissionCandidates(service, catchAll, context.Request.Method, permissionId);

        var cacheKey = $"pxauth:{userId}:{tenantId}:{applicationId}:{permissionId}";
        if (_options.DecisionCacheSeconds > 0 &&
            _cache.TryGetValue<bool>(cacheKey, out var cachedAllowed))
        {
            return (cachedAllowed, permissionId, cachedAllowed ? null : "forbidden");
        }

        var baseRequest = new CheckPermissionRequest
        {
            UserId = userId,
            TenantId = tenantId.ToString(),
            ApplicationId = applicationId,
            Context = BuildContextJson(context)
        };

        try
        {
            var allowed = false;
            foreach (var candidate in permissionCandidates)
            {
                baseRequest.PermissionId = candidate;
                var response = await _access.CheckPermissionAsync(baseRequest, cancellationToken: ct);
                if (response.IsAllowed)
                {
                    allowed = true;
                    break;
                }
            }

            if (_options.DecisionCacheSeconds > 0)
            {
                _cache.Set(cacheKey, allowed, TimeSpan.FromSeconds(_options.DecisionCacheSeconds));
            }

            return (allowed, permissionId, allowed ? null : "forbidden");
        }
        catch (RpcException ex) when (ex.StatusCode is StatusCode.Unavailable or StatusCode.DeadlineExceeded)
        {
            _logger.LogWarning(ex, "Access gRPC unavailable during proxy authorization for {PermissionId}", permissionId);
            return _options.FailOpenOnAccessUnavailable
                ? (true, permissionId, null)
                : (false, permissionId, "access_unavailable");
        }
        catch (RpcException ex)
        {
            _logger.LogWarning(ex, "Access gRPC failed during proxy authorization for {PermissionId} with {Status}", permissionId, ex.StatusCode);
            return _options.FailOpenOnAccessUnavailable
                ? (true, permissionId, null)
                : (false, permissionId, "access_error");
        }
    }

    private static string? GetUserId(ClaimsPrincipal principal) =>
        principal.FindFirstValue("sub")
        ?? principal.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? principal.FindFirstValue("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier");

    private Guid ResolveTenantId(HttpContext context)
    {
        var claim = context.User.FindFirstValue("tenant_id") ?? context.User.FindFirstValue("tenantId");
        if (!string.IsNullOrWhiteSpace(claim) && Guid.TryParse(claim, out var fromClaim))
            return fromClaim;

        if (context.Request.Headers.TryGetValue(_options.TenantIdHeaderName, out var header) &&
            Guid.TryParse(header.ToString(), out var fromHeader))
        {
            return fromHeader;
        }

        return Guid.Empty;
    }

    private string ResolveApplicationId(HttpContext context)
    {
        var appId = _options.DefaultApplicationId;

        if (context.Request.Headers.TryGetValue(_options.ApplicationIdHeaderName, out var headerValue))
        {
            var raw = headerValue.ToString().Trim();
            if (!string.IsNullOrWhiteSpace(raw) && IsAllowedAppId(raw))
                appId = raw;
        }

        return appId;
    }

    private bool IsAllowedAppId(string appId)
    {
        if (_options.AllowedApplicationIds == null || _options.AllowedApplicationIds.Length == 0)
            return true;

        foreach (var allowed in _options.AllowedApplicationIds)
        {
            if (string.Equals(allowed, appId, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    public static string ResolvePermissionId(string service, string? catchAll, string httpMethod)
    {
        var normalizedService = NormalizeSegment(service);
        if (string.IsNullOrWhiteSpace(normalizedService))
            return string.Empty;

        var normalizedResource = NormalizeFirstPathSegment(catchAll);
        var action = httpMethod switch
        {
            "GET" or "HEAD" => "read",
            "POST" => "create",
            "PUT" or "PATCH" => "update",
            "DELETE" => "delete",
            _ => "execute"
        };

        // Convention: {service}[.{resource}].{action}
        // Examples:
        // - GET /api/loyalty/graph-rules        => loyalty.graph_rules.read
        // - POST /api/loyalty/graph-rules       => loyalty.graph_rules.create
        // - PUT /api/loyalty/graph-rules/123    => loyalty.graph_rules.update
        // - DELETE /api/loyalty/graph-rules/123 => loyalty.graph_rules.delete
        return string.IsNullOrWhiteSpace(normalizedResource)
            ? $"{normalizedService}.{action}"
            : $"{normalizedService}.{normalizedResource}.{action}";
    }

    private static string NormalizeFirstPathSegment(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return string.Empty;

        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length == 0)
            return string.Empty;

        var index = 0;
        if (segments.Length > 1 && IsVersionSegment(segments[0]))
            index = 1;

        return NormalizeSegment(segments[index]);
    }

    private static string NormalizeSegment(string segment)
    {
        var s = segment.Trim().ToLowerInvariant();
        s = s.Replace('-', '_');
        s = UnsafeChars.Replace(s, "_").Trim('_');
        return s;
    }

    private static bool IsVersionSegment(string segment) => VersionSegment.IsMatch(segment.Trim());

    private static string BuildContextJson(HttpContext context)
    {
        var payload = new Dictionary<string, string?>
        {
            ["ip"] = context.Connection.RemoteIpAddress?.ToString(),
            ["method"] = context.Request.Method,
            ["path"] = context.Request.Path.Value,
            ["userAgent"] = context.Request.Headers.UserAgent.ToString()
        };

        return JsonSerializer.Serialize(payload);
    }

    private static IReadOnlyList<string> BuildPermissionCandidates(string service, string? catchAll, string httpMethod, string primaryPermissionId)
    {
        var candidates = new List<string>(4) { primaryPermissionId };

        var normalizedService = NormalizeSegment(service);
        var normalizedResource = NormalizeFirstPathSegment(catchAll);
        var action = httpMethod switch
        {
            "GET" or "HEAD" => "read",
            "POST" => "create",
            "PUT" or "PATCH" => "update",
            "DELETE" => "delete",
            _ => "execute"
        };

        // Support coarser grants like "users.manage" (and optionally "users.profile.manage").
        if (!string.IsNullOrWhiteSpace(normalizedService))
        {
            if (!string.IsNullOrWhiteSpace(normalizedResource))
                candidates.Add($"{normalizedService}.{normalizedResource}.manage");

            if (!string.Equals(action, "read", StringComparison.OrdinalIgnoreCase))
                candidates.Add($"{normalizedService}.manage");
            else
                candidates.Add($"{normalizedService}.manage");
        }

        // De-dup while preserving order
        var unique = new List<string>(candidates.Count);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var c in candidates)
        {
            if (string.IsNullOrWhiteSpace(c))
                continue;
            if (seen.Add(c))
                unique.Add(c);
        }

        return unique;
    }
}
