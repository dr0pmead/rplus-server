// Decompiled with JetBrains decompiler
// Type: RPlus.Gateway.Application.Commands.PublishAppRelease.PublishAppReleaseCommandHandler
// Assembly: RPlus.Gateway.Application, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 67A55195-718A-4D21-B898-C0A623E6660E
// Assembly location: F:\RPlus Framework\Recovery\rplus-kernel-gateway\RPlus.Gateway.Application.dll

using MediatR;
using Microsoft.EntityFrameworkCore;
using RPlus.Gateway.Application.Contracts.Requests;
using RPlus.Gateway.Persistence;
using RPlus.Gateway.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

#nullable enable
namespace RPlus.Gateway.Application.Commands.PublishAppRelease;

public class PublishAppReleaseCommandHandler : IRequestHandler<PublishAppReleaseCommand, Guid>
{
  private readonly IGatewayDbContext _db;
  private readonly TimeProvider _timeProvider;

  public PublishAppReleaseCommandHandler(IGatewayDbContext db, TimeProvider timeProvider)
  {
    this._db = db;
    this._timeProvider = timeProvider;
  }

  public async Task<Guid> Handle(PublishAppReleaseCommand command, CancellationToken ct)
  {
    CreateAppReleaseRequest req = command.Request;
    string normalized = req.AppName.Trim().ToLowerInvariant();
    if (await this._db.AppReleases.AnyAsync<AppRelease>((Expression<Func<AppRelease, bool>>) (x => x.AppName == normalized), ct))
      throw new InvalidOperationException($"App '{normalized}' already exists.");
    DateTime utcDateTime = this._timeProvider.GetUtcNow().UtcDateTime;
    AppRelease release = new AppRelease()
    {
      Id = Guid.NewGuid(),
      AppName = normalized,
      DisplayName = req.DisplayName ?? req.AppName,
      MinVersionCode = req.MinVersionCode,
      LatestVersionCode = req.LatestVersionCode,
      StoreUrls = req.StoreUrls ?? new Dictionary<string, string>(),
      Message = req.Message,
      IsActive = req.IsActive,
      CreatedAt = utcDateTime,
      UpdatedAt = utcDateTime
    };
    this._db.AppReleases.Add(release);
    int num = await this._db.SaveChangesAsync(ct);
    Guid id = release.Id;
    req = (CreateAppReleaseRequest) null;
    release = (AppRelease) null;
    return id;
  }
}


