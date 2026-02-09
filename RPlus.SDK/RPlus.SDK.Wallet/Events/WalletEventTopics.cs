#nullable enable
namespace RPlus.SDK.Wallet.Events;

public static class WalletEventTopics
{
    public const string TransactionCreated = "wallet.transaction.created.v1";
    public const string BalanceChanged = "wallet.balance.changed.v1";
    public const string PromoAwarded = "wallet.promo.awarded.v1";
    public const string TransactionReversed = "wallet.transaction.reversed.v1";
    public const string TransactionCommitted = "wallet.transaction.committed.v1";
    public const string TransactionCancelled = "wallet.transaction.cancelled.v1";
}
