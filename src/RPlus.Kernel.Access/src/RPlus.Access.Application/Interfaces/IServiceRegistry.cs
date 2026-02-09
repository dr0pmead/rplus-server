// Decompiled with JetBrains decompiler
// Type: RPlus.Access.Application.Interfaces.IServiceRegistry
// Assembly: RPlus.Access.Application, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 42B68179-0F94-443C-B8AC-3FE1745E13E8
// Assembly location: F:\RPlus Framework\Recovery\access\RPlus.Access.Application.dll

using RPlus.Access.Domain.Entities;
using System.Threading;
using System.Threading.Tasks;

#nullable enable
namespace RPlus.Access.Application.Interfaces;

public interface IServiceRegistry
{
  Task<ServiceRegistryEntry?> GetServiceAsync(
    string serviceName,
    CancellationToken cancellationToken = default (CancellationToken));

  Task RegisterOrUpdateAsync(
    string serviceName,
    string baseUrl,
    string publicKeys,
    ServiceCriticality criticality,
    CancellationToken cancellationToken = default (CancellationToken));

  Task RefreshCacheAsync(CancellationToken cancellationToken = default (CancellationToken));
}
