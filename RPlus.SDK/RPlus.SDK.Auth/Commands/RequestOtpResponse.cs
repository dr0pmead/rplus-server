#nullable enable
namespace RPlus.SDK.Auth.Commands;

public record RequestOtpResponse(
    bool Success,
    int RetryAfterSeconds = 0,
    string? Code = null,
    string? ErrorCode = null,
    bool UserExists = false,
    string? SelectedChannel = null);
