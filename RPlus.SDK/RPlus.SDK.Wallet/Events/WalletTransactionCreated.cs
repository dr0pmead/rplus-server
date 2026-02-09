#nullable enable
namespace RPlus.SDK.Wallet.Events;

public sealed record WalletTransactionCreated(
    string TransactionId,
    string UserId,
    long Amount,
    string OperationId,
    string Source,
    long Timestamp);
