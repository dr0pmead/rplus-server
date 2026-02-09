using System.Collections.Generic;

namespace RPlus.SDK.Gateway.Models;

public class GatewayCluster
{
    public string ClusterId { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string? HealthCheckPath { get; set; }
    public string LoadBalancingPolicy { get; set; } = "RoundRobin";
}
