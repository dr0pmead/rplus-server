using System;

#nullable enable
namespace RPlus.SDK.Organization.DTOs;

public sealed record UserProfileDto(
    Guid UserId,
    string? Login,
    string? Email,
    string? Phone,
    string? FirstName,
    string? LastName,
    string? MiddleName);
