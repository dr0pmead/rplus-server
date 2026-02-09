// Decompiled with JetBrains decompiler
// Type: RPlus.Kernel.Integration.Application.Queries.GetIntegrationStatus.GetIntegrationStatusQueryHandler
// Assembly: RPlus.Kernel.Integration.Application, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: C48B19BB-641F-4A32-A8FE-89CEE109A05C
// Assembly location: F:\RPlus Framework\Recovery\integration\app\RPlus.Kernel.Integration.Application.dll

using MediatR;
using System;
using System.Threading;
using System.Threading.Tasks;

#nullable enable
namespace RPlus.Kernel.Integration.Application.Queries.GetIntegrationStatus;

public class GetIntegrationStatusQueryHandler : 
  IRequestHandler<GetIntegrationStatusQuery, IntegrationStatusResponse>
{
  public Task<IntegrationStatusResponse> Handle(
    GetIntegrationStatusQuery request,
    CancellationToken cancellationToken)
  {
    return Task.FromResult<IntegrationStatusResponse>(new IntegrationStatusResponse()
    {
      Service = "rplus-kernel-integration",
      Version = typeof (GetIntegrationStatusQueryHandler).Assembly.GetName().Version?.ToString() ?? "unknown",
      UtcNow = DateTime.UtcNow
    });
  }
}
