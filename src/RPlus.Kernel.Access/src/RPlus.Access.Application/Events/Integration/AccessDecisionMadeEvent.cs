// Decompiled with JetBrains decompiler
// Type: RPlus.Access.Application.Events.Integration.AccessDecisionMadeEvent
// Assembly: RPlus.Access.Application, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 42B68179-0F94-443C-B8AC-3FE1745E13E8
// Assembly location: F:\RPlus Framework\Recovery\access\RPlus.Access.Application.dll

using System;
using System.Collections.Generic;

#nullable enable
namespace RPlus.Access.Application.Events.Integration;

public class AccessDecisionMadeEvent
{
  public Guid TraceId { get; set; }

  public Guid TenantId { get; set; }

  public Guid UserId { get; set; }

  public string Action { get; set; } = string.Empty;

  public string? Resource { get; set; }

  public bool Allowed { get; set; }

  public string Reason { get; set; } = string.Empty;

  public DateTime Timestamp { get; set; } = DateTime.UtcNow;

  public Dictionary<string, object> Context { get; set; } = new Dictionary<string, object>();

  public bool StepUpRequired { get; set; }

  public int? RequiredAuthLevel { get; set; }

  public TimeSpan? MaxAuthAge { get; set; }

  public int RiskLevel { get; set; }

  public List<string> RiskSignals { get; set; } = new List<string>();
}
