using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using RPlus.SDK.Access.Authorization;
using RPlusGrpc.Access;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace RPlus.SDK.Infrastructure.Access.PermissionDiscovery;

public static class PermissionManifestDiscovery
{
    private static readonly Regex UnsafeChars = new("[^a-z0-9_]+", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex VersionSegment = new("^v\\d+$", RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    public static UpsertPermissionManifestRequest Build(EndpointDataSource endpointDataSource, PermissionManifestPublisherOptions options)
    {
        if (endpointDataSource == null) throw new ArgumentNullException(nameof(endpointDataSource));
        if (options == null) throw new ArgumentNullException(nameof(options));

        var request = new UpsertPermissionManifestRequest
        {
            ServiceName = options.ServiceName ?? string.Empty,
            ApplicationId = options.ApplicationId ?? "system",
            MarkMissingAsDeprecated = options.MarkMissingAsDeprecated
        };

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var endpoint in endpointDataSource.Endpoints)
        {
            if (endpoint is not RouteEndpoint routeEndpoint)
                continue;

            if (IsAnonymous(endpoint))
                continue;

            var methods = endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods;
            var httpMethod = methods?.FirstOrDefault();

            var permissions = endpoint.Metadata.OfType<RequiresPermissionAttribute>()
                .Select(a => a.PermissionId)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToArray();

            var anyPermissions = endpoint.Metadata.OfType<RequiresAnyPermissionAttribute>()
                .SelectMany(a => a.PermissionIds)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToArray();

            if (anyPermissions.Length > 0)
            {
                permissions = permissions.Concat(anyPermissions).ToArray();
            }

            if (permissions.Length == 0)
            {
                var generated = TryGeneratePermissionId(routeEndpoint.RoutePattern.RawText, httpMethod);
                if (!string.IsNullOrWhiteSpace(generated))
                    permissions = new[] { generated };
            }

            foreach (var permissionId in permissions)
            {
                var id = permissionId.Trim();
                if (id.Length == 0 || id.Length > 150 || !seen.Add(id))
                    continue;

                request.Permissions.Add(new PermissionManifestEntry
                {
                    PermissionId = id,
                    Title = $"Discovered {id}",
                    Description = string.Empty
                });
            }
        }

        return request;
    }

    private static bool IsAnonymous(Endpoint endpoint)
    {
        if (endpoint.Metadata.GetMetadata<IAllowAnonymous>() != null)
            return true;

        // Also treat explicit [AllowAnonymous] as anonymous even if it doesn't implement IAllowAnonymous in older versions.
        return endpoint.Metadata.Any(m => string.Equals(m.GetType().Name, "AllowAnonymousAttribute", StringComparison.Ordinal));
    }

    private static string? TryGeneratePermissionId(string? rawRoute, string? httpMethod)
    {
        if (string.IsNullOrWhiteSpace(rawRoute))
            return null;

        var segments = rawRoute.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length < 2)
            return null;

        if (!string.Equals(segments[0], "api", StringComparison.OrdinalIgnoreCase))
            return null;

        // Support both routing styles:
        // - /api/{service}/...
        // - /api/v1/{service}/...
        var serviceIndex = 1;
        if (segments.Length >= 3 && IsVersionSegment(segments[1]))
            serviceIndex = 2;

        var service = NormalizeSegment(segments[serviceIndex]);
        if (string.IsNullOrWhiteSpace(service))
            return null;

        string? resource = null;
        var resourceIndex = serviceIndex + 1;

        // Support /api/{service}/v1/{resource}/... and /api/v1/{service}/v1/{resource}/...
        if (segments.Length > resourceIndex && IsVersionSegment(segments[resourceIndex]))
            resourceIndex++;

        if (segments.Length > resourceIndex && !IsRouteParameter(segments[resourceIndex]))
        {
            resource = NormalizeSegment(segments[resourceIndex]);
            if (string.IsNullOrWhiteSpace(resource))
                resource = null;
        }

        var action = httpMethod?.ToUpperInvariant() switch
        {
            "GET" or "HEAD" => "read",
            "POST" => "create",
            "PUT" or "PATCH" => "update",
            "DELETE" => "delete",
            _ => "execute"
        };

        return resource == null ? $"{service}.{action}" : $"{service}.{resource}.{action}";
    }

    private static bool IsRouteParameter(string segment) =>
        segment.StartsWith('{') && segment.EndsWith('}');

    private static bool IsVersionSegment(string segment) => VersionSegment.IsMatch(segment.Trim());

    private static string NormalizeSegment(string segment)
    {
        var s = segment.Trim().ToLowerInvariant();
        s = s.Replace('-', '_');
        s = UnsafeChars.Replace(s, "_").Trim('_');
        return s;
    }
}
