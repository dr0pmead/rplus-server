using System;

#nullable enable
namespace RPlus.SDK.Organization.DTOs;

public sealed record OrganizationTreeSummaryDto(
    Guid Id,
    Guid? ParentId,
    string Name,
    int LeadersCount,
    int MembersCount,
    string? SemanticLabel,
    Guid? PrimaryLeaderUserId,
    UserProfileDto? PrimaryLeaderProfile);
