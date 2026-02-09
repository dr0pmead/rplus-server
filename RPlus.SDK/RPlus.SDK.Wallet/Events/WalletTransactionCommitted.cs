#nullable enable
namespace RPlus.SDK.Wallet.Events;

public sealed record WalletTransactionCommitted(
    string TransactionId,
    string UserId,
    string OperationId);
