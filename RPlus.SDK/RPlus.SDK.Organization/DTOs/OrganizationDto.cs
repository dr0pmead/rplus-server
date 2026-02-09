using System;
using System.Collections.Generic;

#nullable enable
namespace RPlus.SDK.Organization.DTOs;

public sealed record OrganizationDto(
    Guid Id,
    Guid? ParentId,
    string Name,
    string Description,
    string? MetadataJson,
    string? RulesJson,
    List<LeaderDto> Leaders,
    List<MemberDto> Members,
    DateTime CreatedAt,
    DateTime UpdatedAt);
