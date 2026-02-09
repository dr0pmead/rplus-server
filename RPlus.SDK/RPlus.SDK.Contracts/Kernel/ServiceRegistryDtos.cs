// Decompiled with JetBrains decompiler
// Type: RPlus.Kernel.Core.Contracts.DTOs.AdminApiInfoDto
// Assembly: RPlus.SDK.Contracts, Version=1.0.53.0, Culture=neutral, PublicKeyToken=null
// MVID: A6C08EAE-EAE1-417A-A2D9-4C69FE3F3790
// Assembly location: F:\RPlus Framework\Recovery\loyalty\RPlus.SDK.Contracts.dll

#nullable enable
namespace RPlus.Kernel.Core.Contracts.DTOs;

public class AdminApiInfoDto
{
  public string Schema { get; set; } = "openapi";

  public string Endpoint { get; set; } = string.Empty;
}
