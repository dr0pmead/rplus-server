using System;
using RPlus.SDK.Organization.Enums;

#nullable enable
namespace RPlus.SDK.Organization.DTOs;

public sealed record LeaderDto(Guid UserId, LeaderRole Role, UserProfileDto? Profile);
