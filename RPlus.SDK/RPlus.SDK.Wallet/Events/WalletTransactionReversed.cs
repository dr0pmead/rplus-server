#nullable enable
namespace RPlus.SDK.Wallet.Events;

public sealed record WalletTransactionReversed(
    string OriginalTransactionId,
    string ReversalTransactionId,
    string UserId,
    long AmountReversed,
    string Reason);
