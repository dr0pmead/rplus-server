#nullable enable
namespace RPlus.SDK.Wallet.Results;

public sealed record CancelReserveResult(
    bool Success,
    long BalanceAfter,
    string? ErrorCode = null);
