using System;
using System.Collections.Generic;
using System.Text.Json;
using RPlus.SDK.Organization.DTOs;

namespace RPlus.Organization.Api.Contracts;

public sealed record UpdateOrganizationDto(
    string? Name,
    string? Description,
    JsonDocument? Metadata,
    JsonDocument? Rules,
    List<Guid>? Leaders,
    List<Guid>? Deputies,
    List<Guid>? Members);

public sealed record BatchUpdateRequest(List<BatchUpdateItemDto> Updates);

public sealed record UserProfilesRequest(List<Guid> UserIds);

public sealed record OrganizationUserProfileDto(
    Guid UserId,
    string FirstName,
    string LastName,
    string? MiddleName,
    string? PreferredName,
    string? AvatarId,
    string Status);
