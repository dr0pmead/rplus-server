using System;

namespace RPlus.SDK.Gateway.Models;

public class GatewayRoute
{
    public string RouteId { get; set; } = string.Empty;
    public string ClusterId { get; set; } = string.Empty;
    public string PathPattern { get; set; } = string.Empty;
    public string[] Methods { get; set; } = Array.Empty<string>();
    public string AuthPolicy { get; set; } = "Default";
    public string? AccessPolicy { get; set; }
    public bool IsEnabled { get; set; } = true;
    public int Priority { get; set; }
}
