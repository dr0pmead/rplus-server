// Decompiled with JetBrains decompiler
// Type: RPlus.Auth.Options.RiskManagementOptions
// Assembly: RPlus.Auth.Application, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 94419EED-98B7-4B52-A7B5-E1ADD668651C
// Assembly location: F:\RPlus Framework\Recovery\rplus-kernel-auth\RPlus.Auth.Application.dll

#nullable enable
namespace RPlus.Auth.Options;

public sealed class RiskManagementOptions
{
  public const string SectionName = "RiskManagement";

  public int IpChangeScore { get; set; } = 25;

  public int UserAgentChangeScore { get; set; } = 35;

  public int SuspiciousThreshold { get; set; } = 50;

  public int CriticalThreshold { get; set; } = 80 /*0x50*/;
}
