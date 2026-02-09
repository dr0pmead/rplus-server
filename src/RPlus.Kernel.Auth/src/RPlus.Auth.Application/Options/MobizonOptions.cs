// Decompiled with JetBrains decompiler
// Type: RPlus.Auth.Options.MobizonOptions
// Assembly: RPlus.Auth.Application, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 94419EED-98B7-4B52-A7B5-E1ADD668651C
// Assembly location: F:\RPlus Framework\Recovery\rplus-kernel-auth\RPlus.Auth.Application.dll

#nullable enable
namespace RPlus.Auth.Options;

public sealed class MobizonOptions
{
  public const string SectionName = "Mobizon";

  public string? ApiKey { get; init; }

  public string? ApiUrl { get; init; }

  public string? SenderName { get; init; }

  public bool EnableMock { get; init; }

  public int TimeoutSeconds { get; init; } = 30;
}
