using System;

namespace RPlus.Access.Api.Services;

public sealed class PermissionManifestOptions
{
    // Shared secret expected in gRPC metadata: x-rplus-service-secret
    public string? SharedSecret { get; set; }

    // Optional allowlist of service names allowed to publish manifests.
    public string[] AllowedServices { get; set; } = Array.Empty<string>();

    // Development convenience: allow manifest calls without SharedSecret if true.
    public bool AllowInDevelopmentWithoutSecret { get; set; } = true;
}

