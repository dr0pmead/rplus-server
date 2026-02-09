using System;

namespace RPlus.SDK.Contracts.Domain.Wallet;

public record WalletTransactionCreated_v1(
    string TransactionId,
    string UserId,
    decimal Amount,
    string Type,
    string ReferenceId
) : IntegrationEvent;

public record WalletTransactionCompleted_v1(
    string TransactionId,
    string UserId,
    decimal Amount,
    string ReferenceId
) : IntegrationEvent;

public record WalletTransactionFailed_v1(
    string TransactionId,
    string UserId,
    decimal Amount,
    string Reason,
    string ReferenceId
) : IntegrationEvent;

public record WalletBalanceUpdated_v1(
    string UserId,
    decimal NewBalance,
    string Currency
) : IntegrationEvent;
