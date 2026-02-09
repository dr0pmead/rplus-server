namespace RPlus.Gateway.Domain.Entities;

public class GatewayRoute : RPlus.SDK.Gateway.Models.GatewayRoute
{
    public virtual GatewayCluster? Cluster { get; set; }
}
