// Decompiled with JetBrains decompiler
// Type: RPlus.Access.Application.Interfaces.Monitoring.IAccessMetrics
// Assembly: RPlus.Access.Application, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 42B68179-0F94-443C-B8AC-3FE1745E13E8
// Assembly location: F:\RPlus Framework\Recovery\access\RPlus.Access.Application.dll

#nullable enable
namespace RPlus.Access.Application.Interfaces.Monitoring;

public interface IAccessMetrics
{
  void IncAccessRequest();

  void IncAccessDecision(bool allowed, string reason, string tenantId);

  void IncStepUpChallenge(string reason);

  void ObserveRiskScore(double score, string level);

  void IncEventConsumed(string topic, string status);

  void IncApiKeyCacheHit();

  void IncApiKeyCacheMiss();

  void IncQuotaExceeded(string keyId);
}
