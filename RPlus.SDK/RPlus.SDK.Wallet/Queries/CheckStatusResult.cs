#nullable enable
namespace RPlus.SDK.Wallet.Queries;

public sealed record CheckStatusResult(
    string Status,
    long Balance = 0,
    long ReservedBalance = 0,
    string? LastError = null);
