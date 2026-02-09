namespace RPlus.Gateway.Api.OpenApi;

public sealed class OpenApiProxyOptions
{
    public const string SectionName = "OpenApiProxy";

    public bool Enabled { get; set; }

    public Dictionary<string, OpenApiServiceOptions> Services { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class OpenApiServiceOptions
{
    /// <summary>Base URL of the internal service (HTTP/1.1 endpoint, inside docker network).</summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>Swagger document name (default: v1).</summary>
    public string DocName { get; set; } = "v1";
}

