#nullable enable
namespace RPlus.SDK.Auth.Queries;

public record IdentifyUserResult(
    bool Exists,
    string? AuthMethod,
    bool IsBlocked);
