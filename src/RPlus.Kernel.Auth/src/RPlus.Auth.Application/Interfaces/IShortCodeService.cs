namespace RPlus.Auth.Application.Interfaces;

/// <summary>
/// Short Code service for partner scan fallback.
/// Generates 6-digit numeric codes that users can dictate to cashiers
/// when QR scanning is unavailable.
/// </summary>
public interface IShortCodeService
{
    /// <summary>
    /// Generates a 6-digit short code for the user.
    /// Code is stored in Redis with 120s TTL and can only be used once (GETDEL).
    /// </summary>
    /// <param name="userId">User requesting the code.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Short code result with code and expiration info.</returns>
    Task<ShortCodeGenerateResult> GenerateAsync(Guid userId, CancellationToken ct = default);
}

/// <summary>
/// Result of short code generation for partner scan.
/// </summary>
/// <param name="Success">Whether generation succeeded.</param>
/// <param name="Code">6-digit code formatted with space (e.g., "384 912"). Null if failed.</param>
/// <param name="RawCode">6-digit code without formatting (e.g., "384912"). Null if failed.</param>
/// <param name="ExpiresInSeconds">TTL in seconds (120).</param>
/// <param name="ValidUntil">Absolute expiration time.</param>
/// <param name="Error">Error code if failed.</param>
public record ShortCodeGenerateResult(
    bool Success,
    string? Code,
    string? RawCode,
    int ExpiresInSeconds,
    DateTime ValidUntil,
    string? Error)
{
    private const int DefaultTtl = 120;

    public static ShortCodeGenerateResult Ok(string rawCode) =>
        new(
            true,
            FormatCode(rawCode),
            rawCode,
            DefaultTtl,
            DateTime.UtcNow.AddSeconds(DefaultTtl),
            null);

    public static ShortCodeGenerateResult Fail(string error) =>
        new(false, null, null, 0, DateTime.MinValue, error);

    /// <summary>
    /// Formats code with space in the middle for readability: "384912" -> "384 912"
    /// </summary>
    private static string FormatCode(string code) =>
        code.Length == 6 ? $"{code[..3]} {code[3..]}" : code;
}
