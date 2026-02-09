using RPlus.SDK.Contracts;

namespace RPlus.SDK.Contracts.Domain.Social;

public record SocialFeedPostRequested_v1(
    string UserId,
    string Channel,
    string Content,
    string OperationId,
    string RuleId,
    string NodeId
) : IntegrationEvent;

