using System.Collections.Generic;

namespace RPlus.Gateway.Domain.Entities;

public class GatewayCluster : RPlus.SDK.Gateway.Models.GatewayCluster
{
    public virtual ICollection<GatewayRoute> Routes { get; set; } = new List<GatewayRoute>();
}
