// Decompiled with JetBrains decompiler
// Type: RPlus.Access.Application.Services.PolicyEvaluator
// Assembly: RPlus.Access.Application, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 42B68179-0F94-443C-B8AC-3FE1745E13E8
// Assembly location: F:\RPlus Framework\Recovery\access\RPlus.Access.Application.dll

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RPlus.Access.Application.Interfaces;
using RPlus.SDK.Access.Models;
using RPlus.Access.Domain.Entities;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

#nullable enable
namespace RPlus.Access.Application.Services;

public class PolicyEvaluator : IPolicyEvaluator
{
  private readonly IAccessDbContext _db;
  private readonly IConditionEvaluator _conditionEvaluator;
  private readonly IRiskEvaluator _riskEvaluator;
  private readonly ILogger<PolicyEvaluator> _logger;

  public PolicyEvaluator(
    IAccessDbContext db,
    IConditionEvaluator conditionEvaluator,
    IRiskEvaluator riskEvaluator,
    ILogger<PolicyEvaluator> logger)
  {
    this._db = db;
    this._conditionEvaluator = conditionEvaluator;
    this._riskEvaluator = riskEvaluator;
    this._logger = logger;
  }

  public async Task<PolicyDecision> EvaluateAsync(
    string permissionId,
    RPlus.SDK.Access.Models.AccessContext context,
    CancellationToken ct = default (CancellationToken))
  {
    Guid userId = context.Identity.UserId;
    List<string> trace = new List<string>();
    trace.Add($"Evaluating Access: User={userId}, Permission={permissionId}, Node={context.Resource.NodeId}");
    if (!context.Identity.EffectiveRoles.Any<string>())
    {
      trace.Add("No assignments (effective roles) found for user.");
      return new PolicyDecision(false, "No roles assigned", trace);
    }
    PolicyDecision sodAsync = await this.EvaluateSodAsync(context.Identity.EffectiveRoles, context.Identity.TenantId, trace, ct);
    if (sodAsync != (PolicyDecision) null && !sodAsync.Allowed)
      return sodAsync;
    List<string> userRoleCodes = context.Identity.EffectiveRoles.ToList<string>();
    List<AccessPolicy> listAsync = await this._db.AccessPolicies.Include<AccessPolicy, Domain.Entities.Role>((Expression<Func<AccessPolicy, Domain.Entities.Role>>) (p => p.Role)).Where<AccessPolicy>((Expression<Func<AccessPolicy, bool>>) (p => p.PermissionId == permissionId && userRoleCodes.Contains(p.Role.Code))).OrderByDescending<AccessPolicy, int>((Expression<Func<AccessPolicy, int>>) (p => p.Priority)).ToListAsync<AccessPolicy>(ct);
    if (!listAsync.Any<AccessPolicy>())
    {
      trace.Add("No policies found for this feature and user roles.");
      return new PolicyDecision(false, "No matching policies", trace);
    }
    bool flag = false;
    foreach (AccessPolicy accessPolicy in listAsync)
    {
      AccessPolicy policy = accessPolicy;
      if (!string.IsNullOrEmpty(policy.ConditionExpression) && !this._conditionEvaluator.Evaluate(policy.ConditionExpression, context.Attributes))
      {
        trace.Add($"Policy {policy.Id} SKIPPED: Condition false ({policy.ConditionExpression})");
      }
      else
      {
        trace.Add($"Policy {policy.Id} MATCHED: Effect={policy.Effect}");
        if (policy.Effect == "DENY")
          return new PolicyDecision(false, "Explicit Deny", trace);
        if (policy.Effect == "ALLOW")
        {
          RiskAssessment Risk = await this._riskEvaluator.AssessAsync(context, ct);
          int? nullable = policy.RequiredAuthLevel;
          int val1 = nullable ?? 1;
          nullable = Risk.RecommendedAal;
          int val2 = nullable ?? 1;
          StepUpChallenge challenge;
          if (this.CheckStepUpRequirement(Math.Max(val1, val2), policy.MaxAuthAgeSeconds, context.Authentication, trace, out challenge))
          {
            trace.Add($"Risk Assessment: Level={Risk.Level}, RecommendedAal={Risk.RecommendedAal}");
            return new PolicyDecision(false, "Step-Up Required (Policy/Risk)", trace, challenge, Risk);
          }
          flag = true;
        }
        policy = (AccessPolicy) null;
      }
    }
    return !flag ? new PolicyDecision(false, "Implicit Deny (No Allow policy matched)", trace) : new PolicyDecision(true, "Allowed by policy", trace);
  }

  private async Task<PolicyDecision?> EvaluateSodAsync(
    HashSet<string> userRoles,
    Guid tenantId,
    List<string> trace,
    CancellationToken ct)
  {
    SodPolicySet sodPolicySet = (await this._db.SodPolicySets.Include<SodPolicySet, List<SodPolicy>>((Expression<Func<SodPolicySet, List<SodPolicy>>>) (s => s.Policies)).Where<SodPolicySet>((Expression<Func<SodPolicySet, bool>>) (s => (int) s.Status == 1)).Where<SodPolicySet>((Expression<Func<SodPolicySet, bool>>) (s => s.TenantId == (Guid?) tenantId || s.TenantId == new Guid?())).OrderByDescending<SodPolicySet, int>((Expression<Func<SodPolicySet, int>>) (s => s.Version)).ToListAsync<SodPolicySet>(ct)).OrderByDescending<SodPolicySet, bool>((Func<SodPolicySet, bool>) (s => s.TenantId.HasValue)).FirstOrDefault<SodPolicySet>();
    if (sodPolicySet == null)
      return (PolicyDecision) null;
    foreach (SodPolicy policy in sodPolicySet.Policies)
    {
      int num = policy.ConflictRoles.Count<string>((Func<string, bool>) (r => userRoles.Contains(r)));
      if (num == policy.ConflictRoles.Count && num > 0)
      {
        trace.Add($"SoD VIOLATION: Policy {policy.Id} forbids combination: {string.Join(",", (IEnumerable<string>) policy.ConflictRoles)}");
        return new PolicyDecision(false, "SoD Conflict: " + policy.Description, trace);
      }
    }
    return (PolicyDecision) null;
  }

  private bool CheckStepUpRequirement(
    int requiredAal,
    int? maxAuthAgeSeconds,
    AuthenticationContext auth,
    List<string> trace,
    out StepUpChallenge? challenge)
  {
    challenge = (StepUpChallenge) null;
    if (auth.Aal < requiredAal)
    {
      trace.Add($"Step-Up: Current AAL ({auth.Aal}) < Required ({requiredAal})");
      challenge = new StepUpChallenge(requiredAal, "Insufficient Authentication Level", maxAuthAgeSeconds.HasValue ? new TimeSpan?(TimeSpan.FromSeconds((long) maxAuthAgeSeconds.Value)) : new TimeSpan?());
      return true;
    }
    if (maxAuthAgeSeconds.HasValue)
    {
      DateTime? authTime = auth.AuthTime;
      if (authTime.HasValue)
      {
        DateTime utcNow = DateTime.UtcNow;
        authTime = auth.AuthTime;
        DateTime dateTime = authTime.Value;
        TimeSpan timeSpan = utcNow - dateTime;
        if (timeSpan.TotalSeconds > (double) maxAuthAgeSeconds.Value)
        {
          trace.Add($"Step-Up: Auth Age ({timeSpan.TotalSeconds}s) > Max Allowed ({maxAuthAgeSeconds}s)");
          challenge = new StepUpChallenge(requiredAal, "Authentication Expired (Max Age)", new TimeSpan?(TimeSpan.FromSeconds((long) maxAuthAgeSeconds.Value)));
          return true;
        }
      }
    }
    return false;
  }
}
