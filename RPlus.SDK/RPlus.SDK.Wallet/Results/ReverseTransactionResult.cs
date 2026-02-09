#nullable enable
namespace RPlus.SDK.Wallet.Results;

public sealed record ReverseTransactionResult(
    bool Success,
    long BalanceAfter,
    string? ErrorCode = null);
