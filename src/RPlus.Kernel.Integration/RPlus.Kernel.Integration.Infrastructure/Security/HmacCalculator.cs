using System;
using System.Security.Cryptography;
using System.Text;

namespace RPlus.Kernel.Integration.Infrastructure.Security;

/// <summary>
/// HMAC-SHA256 calculator for partner API signature verification.
/// Provides timing-attack-safe comparison.
/// </summary>
public static class HmacCalculator
{
    /// <summary>
    /// Compute HMAC-SHA256 hash of a message using the given secret.
    /// </summary>
    public static string Compute(string secret, string message)
    {
        var keyBytes = Encoding.UTF8.GetBytes(secret);
        var messageBytes = Encoding.UTF8.GetBytes(message);

        using var hmac = new HMACSHA256(keyBytes);
        var hash = hmac.ComputeHash(messageBytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>
    /// Timing-attack-safe string comparison.
    /// Uses CryptographicOperations.FixedTimeEquals to prevent timing attacks.
    /// </summary>
    public static bool FixedTimeEquals(string left, string right)
    {
        if (string.IsNullOrEmpty(left) || string.IsNullOrEmpty(right))
            return false;

        if (left.Length != right.Length)
            return false;

        var leftBytes = Encoding.UTF8.GetBytes(left);
        var rightBytes = Encoding.UTF8.GetBytes(right);

        return CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
    }
}
