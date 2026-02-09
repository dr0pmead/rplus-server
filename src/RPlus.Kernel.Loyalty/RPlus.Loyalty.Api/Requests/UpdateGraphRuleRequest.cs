using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace RPlus.Loyalty.Api.Requests;

public sealed class UpdateGraphRuleRequest
{
    [Required]
    public string Name { get; set; } = string.Empty;

    [Required]
    public string Topic { get; set; } = string.Empty;

    public int Priority { get; set; } = 100;

    public bool IsActive { get; set; } = true;

    public int? MaxExecutions { get; set; }

    [Required]
    public JsonElement Graph { get; set; }

    public JsonElement? Variables { get; set; }

    public string? Description { get; set; }
}
