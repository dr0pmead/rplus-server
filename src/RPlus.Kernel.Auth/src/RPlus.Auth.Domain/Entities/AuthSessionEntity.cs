using System;
using RPlus.SDK.Auth.Models;

#nullable enable
namespace RPlus.Auth.Domain.Entities;

public sealed class AuthSessionEntity : AuthSession
{
    public AuthUserEntity User { get; set; } = null!;
}
