using System.Text.Json;

namespace RPlus.Organization.Api.Contracts;

public sealed record CreateOrgNodeRequest(
    Guid? ParentId,
    string Name,
    string Type,
    JsonDocument? Attributes);

public sealed record UpdateOrgNodeRequest(
    string? Name,
    JsonDocument? Attributes);

public sealed record MoveOrgNodeRequest(Guid? NewParentId);
