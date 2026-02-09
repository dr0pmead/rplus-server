using RPlus.SDK.Contracts;

namespace RPlus.SDK.Contracts.Domain.Notifications;

public record NotificationDispatchRequested_v1(
    string UserId,
    string Channel,
    string Title,
    string Body,
    string OperationId,
    string RuleId,
    string NodeId
) : IntegrationEvent;

