using System;

namespace RPlus.Gateway.Api.Services;

public sealed class ProxyAuthorizationOptions
{
    public bool Enabled { get; set; } = true;

    // In Development you may set this to true to avoid blocking UI when Access is restarting.
    public bool FailOpenOnAccessUnavailable { get; set; } = false;

    // Small cache to protect Access from bursts; set to 0 for fully realtime decisions.
    public int DecisionCacheSeconds { get; set; } = 1;

    public string DefaultApplicationId { get; set; } = "web-admin-ui";

    public string[] AllowedApplicationIds { get; set; } = Array.Empty<string>();

    public string TenantIdHeaderName { get; set; } = "X-Tenant-Id";

    public string ApplicationIdHeaderName { get; set; } = "X-App-Id";
}

