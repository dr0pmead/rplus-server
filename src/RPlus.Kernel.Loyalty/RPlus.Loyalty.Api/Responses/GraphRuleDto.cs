using System;
using System.Text.Json;

namespace RPlus.Loyalty.Api.Responses;

public sealed class GraphRuleDto
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Topic { get; set; } = string.Empty;

    public int Priority { get; set; }

    public bool IsActive { get; set; }

    public int? MaxExecutions { get; set; }

    public int ExecutionsCount { get; set; }

    public JsonElement Graph { get; set; }

    public JsonElement Variables { get; set; }

    public bool IsSystem { get; set; }

    public string? SystemKey { get; set; }

    public string? Description { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}
