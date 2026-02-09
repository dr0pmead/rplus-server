#nullable enable
namespace RPlus.SDK.Wallet.Events;

public sealed record PromoAwarded(
    string UserId,
    long Amount,
    string PromoId,
    string OperationId);
