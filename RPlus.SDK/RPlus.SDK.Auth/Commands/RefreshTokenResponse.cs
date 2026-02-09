using RPlus.SDK.Auth.Models;

#nullable enable
namespace RPlus.SDK.Auth.Commands;

public record RefreshTokenResponse(
    bool Success,
    AuthUser? User = null,
    Device? Device = null,
    AuthSession? Session = null,
    string? ErrorCode = null);
