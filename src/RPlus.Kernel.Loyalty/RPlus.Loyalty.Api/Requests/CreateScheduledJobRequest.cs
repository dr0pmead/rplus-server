using System;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace RPlus.Loyalty.Api.Requests;

public sealed class CreateScheduledJobRequest
{
    [Required]
    public Guid RuleId { get; set; }

    [Required]
    public Guid UserId { get; set; }

    /// <summary>UTC timestamp when the job should fire.</summary>
    [Required]
    public DateTime RunAtUtc { get; set; }

    public string? OperationId { get; set; }

    public string? EventType { get; set; }

    /// <summary>Optional JSON payload passed to JsonLogic under <c>payload</c>.</summary>
    public JsonElement? Payload { get; set; }
}

