#nullable enable
namespace RPlus.SDK.Wallet.Events;

public sealed record WalletTransactionCancelled(
    string TransactionId,
    string UserId,
    string OperationId);
