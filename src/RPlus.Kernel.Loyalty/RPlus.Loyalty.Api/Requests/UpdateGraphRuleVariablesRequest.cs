using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace RPlus.Loyalty.Api.Requests;

public sealed class UpdateGraphRuleVariablesRequest
{
    [Required]
    public JsonElement Variables { get; set; }
}
