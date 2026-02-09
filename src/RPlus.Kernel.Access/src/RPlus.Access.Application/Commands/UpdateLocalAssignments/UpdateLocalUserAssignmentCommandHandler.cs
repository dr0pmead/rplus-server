// Decompiled with JetBrains decompiler
// Type: RPlus.Access.Application.Commands.UpdateLocalAssignments.UpdateLocalUserAssignmentCommandHandler
// Assembly: RPlus.Access.Application, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 42B68179-0F94-443C-B8AC-3FE1745E13E8
// Assembly location: F:\RPlus Framework\Recovery\access\RPlus.Access.Application.dll

using MediatR;
using Microsoft.EntityFrameworkCore;
using RPlus.Access.Application.Interfaces;
using RPlus.Access.Domain.Entities;
using System;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

#nullable enable
namespace RPlus.Access.Application.Commands.UpdateLocalAssignments;

public class UpdateLocalUserAssignmentCommandHandler : 
  IRequestHandler<UpdateLocalUserAssignmentCommand>
{
  private readonly IAccessDbContext _context;

  public UpdateLocalUserAssignmentCommandHandler(IAccessDbContext context)
  {
    this._context = context;
  }

  public async Task Handle(
    UpdateLocalUserAssignmentCommand request,
    CancellationToken cancellationToken)
  {
    var normalizedPathSnapshot = string.IsNullOrWhiteSpace(request.PathSnapshot) ? "root" : request.PathSnapshot;

    LocalUserAssignment localUserAssignment = await this._context.UserAssignments.FirstOrDefaultAsync<LocalUserAssignment>((Expression<Func<LocalUserAssignment, bool>>) (x => x.TenantId == Guid.Empty && x.UserId == request.UserId && x.NodeId == request.NodeId && x.RoleCode == request.RoleCode), cancellationToken);
    if (localUserAssignment != null)
    {
      if (localUserAssignment.PathSnapshot != normalizedPathSnapshot)
        localUserAssignment.PathSnapshot = normalizedPathSnapshot;
    }
    else
      this._context.UserAssignments.Add(new LocalUserAssignment()
      {
        TenantId = Guid.Empty,
        UserId = request.UserId,
        NodeId = request.NodeId,
        RoleCode = request.RoleCode,
        PathSnapshot = normalizedPathSnapshot
      });

    // Make role changes effective immediately by dropping cached effective-rights snapshots.
    var staleSnapshots = await this._context.EffectiveSnapshots
      .Where(x => x.UserId == request.UserId)
      .ToListAsync(cancellationToken);

    if (staleSnapshots.Count > 0)
      this._context.EffectiveSnapshots.RemoveRange(staleSnapshots);

    int num = await this._context.SaveChangesAsync(cancellationToken);
  }
}
