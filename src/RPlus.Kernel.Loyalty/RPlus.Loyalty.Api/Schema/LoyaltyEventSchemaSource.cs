using RPlus.SDK.Contracts.Domain.Notifications;
using RPlus.SDK.Contracts.Domain.Social;
using RPlus.SDK.Eventing.SchemaRegistry;
using System;
using System.Collections.Generic;
using System.Text.Json;

namespace RPlus.Loyalty.Api.Schema;

public sealed class LoyaltyEventSchemaSource : IEventSchemaSource
{
    private readonly string _serviceName;
    private readonly string _version;

    public LoyaltyEventSchemaSource()
    {
        _serviceName = "rplus-loyalty";
        _version = typeof(LoyaltyEventSchemaSource).Assembly.GetName().Version?.ToString() ?? "0.0.0";
    }

    public IReadOnlyList<EventSchemaDescriptor> GetSchemas()
    {
        return new[]
        {
            new EventSchemaDescriptor
            {
                EventType = "system.cron.v1",
                Topic = "system.cron.v1",
                ProducerService = _serviceName,
                ProducerVersion = _version,
                SchemaVersion = 1,
                JsonSchema = Parse(
                    """
                    {
                      "$schema": "https://json-schema.org/draft/2020-12/schema",
                      "type": "object",
                      "required": ["EventType","EventId","OccurredAt","Payload"],
                      "properties": {
                        "EventType": { "type": "string" },
                        "EventId": { "type": "string" },
                        "OccurredAt": { "type": "string", "format": "date-time" },
                        "Payload": {
                          "type": "object",
                          "required": ["NowUtc"],
                          "properties": {
                            "NowUtc": { "type": "string", "format": "date-time" }
                          }
                        }
                      }
                    }
                    """),
                Hints = new EventSchemaHints
                {
                    IsEventEnvelope = true,
                    EnvelopePayloadPath = "Payload",
                    SubjectIdPath = null,
                    OccurredAtPath = "OccurredAt",
                    OperationIdPath = "EventId"
                },
                Tags = new[] { "system", "scheduler", "loyalty" }
            },
            new EventSchemaDescriptor
            {
                EventType = NotificationsEventTopics.DispatchRequested,
                Topic = NotificationsEventTopics.DispatchRequested,
                ProducerService = _serviceName,
                ProducerVersion = _version,
                SchemaVersion = 1,
                JsonSchema = Parse(
                    """
                    {
                      "$schema": "https://json-schema.org/draft/2020-12/schema",
                      "type": "object",
                      "required": ["EventId","EventType","OccurredAt","Source","AggregateId","Payload"],
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
                          "required": ["UserId","Channel","Title","Body","OperationId","RuleId","NodeId"],
                          "properties": {
                            "UserId": { "type": "string", "format": "uuid" },
                            "Channel": { "type": "string" },
                            "Title": { "type": "string" },
                            "Body": { "type": "string" },
                            "OperationId": { "type": "string" },
                            "RuleId": { "type": "string" },
                            "NodeId": { "type": "string" }
                          }
                        }
                      }
                    }
                    """),
                Hints = new EventSchemaHints
                {
                    IsEventEnvelope = true,
                    EnvelopePayloadPath = "Payload",
                    SubjectIdPath = "Payload.UserId",
                    OccurredAtPath = "OccurredAt",
                    OperationIdPath = "Payload.OperationId"
                },
                Tags = new[] { "notifications", "action" }
            },
            new EventSchemaDescriptor
            {
                EventType = SocialEventTopics.FeedPostRequested,
                Topic = SocialEventTopics.FeedPostRequested,
                ProducerService = _serviceName,
                ProducerVersion = _version,
                SchemaVersion = 1,
                JsonSchema = Parse(
                    """
                    {
                      "$schema": "https://json-schema.org/draft/2020-12/schema",
                      "type": "object",
                      "required": ["EventId","EventType","OccurredAt","Source","AggregateId","Payload"],
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
                          "required": ["UserId","Channel","Content","OperationId","RuleId","NodeId"],
                          "properties": {
                            "UserId": { "type": "string", "format": "uuid" },
                            "Channel": { "type": "string" },
                            "Content": { "type": "string" },
                            "OperationId": { "type": "string" },
                            "RuleId": { "type": "string" },
                            "NodeId": { "type": "string" }
                          }
                        }
                      }
                    }
                    """),
                Hints = new EventSchemaHints
                {
                    IsEventEnvelope = true,
                    EnvelopePayloadPath = "Payload",
                    SubjectIdPath = "Payload.UserId",
                    OccurredAtPath = "OccurredAt",
                    OperationIdPath = "Payload.OperationId"
                },
                Tags = new[] { "social", "action" }
            }
        };
    }

    private static JsonElement Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }
}
