#nullable enable
namespace RPlus.SDK.Wallet.Results;

public sealed record CommitReserveResult(
    bool Success,
    long BalanceAfter,
    string? ErrorCode = null);
