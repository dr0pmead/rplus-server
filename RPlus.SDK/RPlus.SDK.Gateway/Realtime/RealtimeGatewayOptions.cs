using System.Collections.Generic;

#nullable enable

namespace RPlus.SDK.Gateway.Realtime;

public sealed class RealtimeGatewayOptions
{
    public bool Enabled { get; set; } = false;
    public string FanoutChannel { get; set; } = "rplus:realtime:events";
    public RealtimeKafkaOptions Kafka { get; set; } = new();
    public Dictionary<string, RealtimeMappingDefinition> Mappings { get; set; } = new();
}

public sealed class RealtimeKafkaOptions
{
    public string GroupId { get; set; } = "rplus-realtime-gateway";
    public string[] Topics { get; set; } = [];
}

