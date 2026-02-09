#nullable enable
namespace RPlus.SDK.Auth.Commands;

public record IdentifyResponse(
    bool Exists,
    string? AuthMethod = null,
    string? Error = null);
