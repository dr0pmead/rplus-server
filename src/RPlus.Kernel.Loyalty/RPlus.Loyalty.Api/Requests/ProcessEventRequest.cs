using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace RPlus.Loyalty.Api.Requests;

public class ProcessEventRequest
{
    [Required]
    public string EventType { get; set; } = string.Empty;

    public string? OperationId { get; set; }

    public Dictionary<string, string> Metadata { get; set; } = new();

    public string Source { get; set; } = "api";

    public DateTime? OccurredAt { get; set; }

    public string? Payload { get; set; }
}
