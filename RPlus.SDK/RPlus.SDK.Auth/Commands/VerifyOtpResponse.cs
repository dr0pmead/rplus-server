using RPlus.SDK.Auth.Enums;
using RPlus.SDK.Auth.Models;

#nullable enable
namespace RPlus.SDK.Auth.Commands;

public record VerifyOtpResponse(
    OtpVerifyStatus Status,
    AuthUser? User = null,
    Device? Device = null,
    string? PhoneHash = null);
