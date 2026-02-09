using System;
using RPlus.SDK.Auth.Enums;

#nullable enable
namespace RPlus.SDK.Auth.Models;

public class AuthUser
{
    public Guid Id { get; set; }
    public string? Login { get; set; }
    public string? Email { get; set; }
    public AuthUserType UserType { get; set; } = AuthUserType.Platform;
    public Guid TenantId { get; set; }
    public bool IsBlocked { get; set; }
    public string? BlockReason { get; set; }
    public DateTime? BlockedAt { get; set; }
    public DateTime? LockedUntil { get; set; }
    public int PasswordVersion { get; set; } = 1;
    public int SecurityVersion { get; set; } = 1;
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public int FailedAttempts { get; set; }
}

public class AuthCredential
{
    public Guid UserId { get; set; }
    public byte[] PasswordHash { get; set; } = Array.Empty<byte>();
    public byte[] PasswordSalt { get; set; } = Array.Empty<byte>();
    public DateTime ChangedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class Device
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string DeviceKey { get; set; } = string.Empty;
    public string? PublicJwk { get; set; }
    public bool IsBlocked { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime LastSeenAt { get; set; }
}

public class AuthSession
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string DeviceId { get; set; } = string.Empty;
    public string DeviceFingerprint { get; set; } = string.Empty;
    public string? DeviceName { get; set; }
    public string? DeviceOs { get; set; }
    public DateTime IssuedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? LastActivityAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public string? RevokeReason { get; set; }
    public string IssuerIp { get; set; } = string.Empty;
    public string IssuerUserAgent { get; set; } = string.Empty;
    public string? IssuerLocation { get; set; }
    public string RiskLevel { get; set; } = "low";
    public int RiskScore { get; set; }
    public bool IsSuspicious { get; set; }
    public string? SuspiciousActivityDetails { get; set; }
    public bool RequiresMfa { get; set; }
}
