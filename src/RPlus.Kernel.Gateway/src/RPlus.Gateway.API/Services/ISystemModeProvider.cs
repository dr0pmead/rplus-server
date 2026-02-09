// Decompiled with JetBrains decompiler
// Type: RPlus.Gateway.Api.Services.ISystemModeProvider
// Assembly: ExecuteService, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 53C73046-40B0-469F-A259-3E029837F0C4
// Assembly location: F:\RPlus Framework\Recovery\rplus-kernel-gateway\ExecuteService.dll

using System.Threading.Tasks;

#nullable enable
namespace RPlus.Gateway.Api.Services;

public interface ISystemModeProvider
{
  Task<SystemMode> GetCurrentModeAsync();
}


