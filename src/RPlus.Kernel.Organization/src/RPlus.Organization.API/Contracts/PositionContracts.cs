using System.Text.Json;

namespace RPlus.Organization.Api.Contracts;

public sealed record CreatePositionRequest(
    Guid NodeId,
    string Title,
    int Level,
    Guid? ReportsToPositionId,
    JsonDocument? Attributes);

