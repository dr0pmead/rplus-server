// Decompiled with JetBrains decompiler
// Type: RPlus.Access.Application.Services.RiskEvaluator
// Assembly: RPlus.Access.Application, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 42B68179-0F94-443C-B8AC-3FE1745E13E8
// Assembly location: F:\RPlus Framework\Recovery\access\RPlus.Access.Application.dll

using RPlus.Access.Application.Interfaces;
using RPlus.SDK.Access.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

#nullable enable
namespace RPlus.Access.Application.Services;

public class RiskEvaluator : IRiskEvaluator
{
  public async Task<RiskAssessment> AssessAsync(AccessContext context, CancellationToken ct = default (CancellationToken))
  {
    List<RiskSignal> source = new List<RiskSignal>();
    if (!context.Attributes.ContainsKey("act"))
    {
      int num = context.Identity.TenantId == Guid.Empty ? 1 : 0;
    }
    if (context.Request.IpAddress == "1.1.1.1")
      source.Add(new RiskSignal("WATCHLIST_IP", RiskLevel.High, context.Request.IpAddress));
    RiskLevel riskLevel = source.Any<RiskSignal>() ? source.Max<RiskSignal, RiskLevel>((Func<RiskSignal, RiskLevel>) (s => s.Severity)) : RiskLevel.Low;
    int? nullable = new int?();
    if (riskLevel >= RiskLevel.High)
      nullable = new int?(3);
    else if (riskLevel >= RiskLevel.Elevated)
      nullable = new int?(2);
    return new RiskAssessment()
    {
      Level = riskLevel,
      Signals = source,
      RecommendedAal = nullable,
      Explanation = source.Any<RiskSignal>() ? string.Join("; ", source.Select<RiskSignal, string>((Func<RiskSignal, string>) (s => s.Code))) : "No Risk Signals"
    };
  }
}
