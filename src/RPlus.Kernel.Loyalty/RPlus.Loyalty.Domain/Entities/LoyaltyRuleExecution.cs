using System;

namespace RPlus.Loyalty.Domain.Entities;

public class LoyaltyRuleExecution
{
    public Guid Id { get; private set; }
    public Guid RuleId { get; private set; }
    public Guid ProfileId { get; private set; }
    public Guid UserId { get; private set; }
    public string EventType { get; private set; } = string.Empty;
    public string OperationId { get; private set; } = string.Empty;
    public decimal PointsApplied { get; private set; }
    public DateTime ExecutedAt { get; private set; }
    public string? MetadataHash { get; private set; }

    private LoyaltyRuleExecution()
    {
    }

    public static LoyaltyRuleExecution Create(
        Guid ruleId,
        Guid profileId,
        Guid userId,
        string eventType,
        string operationId,
        decimal pointsApplied,
        string? metadataHash)
    {
        return new LoyaltyRuleExecution
        {
            Id = Guid.NewGuid(),
            RuleId = ruleId,
            ProfileId = profileId,
            UserId = userId,
            EventType = eventType,
            OperationId = operationId,
            PointsApplied = pointsApplied,
            ExecutedAt = DateTime.UtcNow,
            MetadataHash = metadataHash
        };
    }
}
