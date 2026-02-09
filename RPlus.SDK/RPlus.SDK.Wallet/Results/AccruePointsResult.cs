#nullable enable
namespace RPlus.SDK.Wallet.Results;

/// <summary>
/// Outcome returned to callers and consumers after processing <see cref="Commands.AccruePointsCommand"/>.
/// </summary>
public sealed record AccruePointsResult(
    bool Success,
    string Status,
    long NewBalance,
    string? ErrorCode = null);
