using System;
using System.Threading.Tasks;
using RPlus.Kernel.Guard.Domain.Services;
using RPlus.SDK.Security.Enums;
using RPlus.SDK.Security.Models;

namespace RPlus.Kernel.Guard.Application.Pipelines;

public interface ISecurityPipeline
{
    Task<SecurityDecision> EvaluateAsync(SecurityContext context);
}

public class SecurityPipeline : ISecurityPipeline
{
    private readonly IGuardStateStore _stateStore;

    public SecurityPipeline(IGuardStateStore stateStore)
    {
        _stateStore = stateStore;
    }

    public async Task<SecurityDecision> EvaluateAsync(SecurityContext context)
    {
        var ip = context.Subject.IpAddress;

        // 1. Block List Check (Terminal)
        if (await _stateStore.IsBlockedAsync(ip))
        {
             return new SecurityDecision(
                 DecisionType.Block, 
                 ThreatLevel.Critical, 
                 DecisionSource.Cache, 
                 "IP_BLOCKED", 
                 IsTerminal: true,
                 EffectiveThreatLevel: ThreatLevel.Critical,
                 PolicyId: "BLOCK_LIST",
                 TtlSeconds: 300);
        }
        
        // 2. Threat Level Check
        var threat = await _stateStore.GetThreatLevelAsync(ip);
        if (threat == ThreatLevel.Critical)
        {
             return new SecurityDecision(
                 DecisionType.Block, 
                 ThreatLevel.Critical, 
                 DecisionSource.BotDetection, 
                 "THREAT_CRITICAL",
                 IsTerminal: true,
                 EffectiveThreatLevel: ThreatLevel.Critical,
                 PolicyId: "THREAT_POLICY_CRITICAL",
                 TtlSeconds: 300);
        }

        // 3. Rate Limit (Simple Fixed Window for demo)
        var (count, ttl) = await _stateStore.IncrementRateLimitAsync(ip, 10);
        
        if (count > 50) // 50 req / 10s = 5 RPS
        {
             return new SecurityDecision(
                 DecisionType.Throttle, 
                 threat, 
                 DecisionSource.RateLimit, 
                 "RATE_LIMIT_EXCEEDED",
                 IsTerminal: false,
                 EffectiveThreatLevel: threat,
                 PolicyId: "GLOBAL_RATE_LIMIT",
                 TtlSeconds: 10 + (int)threat * 5);
        }

        return new SecurityDecision(
            DecisionType.Allow, 
            threat, 
            DecisionSource.Manual, 
            "OK",
            IsTerminal: false,
            EffectiveThreatLevel: threat,
            PolicyId: "DEFAULT_ALLOW");
    }
}
