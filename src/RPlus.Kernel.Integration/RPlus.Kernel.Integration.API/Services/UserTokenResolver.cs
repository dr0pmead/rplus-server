using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace RPlus.Kernel.Integration.Api.Services;

/// <summary>
/// Type of token used for partner scan.
/// </summary>
public enum TokenType
{
    /// <summary>QR code token (UUID format).</summary>
    QrToken,
    
    /// <summary>Short numeric code (6 digits).</summary>
    ShortCode
}

/// <summary>
/// Result of token resolution.
/// </summary>
public sealed record TokenResolutionResult(
    bool Success,
    Guid UserId,
    TokenType Type,
    string? Error)
{
    public static TokenResolutionResult Ok(Guid userId, TokenType type) => 
        new(true, userId, type, null);
    
    public static TokenResolutionResult Fail(string error, TokenType type) => 
        new(false, Guid.Empty, type, error);
}

/// <summary>
/// Unified token resolver that detects token type and delegates to appropriate validator.
/// </summary>
public interface IUserTokenResolver
{
    /// <summary>
    /// Resolves user identity from any supported token format.
    /// Automatically detects QR token vs Short Code.
    /// </summary>
    Task<TokenResolutionResult> ResolveAsync(string input, CancellationToken ct = default);
}

/// <summary>
/// Unified token resolver implementation.
/// Detection algorithm:
/// - 6 digits only → Short Code → Redis auth:otp:{code}
/// - Otherwise → QR Token → existing IQrTokenValidator
/// </summary>
public sealed class UserTokenResolver : IUserTokenResolver
{
    private static readonly Regex ShortCodePattern = new(@"^\d{6}$", RegexOptions.Compiled);
    private static readonly Regex SanitizePattern = new(@"[\s\-]", RegexOptions.Compiled);

    private readonly IQrTokenValidator _qrValidator;
    private readonly IShortCodeValidator _shortCodeValidator;
    private readonly ILogger<UserTokenResolver> _logger;

    public UserTokenResolver(
        IQrTokenValidator qrValidator,
        IShortCodeValidator shortCodeValidator,
        ILogger<UserTokenResolver> logger)
    {
        _qrValidator = qrValidator;
        _shortCodeValidator = shortCodeValidator;
        _logger = logger;
    }

    public async Task<TokenResolutionResult> ResolveAsync(string input, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return TokenResolutionResult.Fail("missing_token", TokenType.QrToken);
        }

        // Sanitize: remove spaces and dashes
        var sanitized = SanitizePattern.Replace(input, "");

        // Detection: 6 digits = Short Code, otherwise = QR Token
        if (ShortCodePattern.IsMatch(sanitized))
        {
            _logger.LogDebug("Token detected as Short Code: {Code}", sanitized);
            
            var result = await _shortCodeValidator.ValidateAsync(sanitized, ct).ConfigureAwait(false);
            
            return result.Success
                ? TokenResolutionResult.Ok(result.UserId, TokenType.ShortCode)
                : TokenResolutionResult.Fail(result.Error ?? "short_code_validation_failed", TokenType.ShortCode);
        }
        else
        {
            _logger.LogDebug("Token detected as QR Token");
            
            var result = _qrValidator.Validate(sanitized);
            
            return result.Success
                ? TokenResolutionResult.Ok(result.UserId, TokenType.QrToken)
                : TokenResolutionResult.Fail(result.Error, TokenType.QrToken);
        }
    }
}
