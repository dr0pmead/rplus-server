using System.Threading;
using System.Threading.Tasks;
using RPlus.SDK.Loyalty.Models;
using RPlus.SDK.Loyalty.Results;

namespace RPlus.SDK.Loyalty.Abstractions;

public interface ILoyaltyRuleEvaluator
{
    Task<LoyaltyDecision> EvaluateAsync<TCommand>(TCommand command, LoyaltyProfile profile, CancellationToken cancellationToken = default) where TCommand : notnull;
}
