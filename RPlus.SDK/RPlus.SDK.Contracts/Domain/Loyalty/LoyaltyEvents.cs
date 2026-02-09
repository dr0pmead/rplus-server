using System;

namespace RPlus.SDK.Contracts.Domain.Loyalty;

public record LoyaltyPointsAccrualRequested_v1(
    string UserId,
    decimal Amount,
    string OperationId
) : IntegrationEvent;

public record LoyaltyPointsAccrued_v1(
    string UserId,
    decimal PointsDelta,
    decimal NewBalance,
    string OperationId
) : IntegrationEvent;

public record LoyaltyPointsRevoked_v1(
    string UserId,
    decimal Amount,
    string Reason
) : IntegrationEvent;

public record LoyaltyPointsAccrualFailed_v1(
    string UserId,
    string OperationId,
    string Reason
) : IntegrationEvent;
