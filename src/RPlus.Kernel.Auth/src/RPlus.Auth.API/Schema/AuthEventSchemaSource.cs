using RPlus.SDK.Eventing.SchemaRegistry;
using System;
using System.Collections.Generic;
using System.Text.Json;

namespace RPlus.Auth.Api.Schema;

public sealed class AuthEventSchemaSource : IEventSchemaSource
{
    private readonly string _serviceName;
    private readonly string _version;

    public AuthEventSchemaSource()
    {
        _serviceName = "rplus-kernel-auth";
        _version = typeof(AuthEventSchemaSource).Assembly.GetName().Version?.ToString() ?? "0.0.0";
    }

    public IReadOnlyList<EventSchemaDescriptor> GetSchemas()
    {
        return new[]
        {
            new EventSchemaDescriptor
            {
                EventType = "auth.user.logged_in.v1",
                Topic = "auth.user.logged_in.v1",
                ProducerService = _serviceName,
                ProducerVersion = _version,
                SchemaVersion = 1,
                JsonSchema = Parse(
                    """
                    {
                      "$schema": "https://json-schema.org/draft/2020-12/schema",
                      "type": "object",
                      "required": ["UserId","DeviceId","AuthMethod","IpAddress","LoggedInAt"],
                      "properties": {
                        "UserId": { "type": "string", "format": "uuid" },
                        "DeviceId": { "type": "string" },
                        "AuthMethod": { "type": "string" },
                        "IpAddress": { "type": "string" },
                        "UserAgent": { "type": ["string","null"] },
                        "LoggedInAt": { "type": "string", "format": "date-time" }
                      }
                    }
                    """),
                Example = ParseNullable(
                    """
                    {
                      "UserId": "00000000-0000-0000-0000-000000000000",
                      "DeviceId": "web-admin-panel",
                      "AuthMethod": "Otp",
                      "IpAddress": "127.0.0.1",
                      "UserAgent": "Mozilla/5.0",
                      "LoggedInAt": "2026-01-01T00:00:00Z"
                    }
                    """),
                Hints = new EventSchemaHints
                {
                    IsEventEnvelope = false,
                    SubjectIdPath = "UserId",
                    OccurredAtPath = "LoggedInAt",
                    OperationIdPath = null,
                    PiiPaths = new[] { "IpAddress", "UserAgent" },
                    Metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["deviceId"] = "DeviceId",
                        ["authMethod"] = "AuthMethod"
                    }
                },
                Tags = new[] { "auth", "login", "loyalty" }
            },
            new EventSchemaDescriptor
            {
                EventType = "auth.user.login_failed.v1",
                Topic = "auth.user.login_failed.v1",
                ProducerService = _serviceName,
                ProducerVersion = _version,
                SchemaVersion = 1,
                JsonSchema = Parse(
                    """
                    {
                      "$schema": "https://json-schema.org/draft/2020-12/schema",
                      "type": "object",
                      "required": ["Identifier","Reason","IpAddress","OccurredAt"],
                      "properties": {
                        "UserId": { "type": ["string","null"], "format": "uuid" },
                        "Identifier": { "type": "string" },
                        "Reason": { "type": "string" },
                        "IpAddress": { "type": "string" },
                        "UserAgent": { "type": ["string","null"] },
                        "OccurredAt": { "type": "string", "format": "date-time" }
                      }
                    }
                    """),
                Example = ParseNullable(
                    """
                    {
                      "UserId": null,
                      "Identifier": "+77778889900",
                      "Reason": "invalid_credentials",
                      "IpAddress": "127.0.0.1",
                      "UserAgent": "Mozilla/5.0",
                      "OccurredAt": "2026-01-01T00:00:00Z"
                    }
                    """),
                Hints = new EventSchemaHints
                {
                    IsEventEnvelope = false,
                    SubjectIdPath = "UserId",
                    OccurredAtPath = "OccurredAt",
                    OperationIdPath = null,
                    PiiPaths = new[] { "Identifier", "IpAddress", "UserAgent" },
                    Metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["reason"] = "Reason"
                    }
                },
                Tags = new[] { "auth", "security" }
            },
            new EventSchemaDescriptor
            {
                EventType = "auth.user.logged_out.v1",
                Topic = "auth.user.logged_out.v1",
                ProducerService = _serviceName,
                ProducerVersion = _version,
                SchemaVersion = 1,
                JsonSchema = Parse(
                    """
                    {
                      "$schema": "https://json-schema.org/draft/2020-12/schema",
                      "type": "object",
                      "required": ["UserId","DeviceId","IpAddress","LoggedOutAt"],
                      "properties": {
                        "UserId": { "type": "string", "format": "uuid" },
                        "DeviceId": { "type": "string" },
                        "IpAddress": { "type": "string" },
                        "UserAgent": { "type": ["string","null"] },
                        "LoggedOutAt": { "type": "string", "format": "date-time" }
                      }
                    }
                    """),
                Example = ParseNullable(
                    """
                    {
                      "UserId": "00000000-0000-0000-0000-000000000000",
                      "DeviceId": "web-admin-panel",
                      "IpAddress": "127.0.0.1",
                      "UserAgent": "Mozilla/5.0",
                      "LoggedOutAt": "2026-01-01T00:00:00Z"
                    }
                    """),
                Hints = new EventSchemaHints
                {
                    IsEventEnvelope = false,
                    SubjectIdPath = "UserId",
                    OccurredAtPath = "LoggedOutAt",
                    OperationIdPath = null,
                    PiiPaths = new[] { "IpAddress", "UserAgent" }
                },
                Tags = new[] { "auth", "session" }
            },
            new EventSchemaDescriptor
            {
                EventType = "auth.user.otp.verified.v1",
                Topic = "auth.user.otp.verified.v1",
                ProducerService = _serviceName,
                ProducerVersion = _version,
                SchemaVersion = 1,
                JsonSchema = Parse(
                    """
                    {
                      "$schema": "https://json-schema.org/draft/2020-12/schema",
                      "type": "object",
                      "required": ["Identifier","IpAddress","VerifiedAt"],
                      "properties": {
                        "UserId": { "type": ["string","null"], "format": "uuid" },
                        "Identifier": { "type": "string" },
                        "IpAddress": { "type": "string" },
                        "VerifiedAt": { "type": "string", "format": "date-time" }
                      }
                    }
                    """),
                Example = ParseNullable(
                    """
                    {
                      "UserId": "00000000-0000-0000-0000-000000000000",
                      "Identifier": "+77778889900",
                      "IpAddress": "127.0.0.1",
                      "VerifiedAt": "2026-01-01T00:00:00Z"
                    }
                    """),
                Hints = new EventSchemaHints
                {
                    IsEventEnvelope = false,
                    SubjectIdPath = "UserId",
                    OccurredAtPath = "VerifiedAt",
                    OperationIdPath = null,
                    PiiPaths = new[] { "Identifier", "IpAddress" }
                },
                Tags = new[] { "auth", "otp", "loyalty" }
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
