using Grpc.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using RPlusGrpc.Access;
using RPlusGrpc.Integration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace RPlus.Gateway.Api.Services;

public sealed record IntegrationKeyContext(
    string ApiKeyId,
    string? PartnerId,
    bool IsDiscountPartner,
    IReadOnlyList<string> Permissions);

public sealed record IntegrationKeyAuthorizationResult(
    bool Allowed,
    string? Error,
    string? PermissionId,
    IntegrationKeyContext? Context);

public sealed class IntegrationKeyAuthorizationService
{
    private static readonly Regex UnsafeChars = new("[^a-z0-9_]+", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex VersionSegment = new("^v\\d+$", RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private readonly IntegrationService.IntegrationServiceClient _integration;
    private readonly AccessService.AccessServiceClient _access;
    private readonly ILogger<IntegrationKeyAuthorizationService> _logger;

    public IntegrationKeyAuthorizationService(
        IntegrationService.IntegrationServiceClient integration,
        AccessService.AccessServiceClient access,
        ILogger<IntegrationKeyAuthorizationService> logger)
    {
        _integration = integration;
        _access = access;
        _logger = logger;
    }

    public async Task<IntegrationKeyAuthorizationResult> AuthorizeAsync(
        HttpContext context,
        string service,
        string? catchAll,
        CancellationToken ct)
    {
        var rawKey = context.Request.Headers["X-Integration-Key"].ToString();
        if (string.IsNullOrWhiteSpace(rawKey))
            return new IntegrationKeyAuthorizationResult(false, "missing_integration_key", null, null);

        if (!TryParseKey(rawKey, out _, out var secret))
            return new IntegrationKeyAuthorizationResult(false, "invalid_integration_key", null, null);

        ValidateKeyResponse validate;
        try
        {
            validate = await _integration.ValidateKeyAsync(new ValidateKeyRequest
            {
                Key = rawKey,
                Secret = secret
            }, cancellationToken: ct);
        }
        catch (RpcException ex)
        {
            _logger.LogWarning(ex, "Integration gRPC failed during key validation");
            return new IntegrationKeyAuthorizationResult(false, "integration_unavailable", null, null);
        }

        if (!validate.Success)
            return new IntegrationKeyAuthorizationResult(false, string.IsNullOrWhiteSpace(validate.Error) ? "invalid_integration_key" : validate.Error, null, null);

        var permissionId = ProxyAuthorizationService.ResolvePermissionId(service, catchAll, context.Request.Method);
        if (string.IsNullOrWhiteSpace(permissionId))
            return new IntegrationKeyAuthorizationResult(false, "missing_permission", null, null);

        if (IsIntegrationScan(service, catchAll))
        {
            var scanContext = new IntegrationKeyContext(
                validate.ApiKeyId ?? string.Empty,
                string.IsNullOrWhiteSpace(validate.PartnerId) ? null : validate.PartnerId,
                validate.IsDiscountPartner,
                Array.Empty<string>());
            return new IntegrationKeyAuthorizationResult(true, null, permissionId, scanContext);
        }

        try
        {
            var request = new GetIntegrationPermissionsRequest
            {
                ApiKeyId = validate.ApiKeyId ?? string.Empty
            };
            request.ContextSignals.Add("service", service ?? string.Empty);
            request.ContextSignals.Add("permission", permissionId);
            request.ContextSignals.Add("method", context.Request.Method ?? string.Empty);
            request.ContextSignals.Add("path", context.Request.Path.Value ?? string.Empty);

            var response = await _access.GetIntegrationPermissionsAsync(request, cancellationToken: ct);
            if (!response.Success)
                return new IntegrationKeyAuthorizationResult(false, string.IsNullOrWhiteSpace(response.Error) ? "access_error" : response.Error, permissionId, null);

            if (response.Decision != IntegrationDecision.Allowed)
                return new IntegrationKeyAuthorizationResult(false, "forbidden", permissionId, null);

            var permissions = response.Permissions.ToList();
            var candidates = BuildPermissionCandidates(service ?? string.Empty, catchAll, context.Request.Method ?? "GET", permissionId);
            var allowed = permissions.Any(p => candidates.Contains(p, StringComparer.OrdinalIgnoreCase));
            if (!allowed)
                return new IntegrationKeyAuthorizationResult(false, "forbidden", permissionId, null);

            var resultContext = new IntegrationKeyContext(
                validate.ApiKeyId ?? string.Empty,
                string.IsNullOrWhiteSpace(validate.PartnerId) ? null : validate.PartnerId,
                validate.IsDiscountPartner,
                permissions);

            return new IntegrationKeyAuthorizationResult(true, null, permissionId, resultContext);
        }
        catch (RpcException ex) when (ex.StatusCode is StatusCode.Unavailable or StatusCode.DeadlineExceeded)
        {
            _logger.LogWarning(ex, "Access gRPC unavailable during integration authorization");
            return new IntegrationKeyAuthorizationResult(false, "access_unavailable", permissionId, null);
        }
        catch (RpcException ex)
        {
            _logger.LogWarning(ex, "Access gRPC failed during integration authorization with {Status}", ex.StatusCode);
            return new IntegrationKeyAuthorizationResult(false, "access_error", permissionId, null);
        }
    }

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

    private static bool IsIntegrationScan(string service, string? catchAll)
    {
        if (!service.Equals("integration", StringComparison.OrdinalIgnoreCase))
            return false;

        if (string.IsNullOrWhiteSpace(catchAll))
            return false;

        return catchAll.StartsWith("v1/scan", StringComparison.OrdinalIgnoreCase) ||
               catchAll.Equals("scan", StringComparison.OrdinalIgnoreCase);
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

        if (!string.IsNullOrWhiteSpace(normalizedService))
        {
            if (!string.IsNullOrWhiteSpace(normalizedResource))
                candidates.Add($"{normalizedService}.{normalizedResource}.manage");

            candidates.Add($"{normalizedService}.manage");
        }

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
}
