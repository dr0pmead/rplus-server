// Decompiled with JetBrains decompiler
// Type: RPlus.Access.Application.Services.IntegrationPermissionService
// Assembly: RPlus.Access.Application, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 42B68179-0F94-443C-B8AC-3FE1745E13E8
// Assembly location: F:\RPlus Framework\Recovery\access\RPlus.Access.Application.dll

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RPlus.Access.Application.Interfaces;
using RPlus.Access.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

#nullable enable
namespace RPlus.Access.Application.Services;

public class IntegrationPermissionService : IIntegrationPermissionService
{
  private readonly IAccessDbContext _dbContext;
  private readonly ILogger<IntegrationPermissionService> _logger;

  public IntegrationPermissionService(
    IAccessDbContext dbContext,
    ILogger<IntegrationPermissionService> logger)
  {
    this._dbContext = dbContext;
    this._logger = logger;
  }

  public async Task<List<string>> GetPermissionsAsync(
    Guid apiKeyId,
    IDictionary<string, string> contextSignals,
    CancellationToken ct)
  {
    // Access maps KeyId -> explicit permissions only.
    // Any contextual decisions (rate limits, partner type, etc.) belong to the Integration bounded context.
    return await this._dbContext.IntegrationApiKeyPermissions
      .AsNoTracking<IntegrationApiKeyPermission>()
      .Where<IntegrationApiKeyPermission>((Expression<Func<IntegrationApiKeyPermission, bool>>) (p => p.ApiKeyId == apiKeyId))
      .Select<IntegrationApiKeyPermission, string>((Expression<Func<IntegrationApiKeyPermission, string>>) (p => p.PermissionId))
      .ToListAsync<string>(ct);
  }
}
