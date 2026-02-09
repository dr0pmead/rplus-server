using System;

#nullable enable
namespace RPlus.SDK.Organization.DTOs;

public sealed record MemberDto(Guid UserId, UserProfileDto? Profile);
