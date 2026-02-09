// Decompiled with JetBrains decompiler
// Type: RPlus.Gateway.Application.Queries.GetAppVersion.GetAppVersionQueryHandler
// Assembly: RPlus.Gateway.Application, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 67A55195-718A-4D21-B898-C0A623E6660E
// Assembly location: F:\RPlus Framework\Recovery\rplus-kernel-gateway\RPlus.Gateway.Application.dll

using MediatR;
using Microsoft.EntityFrameworkCore;
using RPlus.Gateway.Application.Contracts.Responses;
using RPlus.Gateway.Persistence;
using RPlus.Gateway.Domain.Entities;
using System;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

#nullable enable
namespace RPlus.Gateway.Application.Queries.GetAppVersion;

public class GetAppVersionQueryHandler : IRequestHandler<GetAppVersionQuery, AppVersionResponse?>
{
  private readonly IGatewayDbContext _db;

  public GetAppVersionQueryHandler(IGatewayDbContext db) => this._db = db;

  public async Task<AppVersionResponse?> Handle(GetAppVersionQuery request, CancellationToken ct)
  {
    string normalized = request.AppName.Trim().ToLowerInvariant();
    AppRelease appRelease = await this._db.AppReleases.AsNoTracking<AppRelease>().Where<AppRelease>((Expression<Func<AppRelease, bool>>) (x => x.AppName == normalized && x.IsActive)).OrderByDescending<AppRelease, DateTime>((Expression<Func<AppRelease, DateTime>>) (x => x.UpdatedAt)).FirstOrDefaultAsync<AppRelease>(ct);
    return appRelease != null ? new AppVersionResponse(appRelease.MinVersionCode, appRelease.LatestVersionCode, appRelease.StoreUrls, appRelease.Message) : (AppVersionResponse) null;
  }
}


