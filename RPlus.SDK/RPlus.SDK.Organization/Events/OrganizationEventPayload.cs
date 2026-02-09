using System;
using System.Collections.Generic;

#nullable enable
namespace RPlus.SDK.Organization.Events;

public sealed record OrganizationEventPayload(
    Guid OrganizationId,
    Guid? ParentId,
    string Name,
    string Description,
    string? MetadataJson,
    string? RulesJson,
    List<OrganizationLeaderBrief> Leaders,
    List<Guid> Members,
    DateTime OccurredAt);
