// Decompiled with JetBrains decompiler
// Type: RPlus.Access.Application.Services.PermissionRegistry
// Assembly: RPlus.Access.Application, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 42B68179-0F94-443C-B8AC-3FE1745E13E8
// Assembly location: F:\RPlus Framework\Recovery\access\RPlus.Access.Application.dll

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RPlus.Access.Application.Interfaces;
using RPlus.Access.Domain.Entities;
using System;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

#nullable enable
namespace RPlus.Access.Application.Services;

public class PermissionRegistry : IPermissionRegistry
{
  private readonly IAccessDbContext _dbContext;
  private readonly ILogger<PermissionRegistry> _logger;

  public PermissionRegistry(IAccessDbContext dbContext, ILogger<PermissionRegistry> logger)
  {
    this._dbContext = dbContext;
    this._logger = logger;
  }

  public async Task<bool> RegisterAsync(
    string permissionId,
    string? applicationId,
    string[] supportedContexts,
    CancellationToken ct)
  {
    Permission permission1 = await this._dbContext.Permissions.FirstOrDefaultAsync<Permission>((Expression<Func<Permission, bool>>) (x => x.Id == permissionId), ct);
    if (permission1 != null)
    {
      bool flag = false;
      string[] span = supportedContexts ?? Array.Empty<string>();
      string[] other = permission1.SupportedContexts ?? Array.Empty<string>();
      if (!((ReadOnlySpan<string>) span).SequenceEqual<string>((ReadOnlySpan<string>) other))
      {
        permission1.SupportedContexts = span;
        permission1.UpdatedAt = DateTime.UtcNow;
        flag = true;
      }
      if (!flag)
        return false;
      int num = await this._dbContext.SaveChangesAsync(ct);
      return true;
    }
    this._logger.LogInformation("Auto-registering discovered permission: {PermissionId}", (object) permissionId);
    string[] strArray = permissionId.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    string resource = strArray.Length <= 1 ? (strArray.Length == 1 ? strArray[0] : "unknown") : string.Join('.', strArray, 0, strArray.Length - 1);
    string action = strArray.Length > 1 ? strArray[strArray.Length - 1] : "execute";
    string appCode = applicationId ?? "system";
    App app = await this._dbContext.Apps.FirstOrDefaultAsync<App>((Expression<Func<App, bool>>) (x => x.Code == appCode), ct);
    if (app == null)
    {
      app = new App()
      {
        Id = Guid.NewGuid(),
        Code = appCode,
        Name = appCode
      };
      this._dbContext.Apps.Add(app);
      int num = await this._dbContext.SaveChangesAsync(ct);
    }
    Permission permission = new Permission()
    {
      Id = permissionId,
      AppId = app.Id,
      Resource = resource,
      Action = action,
      Title = "Auto-discovered " + permissionId,
      Status = "ACTIVE",
      CreatedAt = DateTime.UtcNow,
      UpdatedAt = DateTime.UtcNow,
      SupportedContexts = supportedContexts ?? Array.Empty<string>()
    };
    this._dbContext.Permissions.Add(permission);
    int num1 = await this._dbContext.SaveChangesAsync(ct);
    return true;
  }
}
