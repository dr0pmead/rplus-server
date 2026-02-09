// Decompiled with JetBrains decompiler
// Type: RPlus.Access.Application.DTOs.AccessCheckResult
// Assembly: RPlus.Access.Application, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 42B68179-0F94-443C-B8AC-3FE1745E13E8
// Assembly location: F:\RPlus Framework\Recovery\access\RPlus.Access.Application.dll

#nullable enable
namespace RPlus.Access.Application.DTOs;

public class AccessCheckResult
{
  public bool Allowed { get; set; }

  public string? Reason { get; set; }

  public string? TraceId { get; set; }

  public AccessCheckResult()
  {
  }

  public AccessCheckResult(bool allowed, string? reason = null, string? traceId = null)
  {
    this.Allowed = allowed;
    this.Reason = reason;
    this.TraceId = traceId;
  }
}
