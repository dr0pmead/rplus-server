using RPlus.SDK.Eventing.SchemaRegistry;
using System;
using System.Collections.Generic;
using System.Text.Json;

namespace RPlus.Kernel.Integration.Api.Schema;

public sealed class IntegrationEventSchemaSource : IEventSchemaSource
{
    private readonly string _serviceName;
    private readonly string _version;

    public IntegrationEventSchemaSource()
    {
        _serviceName = "rplus-kernel-integration";
        _version = typeof(IntegrationEventSchemaSource).Assembly.GetName().Version?.ToString() ?? "0.0.0";
    }

    public IReadOnlyList<EventSchemaDescriptor> GetSchemas()
    {
        return new[]
        {
            new EventSchemaDescriptor
            {
                EventType = "integration.scan.succeeded.v1",
                Topic = "integration.scan.succeeded.v1",
                ProducerService = _serviceName,
                ProducerVersion = _version,
                SchemaVersion = 1,
                JsonSchema = Parse(
                    """
                    {
                      "$schema": "https://json-schema.org/draft/2020-12/schema",
                      "type": "object",
                      "required": ["EventId","TraceId","EventType","OccurredAt","Source","AggregateId","Payload"],
                      "properties": {
                        "EventId": { "type": "string", "format": "uuid" },
                        "TraceId": { "type": "string", "format": "uuid" },
                        "EventType": { "type": "string" },
                        "OccurredAt": { "type": "string", "format": "date-time" },
                        "Source": { "type": "string" },
                        "AggregateId": { "type": "string" },
                        "Metadata": { "type": "object", "additionalProperties": { "type": "string" } },
                        "Payload": {
                          "type": "object",
                          "required": ["UserId","IntegrationId","PartnerId","KeyId","ContextType","ContextId","Status","OccurredAt"],
                          "properties": {
                            "UserId": { "type": "string", "format": "uuid" },
                            "IntegrationId": { "type": "string", "format": "uuid" },
                            "PartnerId": { "type": "string", "format": "uuid" },
                            "KeyId": { "type": "string", "format": "uuid" },
                            "ContextType": { "type": "string" },
                            "ContextId": { "type": "string" },
                            "Status": { "type": "string" },
                            "OccurredAt": { "type": "string", "format": "date-time" }
                          }
                        }
                      }
                    }
                    """),
                Example = ParseNullable(
                    """
                    {
                      "EventId": "00000000-0000-0000-0000-000000000000",
                      "TraceId": "00000000-0000-0000-0000-000000000000",
                      "EventType": "integration.scan.succeeded.v1",
                      "OccurredAt": "2026-01-01T00:00:00Z",
                      "Source": "rplus.integration",
                      "AggregateId": "00000000-0000-0000-0000-000000000000",
                      "Metadata": {},
                      "Payload": {
                        "UserId": "00000000-0000-0000-0000-000000000000",
                        "IntegrationId": "00000000-0000-0000-0000-000000000000",
                        "PartnerId": "00000000-0000-0000-0000-000000000000",
                        "KeyId": "00000000-0000-0000-0000-000000000000",
                        "ContextType": "partner",
                        "ContextId": "partner-ctx",
                        "Status": "ok",
                        "OccurredAt": "2026-01-01T00:00:00Z"
                      }
                    }
                    """),
                Hints = new EventSchemaHints
                {
                    IsEventEnvelope = true,
                    EnvelopePayloadPath = "Payload",
                    SubjectIdPath = "Payload.UserId",
                    OccurredAtPath = "Payload.OccurredAt",
                    OperationIdPath = "EventId"
                },
                Tags = new[] { "integration", "scan", "loyalty" }
            },
            new EventSchemaDescriptor
            {
                EventType = "integration.scan.failed.v1",
                Topic = "integration.scan.failed.v1",
                ProducerService = _serviceName,
                ProducerVersion = _version,
                SchemaVersion = 1,
                JsonSchema = Parse(
                    """
                    {
                      "$schema": "https://json-schema.org/draft/2020-12/schema",
                      "type": "object",
                      "required": ["EventId","TraceId","EventType","OccurredAt","Source","AggregateId","Payload"],
                      "properties": {
                        "EventId": { "type": "string", "format": "uuid" },
                        "TraceId": { "type": "string", "format": "uuid" },
                        "EventType": { "type": "string" },
                        "OccurredAt": { "type": "string", "format": "date-time" },
                        "Source": { "type": "string" },
                        "AggregateId": { "type": "string" },
                        "Metadata": { "type": "object", "additionalProperties": { "type": "string" } },
                        "Payload": {
                          "type": "object",
                          "required": ["IntegrationId","PartnerId","KeyId","ContextType","ContextId","Error","OccurredAt"],
                          "properties": {
                            "IntegrationId": { "type": "string", "format": "uuid" },
                            "PartnerId": { "type": "string", "format": "uuid" },
                            "KeyId": { "type": "string", "format": "uuid" },
                            "ContextType": { "type": "string" },
                            "ContextId": { "type": "string" },
                            "Error": { "type": "string" },
                            "OccurredAt": { "type": "string", "format": "date-time" }
                          }
                        }
                      }
                    }
                    """),
                Example = ParseNullable(
                    """
                    {
                      "EventId": "00000000-0000-0000-0000-000000000000",
                      "TraceId": "00000000-0000-0000-0000-000000000000",
                      "EventType": "integration.scan.failed.v1",
                      "OccurredAt": "2026-01-01T00:00:00Z",
                      "Source": "rplus.integration",
                      "AggregateId": "00000000-0000-0000-0000-000000000000",
                      "Metadata": {},
                      "Payload": {
                        "IntegrationId": "00000000-0000-0000-0000-000000000000",
                        "PartnerId": "00000000-0000-0000-0000-000000000000",
                        "KeyId": "00000000-0000-0000-0000-000000000000",
                        "ContextType": "partner",
                        "ContextId": "partner-ctx",
                        "Error": "invalid_qr",
                        "OccurredAt": "2026-01-01T00:00:00Z"
                      }
                    }
                    """),
                Hints = new EventSchemaHints
                {
                    IsEventEnvelope = true,
                    EnvelopePayloadPath = "Payload",
                    SubjectIdPath = null,
                    OccurredAtPath = "Payload.OccurredAt",
                    OperationIdPath = "EventId"
                },
                Tags = new[] { "integration", "scan" }
            }
        };
    }

    private static JsonElement Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }

    private static JsonElement? ParseNullable(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }
}
