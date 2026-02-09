using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using RPlus.Loyalty.Api.Options;
using RPlus.Loyalty.Application.Handlers;
using RPlus.SDK.Eventing.SchemaRegistry;
using RPlus.SDK.Infrastructure.SchemaRegistry;
using RPlus.SDK.Loyalty.Results;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace RPlus.Loyalty.Api.Controllers;

[ApiController]
[Produces("application/json")]
[Route("api/loyalty/ingress")]
public sealed class IngressTestController : ControllerBase
{
    private const int MaxValueBytes = 256 * 1024;

    private readonly IMediator _mediator;
    private readonly IOptionsMonitor<LoyaltyIngressTestOptions> _options;
    private readonly IEventSchemaRegistryReader _registry;

    public IngressTestController(IMediator mediator, IOptionsMonitor<LoyaltyIngressTestOptions> options, IEventSchemaRegistryReader registry)
    {
        _mediator = mediator;
        _options = options;
        _registry = registry;
    }

    [HttpPost("test")]
    [ProducesResponseType(typeof(LoyaltyEventProcessResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(LoyaltyEventProcessResult), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Test([FromBody] IngressTestRequest request, CancellationToken ct)
    {
        var opts = _options.CurrentValue ?? new LoyaltyIngressTestOptions();
        if (!opts.Enabled)
            return NotFound();

        if (request is null)
            return BadRequest(new { error = "INVALID_REQUEST" });

        var topic = (request.Topic ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(topic) || topic.Length > 200)
            return BadRequest(new { error = "INVALID_TOPIC" });

        var key = (request.Key ?? string.Empty).Trim();
        if (key.Length > 256)
            return BadRequest(new { error = "INVALID_KEY" });

        var valueJson = request.Value.ValueKind == JsonValueKind.Undefined ? "{}" : request.Value.GetRawText();
        if (valueJson.Length > MaxValueBytes)
            return BadRequest(new { error = "VALUE_TOO_LARGE" });

        var schemas = await ResolveSchemasAsync(topic, request.Schema, ct);
        if (schemas.Count == 0)
            return BadRequest(new { error = "SCHEMA_NOT_FOUND" });

        var result = await _mediator.Send(
            new ProcessLoyaltyIngressEventCommand(topic, key, valueJson, schemas),
            ct);

        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }

    private async Task<IReadOnlyList<EventSchemaDescriptor>> ResolveSchemasAsync(string topic, EventSchemaDescriptorDto? overrideSchema, CancellationToken ct)
    {
        if (overrideSchema != null)
        {
            return new[]
            {
                new EventSchemaDescriptor
                {
                    EventType = string.IsNullOrWhiteSpace(overrideSchema.EventType) ? topic : overrideSchema.EventType.Trim(),
                    Topic = topic,
                    ProducerService = string.IsNullOrWhiteSpace(overrideSchema.ProducerService) ? "manual" : overrideSchema.ProducerService.Trim(),
                    ProducerVersion = string.IsNullOrWhiteSpace(overrideSchema.ProducerVersion) ? "0" : overrideSchema.ProducerVersion.Trim(),
                    SchemaVersion = overrideSchema.SchemaVersion <= 0 ? 1 : overrideSchema.SchemaVersion,
                    JsonSchema = JsonDocument.Parse("{}").RootElement,
                    Hints = new EventSchemaHints
                    {
                        IsEventEnvelope = overrideSchema.Hints?.IsEventEnvelope ?? false,
                        EnvelopePayloadPath = overrideSchema.Hints?.EnvelopePayloadPath,
                        SubjectIdPath = overrideSchema.Hints?.SubjectIdPath,
                        OccurredAtPath = overrideSchema.Hints?.OccurredAtPath,
                        OperationIdPath = overrideSchema.Hints?.OperationIdPath,
                        PiiPaths = overrideSchema.Hints?.PiiPaths ?? Array.Empty<string>(),
                        Metadata = overrideSchema.Hints?.Metadata ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    },
                    Tags = overrideSchema.Tags ?? Array.Empty<string>()
                }
            };
        }

        var all = await _registry.GetAllAsync(ct);
        return all.Where(s => string.Equals(s.Topic, topic, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    public sealed record IngressTestRequest
    {
        public string Topic { get; init; } = string.Empty;
        public string? Key { get; init; }
        public JsonElement Value { get; init; }
        public EventSchemaDescriptorDto? Schema { get; init; }
    }

    public sealed record EventSchemaDescriptorDto
    {
        public string? EventType { get; init; }
        public string? ProducerService { get; init; }
        public string? ProducerVersion { get; init; }
        public int SchemaVersion { get; init; } = 1;
        public EventSchemaHintsDto? Hints { get; init; }
        public string[]? Tags { get; init; }
    }

    public sealed record EventSchemaHintsDto
    {
        public bool IsEventEnvelope { get; init; }
        public string? EnvelopePayloadPath { get; init; }
        public string? SubjectIdPath { get; init; }
        public string? OccurredAtPath { get; init; }
        public string? OperationIdPath { get; init; }
        public string[]? PiiPaths { get; init; }
        public Dictionary<string, string>? Metadata { get; init; }
    }
}

