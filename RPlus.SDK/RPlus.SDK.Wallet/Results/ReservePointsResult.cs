#nullable enable
namespace RPlus.SDK.Wallet.Results;

public sealed record ReservePointsResult(
    bool Success,
    string Status,
    long AvailableBalance,
    string? ErrorCode = null);
