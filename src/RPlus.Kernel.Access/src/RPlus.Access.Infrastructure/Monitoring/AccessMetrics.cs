// Decompiled with JetBrains decompiler
// Type: RPlus.Access.Infrastructure.Monitoring.AccessMetrics
// Assembly: RPlus.Access.Infrastructure, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: EAF2AFCF-7B6C-4BF5-806A-4B3105E7710C
// Assembly location: F:\RPlus Framework\Recovery\access\RPlus.Access.Infrastructure.dll

using Prometheus;
using RPlus.Access.Application.Interfaces.Monitoring;

#nullable enable
namespace RPlus.Access.Infrastructure.Monitoring;

public sealed class AccessMetrics : IAccessMetrics
{
  private static readonly Counter AccessRequestsTotal = Metrics.CreateCounter("access_requests_total", "Total number of access check requests processed.", (CounterConfiguration) null);
  private static readonly Counter AccessDecisionsTotal;
  private static readonly Counter StepUpChallengesTotal;
  private static readonly Counter EventsConsumedTotal;
  private static readonly Histogram RiskScoreDistribution;
  private static readonly Counter ApiKeyCacheHitsTotal;
  private static readonly Counter ApiKeyCacheMissesTotal;
  private static readonly Counter QuotaExceededTotal;

  public void IncAccessRequest() => AccessMetrics.AccessRequestsTotal.Inc(1.0);

  public void IncAccessDecision(bool allowed, string reason, string tenantId)
  {
    string str = allowed ? nameof (allowed) : "denied";
    AccessMetrics.AccessDecisionsTotal.WithLabels(new string[3]
    {
      str,
      reason,
      tenantId ?? "global"
    }).Inc(1.0);
  }

  public void IncStepUpChallenge(string reason)
  {
    AccessMetrics.StepUpChallengesTotal.WithLabels(new string[1]
    {
      reason
    }).Inc(1.0);
  }

  public void ObserveRiskScore(double score, string level)
  {
    AccessMetrics.RiskScoreDistribution.WithLabels(new string[1]
    {
      level
    }).Observe(score);
  }

  public void IncEventConsumed(string topic, string status)
  {
    AccessMetrics.EventsConsumedTotal.WithLabels(new string[2]
    {
      topic,
      status
    }).Inc(1.0);
  }

  public void IncApiKeyCacheHit() => AccessMetrics.ApiKeyCacheHitsTotal.Inc(1.0);

  public void IncApiKeyCacheMiss() => AccessMetrics.ApiKeyCacheMissesTotal.Inc(1.0);

  public void IncQuotaExceeded(string keyId)
  {
    AccessMetrics.QuotaExceededTotal.WithLabels(new string[1]
    {
      keyId
    }).Inc(1.0);
  }

  static AccessMetrics()
  {
    CounterConfiguration configuration1 = new CounterConfiguration();
    configuration1.LabelNames = new string[3]
    {
      "result",
      "reason",
      "tenant_id"
    };
    AccessMetrics.AccessDecisionsTotal = Metrics.CreateCounter("access_decisions_total", "Total access decisions made", configuration1);
    CounterConfiguration configuration2 = new CounterConfiguration();
    configuration2.LabelNames = new string[1]{ "reason" };
    AccessMetrics.StepUpChallengesTotal = Metrics.CreateCounter("access_stepup_challenges_total", "Total Step-Up challenges issued", configuration2);
    CounterConfiguration configuration3 = new CounterConfiguration();
    configuration3.LabelNames = new string[2]
    {
      "topic",
      "status"
    };
    AccessMetrics.EventsConsumedTotal = Metrics.CreateCounter("access_events_consumed_total", "Total domain events consumed", configuration3);
    HistogramConfiguration configuration4 = new HistogramConfiguration();
    configuration4.LabelNames = new string[1]{ "level" };
    configuration4.Buckets = new double[6]
    {
      0.0,
      0.2,
      0.4,
      0.6,
      0.8,
      1.0
    };
    AccessMetrics.RiskScoreDistribution = Metrics.CreateHistogram("access_risk_score_distribution", "Distribution of calculated risk scores", configuration4);
    AccessMetrics.ApiKeyCacheHitsTotal = Metrics.CreateCounter("access_api_key_cache_hits_total", "Total API key validation cache hits", (CounterConfiguration) null);
    AccessMetrics.ApiKeyCacheMissesTotal = Metrics.CreateCounter("access_api_key_cache_misses_total", "Total API key validation cache misses", (CounterConfiguration) null);
    CounterConfiguration configuration5 = new CounterConfiguration();
    configuration5.LabelNames = new string[1]{ "key_id" };
    AccessMetrics.QuotaExceededTotal = Metrics.CreateCounter("access_quota_exceeded_total", "Total requests rejected due to quota exceeded", configuration5);
  }
}
