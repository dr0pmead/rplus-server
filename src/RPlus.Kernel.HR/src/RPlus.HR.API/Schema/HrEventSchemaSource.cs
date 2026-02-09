using RPlus.SDK.Eventing.SchemaRegistry;
using System;
using System.Collections.Generic;
using System.Text.Json;

namespace RPlus.HR.Api.Schema;

public sealed class HrEventSchemaSource : IEventSchemaSource
{
    private readonly string _serviceName;
    private readonly string _version;

    public HrEventSchemaSource()
    {
        _serviceName = "rplus-kernel-hr";
        _version = typeof(HrEventSchemaSource).Assembly.GetName().Version?.ToString() ?? "0.0.0";
    }

    public IReadOnlyList<EventSchemaDescriptor> GetSchemas()
    {
        return new[]
        {
            new EventSchemaDescriptor
            {
                EventType = "hr.employee.hired.v1",
                Topic = "hr.employee.hired.v1",
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
                          "required": ["EmployeeId","UserId","HiredAt"],
                          "properties": {
                            "EmployeeId": { "type": "string", "format": "uuid" },
                            "UserId": { "type": "string", "format": "uuid" },
                            "PositionId": { "type": ["string","null"] },
                            "DepartmentId": { "type": ["string","null"] },
                            "HiredAt": { "type": "string", "format": "date-time" }
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
                      "EventType": "hr.employee.hired.v1",
                      "OccurredAt": "2026-01-01T00:00:00Z",
                      "Source": "rplus.hr",
                      "AggregateId": "00000000-0000-0000-0000-000000000000",
                      "Metadata": {},
                      "Payload": {
                        "EmployeeId": "00000000-0000-0000-0000-000000000000",
                        "UserId": "00000000-0000-0000-0000-000000000000",
                        "PositionId": "position-1",
                        "DepartmentId": "department-1",
                        "HiredAt": "2026-01-01T00:00:00Z"
                      }
                    }
                    """),
                Hints = new EventSchemaHints
                {
                    IsEventEnvelope = true,
                    EnvelopePayloadPath = "Payload",
                    SubjectIdPath = "Payload.UserId",
                    OccurredAtPath = "Payload.HiredAt",
                    OperationIdPath = "EventId"
                },
                Tags = new[] { "hr", "staffing" }
            },
            new EventSchemaDescriptor
            {
                EventType = "hr.employee.fired.v1",
                Topic = "hr.employee.fired.v1",
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
                          "required": ["EmployeeId","UserId","FiredAt"],
                          "properties": {
                            "EmployeeId": { "type": "string", "format": "uuid" },
                            "UserId": { "type": "string", "format": "uuid" },
                            "Reason": { "type": ["string","null"] },
                            "FiredAt": { "type": "string", "format": "date-time" }
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
                      "EventType": "hr.employee.fired.v1",
                      "OccurredAt": "2026-01-01T00:00:00Z",
                      "Source": "rplus.hr",
                      "AggregateId": "00000000-0000-0000-0000-000000000000",
                      "Metadata": {},
                      "Payload": {
                        "EmployeeId": "00000000-0000-0000-0000-000000000000",
                        "UserId": "00000000-0000-0000-0000-000000000000",
                        "Reason": "terminated",
                        "FiredAt": "2026-01-01T00:00:00Z"
                      }
                    }
                    """),
                Hints = new EventSchemaHints
                {
                    IsEventEnvelope = true,
                    EnvelopePayloadPath = "Payload",
                    SubjectIdPath = "Payload.UserId",
                    OccurredAtPath = "Payload.FiredAt",
                    OperationIdPath = "EventId"
                },
                Tags = new[] { "hr", "staffing" }
            },
            new EventSchemaDescriptor
            {
                EventType = "hr.employee.position.changed.v1",
                Topic = "hr.employee.position.changed.v1",
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
                          "required": ["EmployeeId","UserId","PositionId","ChangedAt"],
                          "properties": {
                            "EmployeeId": { "type": "string", "format": "uuid" },
                            "UserId": { "type": "string", "format": "uuid" },
                            "PositionId": { "type": "string" },
                            "ChangedAt": { "type": "string", "format": "date-time" }
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
                      "EventType": "hr.employee.position.changed.v1",
                      "OccurredAt": "2026-01-01T00:00:00Z",
                      "Source": "rplus.hr",
                      "AggregateId": "00000000-0000-0000-0000-000000000000",
                      "Metadata": {},
                      "Payload": {
                        "EmployeeId": "00000000-0000-0000-0000-000000000000",
                        "UserId": "00000000-0000-0000-0000-000000000000",
                        "PositionId": "position-2",
                        "ChangedAt": "2026-01-01T00:00:00Z"
                      }
                    }
                    """),
                Hints = new EventSchemaHints
                {
                    IsEventEnvelope = true,
                    EnvelopePayloadPath = "Payload",
                    SubjectIdPath = "Payload.UserId",
                    OccurredAtPath = "Payload.ChangedAt",
                    OperationIdPath = "EventId"
                },
                Tags = new[] { "hr", "position" }
            },
            new EventSchemaDescriptor
            {
                EventType = "hr.employee.department.changed.v1",
                Topic = "hr.employee.department.changed.v1",
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
                          "required": ["EmployeeId","UserId","DepartmentId","ChangedAt"],
                          "properties": {
                            "EmployeeId": { "type": "string", "format": "uuid" },
                            "UserId": { "type": "string", "format": "uuid" },
                            "DepartmentId": { "type": "string" },
                            "ChangedAt": { "type": "string", "format": "date-time" }
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
                      "EventType": "hr.employee.department.changed.v1",
                      "OccurredAt": "2026-01-01T00:00:00Z",
                      "Source": "rplus.hr",
                      "AggregateId": "00000000-0000-0000-0000-000000000000",
                      "Metadata": {},
                      "Payload": {
                        "EmployeeId": "00000000-0000-0000-0000-000000000000",
                        "UserId": "00000000-0000-0000-0000-000000000000",
                        "DepartmentId": "department-2",
                        "ChangedAt": "2026-01-01T00:00:00Z"
                      }
                    }
                    """),
                Hints = new EventSchemaHints
                {
                    IsEventEnvelope = true,
                    EnvelopePayloadPath = "Payload",
                    SubjectIdPath = "Payload.UserId",
                    OccurredAtPath = "Payload.ChangedAt",
                    OperationIdPath = "EventId"
                },
                Tags = new[] { "hr", "department" }
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
