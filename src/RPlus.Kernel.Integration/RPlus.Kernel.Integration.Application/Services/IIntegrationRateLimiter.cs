// Decompiled with JetBrains decompiler
// Type: RPlus.Kernel.Integration.Application.Services.IIntegrationRateLimiter
// Assembly: RPlus.Kernel.Integration.Application, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: C48B19BB-641F-4A32-A8FE-89CEE109A05C
// Assembly location: F:\RPlus Framework\Recovery\integration\app\RPlus.Kernel.Integration.Application.dll

using RPlus.Kernel.Integration.Domain.Entities;
using System.Threading;
using System.Threading.Tasks;

#nullable enable
namespace RPlus.Kernel.Integration.Application.Services;

public interface IIntegrationRateLimiter
{
  Task<bool> IsAllowedAsync(
    IntegrationApiKey key,
    string? routePattern,
    CancellationToken cancellationToken);
}
