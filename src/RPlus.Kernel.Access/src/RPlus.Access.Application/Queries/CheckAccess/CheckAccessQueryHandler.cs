// Decompiled with JetBrains decompiler
// Type: RPlus.Access.Application.Queries.CheckAccess.CheckAccessQueryHandler
// Assembly: RPlus.Access.Application, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 42B68179-0F94-443C-B8AC-3FE1745E13E8
// Assembly location: F:\RPlus Framework\Recovery\access\RPlus.Access.Application.dll

using MediatR;
using RPlus.Access.Application.DTOs;
using RPlus.Access.Application.Interfaces;
using RPlus.Access.Application.Services;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

#nullable enable
namespace RPlus.Access.Application.Queries.CheckAccess;

public class CheckAccessQueryHandler : IRequestHandler<CheckAccessQuery, PolicyDecision>
{
  private readonly AccessCheckService _accessService;

  public CheckAccessQueryHandler(AccessCheckService accessService)
  {
    this._accessService = accessService;
  }

  public async Task<PolicyDecision> Handle(
    CheckAccessQuery request,
    CancellationToken cancellationToken)
  {
    return await this._accessService.CheckAccessAsync(request.UserId, request.PermissionId, request.NodeId, request.Context ?? new Dictionary<string, object>(), request.UseStrongConsistency ? ConsistencyLevel.Strong : ConsistencyLevel.Eventual);
  }
}
