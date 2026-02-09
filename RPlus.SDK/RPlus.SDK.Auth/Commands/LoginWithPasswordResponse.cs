using System;

#nullable enable
namespace RPlus.SDK.Auth.Commands;

public record LoginWithPasswordResponse(
    bool Success,
    string? AccessToken,
    string? RefreshToken,
    DateTime? AccessExpiresAt,
    DateTime? RefreshExpiresAt,
    string? Error,
    int? RetryAfterSeconds = null,
    string? UserId = null,
    string? NextAction = null,
    string? TempToken = null,
    string? RecoveryEmailMask = null);
