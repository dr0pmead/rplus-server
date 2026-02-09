using System;

#nullable enable
namespace RPlus.Auth.Api.Controllers;

public sealed record UpdateUserRequest
{
    public string? Login { get; init; }
    public string? Email { get; init; }
    public string? Phone { get; init; }
}

