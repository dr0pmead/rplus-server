using Microsoft.Extensions.Logging;

namespace RPlus.Kernel.Integration.Infrastructure.Services;

/// <summary>
/// Composite protector that tries the primary protector first, then falls back 
/// to the legacy protector for decryption of old keys.
/// New encryptions always use the primary protector (AES-GCM).
/// </summary>
public sealed class FallbackSecretProtector : ISecretProtector
{
    private readonly ISecretProtector _primary;
    private readonly ISecretProtector _fallback;
    private readonly ILogger<FallbackSecretProtector> _logger;

    public FallbackSecretProtector(
        ISecretProtector primary,
        ISecretProtector fallback,
        ILogger<FallbackSecretProtector> logger)
    {
        _primary = primary;
        _fallback = fallback;
        _logger = logger;
    }

    /// <summary>
    /// Always encrypts with the primary (AES-GCM) protector.
    /// </summary>
    public string Protect(string plaintext) => _primary.Protect(plaintext);

    /// <summary>
    /// Tries the primary protector first. If it fails (e.g. legacy DataProtection-encrypted value),
    /// falls back to the legacy protector. This allows seamless migration.
    /// </summary>
    public string Unprotect(string protectedValue)
    {
        try
        {
            return _primary.Unprotect(protectedValue);
        }
        catch
        {
            _logger.LogDebug("Primary protector failed, trying fallback for legacy secret");
            return _fallback.Unprotect(protectedValue);
        }
    }
}
