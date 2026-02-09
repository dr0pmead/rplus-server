using System;
using System.Collections.Generic;
using System.Text.Json;

namespace RPlus.SDK.Eventing.SchemaRegistry;

public sealed record EventSchemaDescriptor
{
    public required string EventType { get; init; }

    /// <summary>Kafka topic that carries this event (often equals <see cref="EventType"/>).</summary>
    public required string Topic { get; init; }

    public required string ProducerService { get; init; }

    public required string ProducerVersion { get; init; }

    public int SchemaVersion { get; init; } = 1;

    /// <summary>JSON Schema (Draft 2020-12 recommended).</summary>
    public JsonElement JsonSchema { get; init; }

    /// <summary>Optional example event payload.</summary>
    public JsonElement? Example { get; init; }

    public EventSchemaHints Hints { get; init; } = new();

    public string[] Tags { get; init; } = Array.Empty<string>();
}

public sealed record EventSchemaHints
{
    /// <summary>
    /// True if the message on the topic is wrapped in <c>EventEnvelope&lt;T&gt;</c>.
    /// </summary>
    public bool IsEventEnvelope { get; init; }

    /// <summary>
    /// JSON path to the payload object (default "Payload") when <see cref="IsEventEnvelope"/> is true.
    /// </summary>
    public string? EnvelopePayloadPath { get; init; } = "Payload";

    /// <summary>JSON path to the subject id (user id) string.</summary>
    public string? SubjectIdPath { get; init; }

    /// <summary>JSON path to occurred-at timestamp (RFC3339 string) or unix ms.</summary>
    public string? OccurredAtPath { get; init; }

    /// <summary>JSON path to operation id / dedupe key.</summary>
    public string? OperationIdPath { get; init; }

    /// <summary>Optional hint for sensitive fields.</summary>
    public string[] PiiPaths { get; init; } = Array.Empty<string>();

    /// <summary>Optional metadata projections: metadata key -> json path.</summary>
    public Dictionary<string, string> Metadata { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}

