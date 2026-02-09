using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using RPlus.SDK.Contracts.External;
using RPlus.SDK.Core.Abstractions;
using RPlus.SDK.Core.Errors;

namespace RPlus.SDK.Infrastructure.Integration;

public sealed class ApiKeyAuthMiddleware
{
    private const string ApiKeyHeader = "X-RPlus-Api-Key";
    private const string SignatureHeader = "X-RPlus-Signature";
    private const string TimestampHeader = "X-RPlus-Timestamp";
    private static readonly TimeSpan MaxClockSkew = TimeSpan.FromMinutes(5);

    private readonly RequestDelegate _next;
    private readonly IApiKeyStore _store;
    private readonly IIntegRateLimiter _rateLimiter;

    public ApiKeyAuthMiddleware(
        RequestDelegate next,
        IApiKeyStore store,
        IIntegRateLimiter rateLimiter)
    {
        _next = next;
        _store = store;
        _rateLimiter = rateLimiter;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = EnsureCorrelationId(context);

        if (!context.Request.Headers.TryGetValue(ApiKeyHeader, out var rawKey) ||
            StringValues.IsNullOrEmpty(rawKey))
        {
            await WriteErrorAsync(context, ErrorCategory.Unauthorized, correlationId, StatusCodes.Status401Unauthorized);
            return;
        }

        if (!TryParseKey(rawKey.ToString(), out var env, out _, out _, out var secret))
        {
            await WriteErrorAsync(context, ErrorCategory.Unauthorized, correlationId, StatusCodes.Status401Unauthorized);
            return;
        }

        string? timestampError = null;
        if (!context.Request.Headers.TryGetValue(TimestampHeader, out var timestampHeader) ||
            !TryValidateTimestamp(timestampHeader, out timestampError))
        {
            await WriteErrorAsync(context, ErrorCategory.Unauthorized, correlationId, StatusCodes.Status401Unauthorized, timestampError);
            return;
        }

        var metadata = await _store.GetBySecretAsync(secret, env, context.RequestAborted);
        if (metadata is null || !metadata.IsActive || IsExpired(metadata))
        {
            await WriteErrorAsync(context, ErrorCategory.Unauthorized, correlationId, StatusCodes.Status401Unauthorized);
            return;
        }

        var external = context.GetEndpoint()?.Metadata.GetMetadata<ExternalAttribute>();
        if (external?.Scope is null || !metadata.Scopes.Contains(external.Scope))
        {
            await WriteErrorAsync(context, ErrorCategory.Forbidden, correlationId, StatusCodes.Status403Forbidden);
            return;
        }

        if (metadata.RequireSignature || context.Request.Headers.ContainsKey(SignatureHeader))
        {
            if (!context.Request.Headers.TryGetValue(SignatureHeader, out var signature) ||
                StringValues.IsNullOrEmpty(signature) ||
                !await ValidateSignatureAsync(context, signature, timestampHeader, metadata.Secret ?? string.Empty))
            {
                await WriteErrorAsync(context, ErrorCategory.Unauthorized, correlationId, StatusCodes.Status401Unauthorized);
                return;
            }
        }

        if (!await _rateLimiter.IsAllowedAsync(metadata, external.Scope, context.RequestAborted))
        {
            await WriteErrorAsync(context, ErrorCategory.RateLimitExceeded, correlationId, StatusCodes.Status429TooManyRequests);
            return;
        }

        context.User = CreatePrincipal(metadata, external.Scope!);
        await _next(context);
    }

    private static ClaimsPrincipal CreatePrincipal(ApiKeyMetadata metadata, string scope)
    {
        if (string.IsNullOrEmpty(scope))
        {
            throw new ArgumentException("Scope must be provided.", nameof(scope));
        }

        var claims = new List<Claim>
        {
            BuildClaim("partner_id", metadata.PartnerId),
            BuildClaim("key_id", metadata.KeyId),
            BuildClaim("env", metadata.Env),
            BuildClaim("scope", scope)
        };

        foreach (var declaredScope in metadata.Scopes)
        {
            var sanitizedScope = declaredScope;
            if (string.IsNullOrWhiteSpace(sanitizedScope))
            {
                continue;
            }

            claims.Add(BuildClaim("scope", sanitizedScope));
        }

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "ApiKey"));
    }

    private static Claim BuildClaim(string type, object? value) =>
        new(type, value?.ToString() ?? string.Empty);

    private static bool IsExpired(ApiKeyMetadata metadata) =>
        metadata.ExpiresAt.HasValue && metadata.ExpiresAt.Value <= DateTimeOffset.UtcNow;

    private static bool TryParseKey(
        string raw,
        out string env,
        out string version,
        out string type,
        out string secret)
    {
        env = string.Empty;
        version = string.Empty;
        type = string.Empty;
        secret = string.Empty;

        var segments = raw.Split('_', 5, StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length != 5 || !segments[0].Equals("rp", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        env = segments[1];
        version = segments[2];
        type = segments[3];
        secret = segments[4];
        return true;
    }

    private static bool TryValidateTimestamp(StringValues header, out string? error)
    {
        error = null;
        if (!long.TryParse(header.ToString(), out var timestampSeconds))
        {
            error = "invalid_timestamp";
            return false;
        }

        var delta = (DateTimeOffset.UtcNow - DateTimeOffset.FromUnixTimeSeconds(timestampSeconds)).Duration();
        if (delta <= MaxClockSkew)
        {
            return true;
        }

        error = "timestamp_skew";
        return false;
    }

    private static async Task<bool> ValidateSignatureAsync(
        HttpContext context,
        StringValues signatureHeader,
        StringValues timestampHeader,
        string secret)
    {
        context.Request.EnableBuffering();
        var bodyHash = await GetBodyHashAsync(context.Request).ConfigureAwait(false);
        context.Request.Body.Position = 0;

        var payload = string.Concat(
            context.Request.Method,
            context.Request.Path.Value ?? string.Empty,
            timestampHeader.ToString(),
            bodyHash);

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var actualSignature = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload)));
        return actualSignature.Equals(signatureHeader.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<string> GetBodyHashAsync(HttpRequest request)
    {
        if (request.Body == Stream.Null)
        {
            return string.Empty;
        }

        using var reader = new StreamReader(request.Body, Encoding.UTF8, leaveOpen: true);
        var body = await reader.ReadToEndAsync().ConfigureAwait(false);
        if (string.IsNullOrEmpty(body))
        {
            return string.Empty;
        }

        using var sha256 = SHA256.Create();
        return Convert.ToHexString(sha256.ComputeHash(Encoding.UTF8.GetBytes(body)));
    }

    private static string EnsureCorrelationId(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue("X-Correlation-Id", out var header) &&
            !StringValues.IsNullOrEmpty(header))
        {
            var value = header.ToString();
            context.Items["X-Correlation-Id"] = value;
            return value;
        }

        var correlationId = Guid.NewGuid().ToString();
        context.Items["X-Correlation-Id"] = correlationId;
        context.Response.Headers["X-Correlation-Id"] = correlationId;
        return correlationId;
    }

    private static Task WriteErrorAsync(
        HttpContext context,
        ErrorCategory errorCode,
        string? correlationId,
        int statusCode,
        string? message = null)
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";
        return context.Response.WriteAsJsonAsync(ExternalResult<object>.Fail(errorCode, correlationId ?? string.Empty, message));
    }
}
