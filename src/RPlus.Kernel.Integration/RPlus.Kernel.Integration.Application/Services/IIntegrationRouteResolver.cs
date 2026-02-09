// Decompiled with JetBrains decompiler
// Type: RPlus.Kernel.Integration.Application.Services.IIntegrationRouteResolver
// Assembly: RPlus.Kernel.Integration.Application, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: C48B19BB-641F-4A32-A8FE-89CEE109A05C
// Assembly location: F:\RPlus Framework\Recovery\integration\app\RPlus.Kernel.Integration.Application.dll

using RPlus.Kernel.Integration.Domain.Entities;
using System;
using System.Threading;
using System.Threading.Tasks;

#nullable enable
namespace RPlus.Kernel.Integration.Application.Services;

public interface IIntegrationRouteResolver
{
  Task<IntegrationRoute?> ResolveAsync(
    string endpoint,
    Guid? partnerId,
    CancellationToken cancellationToken);
}
