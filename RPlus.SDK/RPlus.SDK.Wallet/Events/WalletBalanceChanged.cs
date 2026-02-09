#nullable enable
namespace RPlus.SDK.Wallet.Events;

public sealed record WalletBalanceChanged(
    string UserId,
    long PreviousBalance,
    long NewBalance,
    long ChangeAmount,
    string Reason);
