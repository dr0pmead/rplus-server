using System;

namespace RPlus.SDK.Infrastructure.Access.PermissionDiscovery;

public sealed class PermissionManifestPublisherOptions
{
    public const string SectionName = "RPlus:Access:PermissionDiscovery";

    // Access gRPC endpoint (h2c), e.g. http://rplus-kernel-access:5003
    public string AccessGrpcAddress { get; set; } = "http://rplus-kernel-access:5003";

    // Logical service name, e.g. "wallet"
    public string ServiceName { get; set; } = string.Empty;

    // Logical application code, e.g. "web-admin-ui"
    public string ApplicationId { get; set; } = "system";

    // gRPC metadata value for x-rplus-service-secret (optional in development).
    public string? SharedSecret { get; set; }

    public bool MarkMissingAsDeprecated { get; set; } = true;

    public bool Enabled { get; set; } = true;

    // Optional periodic republish interval. 0 = publish once at startup.
    public int PublishIntervalSeconds { get; set; } = 0;
}
