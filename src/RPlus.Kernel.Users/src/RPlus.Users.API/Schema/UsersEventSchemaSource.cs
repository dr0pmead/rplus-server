using RPlus.SDK.Eventing.SchemaRegistry;
using System;
using System.Collections.Generic;
using System.Text.Json;

namespace RPlus.Users.Api.Schema;

public sealed class UsersEventSchemaSource : IEventSchemaSource
{
    private readonly string _serviceName;
    private readonly string _version;

    public UsersEventSchemaSource()
    {
        _serviceName = "rplus-kernel-users";
        _version = typeof(UsersEventSchemaSource).Assembly.GetName().Version?.ToString() ?? "0.0.0";
    }

    public IReadOnlyList<EventSchemaDescriptor> GetSchemas()
    {
        return new[]
        {
            new EventSchemaDescriptor
            {
                EventType = "users.user.created.v1",
                Topic = "users.user.created.v1",
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
                          "required": ["UserId","Phone","Roles","CreatedAt"],
                          "properties": {
                            "UserId": { "type": "string", "format": "uuid" },
                            "Phone": { "type": "string" },
                            "Email": { "type": ["string","null"] },
                            "Roles": { "type": "array", "items": { "type": "string" } },
                            "CreatedAt": { "type": "string", "format": "date-time" }
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
                      "EventType": "users.user.created.v1",
                      "OccurredAt": "2026-01-01T00:00:00Z",
                      "Source": "rplus.users",
                      "AggregateId": "00000000-0000-0000-0000-000000000000",
                      "Metadata": {},
                      "Payload": {
                        "UserId": "00000000-0000-0000-0000-000000000000",
                        "Phone": "+77778889900",
                        "Email": null,
                        "Roles": ["Admin"],
                        "CreatedAt": "2026-01-01T00:00:00Z"
                      }
                    }
                    """),
                Hints = new EventSchemaHints
                {
                    IsEventEnvelope = true,
                    EnvelopePayloadPath = "Payload",
                    SubjectIdPath = "Payload.UserId",
                    OccurredAtPath = "Payload.CreatedAt",
                    OperationIdPath = "EventId",
                    PiiPaths = new[] { "Payload.Phone", "Payload.Email" },
                    Metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["roles"] = "Payload.Roles"
                    }
                },
                Tags = new[] { "users", "registration", "loyalty" }
            },
            new EventSchemaDescriptor
            {
                EventType = "users.qr.issued.v1",
                Topic = "users.qr.issued.v1",
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
                          "required": ["UserId","Token","ExpiresAt"],
                          "properties": {
                            "UserId": { "type": "string", "format": "uuid" },
                            "Token": { "type": "string" },
                            "ExpiresAt": { "type": "string", "format": "date-time" }
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
                      "EventType": "users.qr.issued.v1",
                      "OccurredAt": "2026-01-01T00:00:00Z",
                      "Source": "rplus.users",
                      "AggregateId": "00000000-0000-0000-0000-000000000000",
                      "Metadata": {},
                      "Payload": {
                        "UserId": "00000000-0000-0000-0000-000000000000",
                        "Token": "token",
                        "ExpiresAt": "2026-01-01T00:00:30Z"
                      }
                    }
                    """),
                Hints = new EventSchemaHints
                {
                    IsEventEnvelope = true,
                    EnvelopePayloadPath = "Payload",
                    SubjectIdPath = "Payload.UserId",
                    OccurredAtPath = "OccurredAt",
                    OperationIdPath = "EventId"
                },
                Tags = new[] { "users", "qr" }
            },
            new EventSchemaDescriptor
            {
                EventType = "users.user.updated.v1",
                Topic = "users.user.updated.v1",
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
                          "required": ["UserId","UpdatedAt"],
                          "properties": {
                            "UserId": { "type": "string", "format": "uuid" },
                            "UpdatedAt": { "type": "string", "format": "date-time" },
                            "Fields": { "type": "array", "items": { "type": "string" } }
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
                      "EventType": "users.user.updated.v1",
                      "OccurredAt": "2026-01-01T00:00:00Z",
                      "Source": "rplus.users",
                      "AggregateId": "00000000-0000-0000-0000-000000000000",
                      "Metadata": {},
                      "Payload": {
                        "UserId": "00000000-0000-0000-0000-000000000000",
                        "UpdatedAt": "2026-01-01T00:00:00Z",
                        "Fields": ["firstName","lastName"]
                      }
                    }
                    """),
                Hints = new EventSchemaHints
                {
                    IsEventEnvelope = true,
                    EnvelopePayloadPath = "Payload",
                    SubjectIdPath = "Payload.UserId",
                    OccurredAtPath = "Payload.UpdatedAt",
                    OperationIdPath = "EventId"
                },
                Tags = new[] { "users", "profile", "loyalty" }
            },
            new EventSchemaDescriptor
            {
                EventType = "users.phone.verified.v1",
                Topic = "users.phone.verified.v1",
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
                          "required": ["UserId","Phone","VerifiedAt"],
                          "properties": {
                            "UserId": { "type": "string", "format": "uuid" },
                            "Phone": { "type": "string" },
                            "VerifiedAt": { "type": "string", "format": "date-time" }
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
                      "EventType": "users.phone.verified.v1",
                      "OccurredAt": "2026-01-01T00:00:00Z",
                      "Source": "rplus.users",
                      "AggregateId": "00000000-0000-0000-0000-000000000000",
                      "Metadata": {},
                      "Payload": {
                        "UserId": "00000000-0000-0000-0000-000000000000",
                        "Phone": "+77778889900",
                        "VerifiedAt": "2026-01-01T00:00:00Z"
                      }
                    }
                    """),
                Hints = new EventSchemaHints
                {
                    IsEventEnvelope = true,
                    EnvelopePayloadPath = "Payload",
                    SubjectIdPath = "Payload.UserId",
                    OccurredAtPath = "Payload.VerifiedAt",
                    OperationIdPath = "EventId",
                    PiiPaths = new[] { "Payload.Phone" }
                },
                Tags = new[] { "users", "verification", "loyalty" }
            },
            new EventSchemaDescriptor
            {
                EventType = "users.email.verified.v1",
                Topic = "users.email.verified.v1",
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
                          "required": ["UserId","Email","VerifiedAt"],
                          "properties": {
                            "UserId": { "type": "string", "format": "uuid" },
                            "Email": { "type": "string" },
                            "VerifiedAt": { "type": "string", "format": "date-time" }
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
                      "EventType": "users.email.verified.v1",
                      "OccurredAt": "2026-01-01T00:00:00Z",
                      "Source": "rplus.users",
                      "AggregateId": "00000000-0000-0000-0000-000000000000",
                      "Metadata": {},
                      "Payload": {
                        "UserId": "00000000-0000-0000-0000-000000000000",
                        "Email": "user@example.com",
                        "VerifiedAt": "2026-01-01T00:00:00Z"
                      }
                    }
                    """),
                Hints = new EventSchemaHints
                {
                    IsEventEnvelope = true,
                    EnvelopePayloadPath = "Payload",
                    SubjectIdPath = "Payload.UserId",
                    OccurredAtPath = "Payload.VerifiedAt",
                    OperationIdPath = "EventId",
                    PiiPaths = new[] { "Payload.Email" }
                },
                Tags = new[] { "users", "verification", "loyalty" }
            },
            new EventSchemaDescriptor
            {
                EventType = "users.segment.added.v1",
                Topic = "users.segment.added.v1",
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
                          "required": ["UserId","SegmentId","AddedAt"],
                          "properties": {
                            "UserId": { "type": "string", "format": "uuid" },
                            "SegmentId": { "type": "string" },
                            "SegmentName": { "type": ["string","null"] },
                            "AddedAt": { "type": "string", "format": "date-time" }
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
                      "EventType": "users.segment.added.v1",
                      "OccurredAt": "2026-01-01T00:00:00Z",
                      "Source": "rplus.users",
                      "AggregateId": "segment-1",
                      "Metadata": {},
                      "Payload": {
                        "UserId": "00000000-0000-0000-0000-000000000000",
                        "SegmentId": "segment-1",
                        "SegmentName": "VIP",
                        "AddedAt": "2026-01-01T00:00:00Z"
                      }
                    }
                    """),
                Hints = new EventSchemaHints
                {
                    IsEventEnvelope = true,
                    EnvelopePayloadPath = "Payload",
                    SubjectIdPath = "Payload.UserId",
                    OccurredAtPath = "Payload.AddedAt",
                    OperationIdPath = "EventId"
                },
                Tags = new[] { "users", "segment", "loyalty" }
            },
            new EventSchemaDescriptor
            {
                EventType = "users.segment.removed.v1",
                Topic = "users.segment.removed.v1",
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
                          "required": ["UserId","SegmentId","RemovedAt"],
                          "properties": {
                            "UserId": { "type": "string", "format": "uuid" },
                            "SegmentId": { "type": "string" },
                            "SegmentName": { "type": ["string","null"] },
                            "RemovedAt": { "type": "string", "format": "date-time" }
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
                      "EventType": "users.segment.removed.v1",
                      "OccurredAt": "2026-01-01T00:00:00Z",
                      "Source": "rplus.users",
                      "AggregateId": "segment-1",
                      "Metadata": {},
                      "Payload": {
                        "UserId": "00000000-0000-0000-0000-000000000000",
                        "SegmentId": "segment-1",
                        "SegmentName": "VIP",
                        "RemovedAt": "2026-01-01T00:00:00Z"
                      }
                    }
                    """),
                Hints = new EventSchemaHints
                {
                    IsEventEnvelope = true,
                    EnvelopePayloadPath = "Payload",
                    SubjectIdPath = "Payload.UserId",
                    OccurredAtPath = "Payload.RemovedAt",
                    OperationIdPath = "EventId"
                },
                Tags = new[] { "users", "segment", "loyalty" }
            },
            new EventSchemaDescriptor
            {
                EventType = "users.preference.updated.v1",
                Topic = "users.preference.updated.v1",
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
                          "required": ["UserId","Key","UpdatedAt"],
                          "properties": {
                            "UserId": { "type": "string", "format": "uuid" },
                            "Key": { "type": "string" },
                            "Value": { "type": ["string","null"] },
                            "UpdatedAt": { "type": "string", "format": "date-time" }
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
                      "EventType": "users.preference.updated.v1",
                      "OccurredAt": "2026-01-01T00:00:00Z",
                      "Source": "rplus.users",
                      "AggregateId": "00000000-0000-0000-0000-000000000000",
                      "Metadata": {},
                      "Payload": {
                        "UserId": "00000000-0000-0000-0000-000000000000",
                        "Key": "marketing.email.opt_in",
                        "Value": "true",
                        "UpdatedAt": "2026-01-01T00:00:00Z"
                      }
                    }
                    """),
                Hints = new EventSchemaHints
                {
                    IsEventEnvelope = true,
                    EnvelopePayloadPath = "Payload",
                    SubjectIdPath = "Payload.UserId",
                    OccurredAtPath = "Payload.UpdatedAt",
                    OperationIdPath = "EventId"
                },
                Tags = new[] { "users", "preferences", "loyalty" }
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
