using System;
using RPlus.SDK.Auth.Models;
using RPlus.SDK.Auth.Enums;

#nullable enable
namespace RPlus.Auth.Domain.Entities;

public sealed class AuthUserEntity : AuthUser
{
    public string PhoneHash { get; set; } = string.Empty;
    public string PhoneEncrypted { get; set; } = string.Empty;
    public DateTime? LastOtpSentAt { get; set; }
    public string? RegistrationIp { get; set; }
    public string? RegistrationUserAgent { get; set; }
    public string? RegistrationDeviceId { get; set; }

    // System Admin lifecycle flags (used only for IsSystem users).
    public bool IsSystem { get; set; }
    public string? RecoveryEmail { get; set; }
    public bool IsTwoFactorEnabled { get; set; }
    public string? TotpSecretEncrypted { get; set; }
    public bool RequiresPasswordChange { get; set; }
    public bool RequiresSetup { get; set; }
}
