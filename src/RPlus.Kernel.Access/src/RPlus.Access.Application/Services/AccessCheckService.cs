using Microsoft.EntityFrameworkCore;
using RPlus.Access.Application.DTOs;
using RPlus.Access.Application.Events.Integration;
using RPlus.Access.Application.Interfaces;
using RPlus.Access.Application.Interfaces.Monitoring;
using RPlus.SDK.Access.Models;
using RPlus.Access.Domain.Entities;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

#nullable enable
namespace RPlus.Access.Application.Services;

public class AccessCheckService
{
    private readonly IAccessDbContext _dbContext;
    private readonly IAuditProducer _auditProducer;
    private readonly IPolicyEvaluator _policyEvaluator;
    private readonly IAccessContextBuilder _contextBuilder;
    private readonly IRiskEvaluator _riskEvaluator;
    private readonly IAccessMetrics _metrics;
    private readonly IConnectionMultiplexer _redis;

    public AccessCheckService(
        IAccessDbContext dbContext,
        IAuditProducer auditProducer,
        IPolicyEvaluator policyEvaluator,
        IAccessContextBuilder contextBuilder,
        IRiskEvaluator riskEvaluator,
        IAccessMetrics metrics,
        IConnectionMultiplexer redis)
    {
        _dbContext = dbContext;
        _auditProducer = auditProducer;
        _policyEvaluator = policyEvaluator;
        _contextBuilder = contextBuilder;
        _riskEvaluator = riskEvaluator;
        _metrics = metrics;
        _redis = redis;
    }

    public async Task<PolicyDecision> CheckAccessAsync(
        Guid userId,
        string actionCode,
        Guid? nodeId,
        Dictionary<string, object> rawContext,
        ConsistencyLevel consistency = ConsistencyLevel.Eventual)
    {
        _metrics.IncAccessRequest();
        var traceId = Guid.NewGuid();
        var debugTrace = new List<string> { $"TraceId={traceId}" };

        try
        {
            var accessContext = await _contextBuilder.BuildContextAsync(userId, actionCode, nodeId, rawContext);

            var tenantId = accessContext.Identity.TenantId;
            bool allowed;
            string reason;
            StepUpChallenge? challenge = null;
            RiskAssessment? risk = null;

            // 2. Cache Check (if eventual consistency)
            if (consistency == ConsistencyLevel.Eventual)
            {
                var db = _redis.GetDatabase();
                var version = await db.StringGetAsync($"policy_version:{tenantId}");
                if (version.IsNull) version = "1";
                
                var hash = ComputeHash(accessContext);
                var cachedDecision = await db.StringGetAsync($"decision:{tenantId}:{version}:{hash}");
                
                if (!cachedDecision.IsNull)
                {
                    allowed = cachedDecision == "ALLOW";
                    reason = "Cached Decision";
                    var decision = new PolicyDecision(allowed, reason, debugTrace);
                    await ProduceAuditAsync(traceId, tenantId, userId, actionCode, nodeId, allowed, reason, rawContext, null, null);
                    return decision;
                }
            }

            // 3. Main Evaluation
            (allowed, reason, challenge, risk) = await EvaluateInternalAsync(actionCode, accessContext, consistency);
            
            _metrics.IncAccessDecision(allowed, reason, tenantId.ToString());
            
            if (challenge != null) _metrics.IncStepUpChallenge(challenge.Reason);
            if (risk != null)
            {
                double score = risk.Level == RiskLevel.Critical ? 1.0 : (double)risk.Level * 0.33;
                _metrics.ObserveRiskScore(score, risk.Level.ToString());
            }

            var finalDecision = new PolicyDecision(allowed, reason, debugTrace, challenge, risk);

            // 4. Audit & Return
            await ProduceAuditAsync(traceId, tenantId, userId, actionCode, nodeId, allowed, reason, rawContext, challenge, risk);
            
            return finalDecision;
        }
        catch (Exception ex)
        {
            _metrics.IncAccessDecision(false, "System Error", "00000000-0000-0000-0000-000000000000");
            return new PolicyDecision(false, $"System Error: {ex.Message}", debugTrace);
        }
    }

    private async Task ProduceAuditAsync(
        Guid traceId,
        Guid tenantId,
        Guid userId,
        string action,
        Guid? resource,
        bool allowed,
        string reason,
        Dictionary<string, object> ctx,
        StepUpChallenge? challenge,
        RiskAssessment? risk)
    {
        var @event = new AccessDecisionMadeEvent
        {
            TraceId = traceId,
            TenantId = tenantId,
            UserId = userId,
            Action = action,
            Resource = resource?.ToString(),
            Allowed = allowed,
            Reason = reason,
            Context = ctx,
            StepUpRequired = challenge != null,
            RequiredAuthLevel = challenge?.RequiredLevel,
            MaxAuthAge = challenge?.MaxAge,
            RiskLevel = (int)(risk?.Level ?? RiskLevel.Low),
            RiskSignals = risk?.Signals.Select(s => s.Code).ToList() ?? new List<string>()
        };

        await _auditProducer.ProduceAuditLogAsync(@event);
    }

    private async Task<(bool Allowed, string Reason, StepUpChallenge? Challenge, RiskAssessment? Risk)> EvaluateInternalAsync(
        string featureCode,
        AccessContext context,
        ConsistencyLevel consistency)
    {
        var permission = await _dbContext.Permissions.FirstOrDefaultAsync(p => p.Id == featureCode);
        if (permission == null)
        {
            return (false, "Permission not found", null, null);
        }

        var decision = await _policyEvaluator.EvaluateAsync(featureCode, context);
        return (decision.Allowed, decision.Reason, decision.Challenge, decision.Risk);
    }

    private string ComputeHash(AccessContext ctx)
    {
        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(ctx));
        return Convert.ToHexString(sha256.ComputeHash(bytes));
    }
}
