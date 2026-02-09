// Decompiled with JetBrains decompiler
// Type: RPlus.Access.Infrastructure.Services.RootAccessService
// Assembly: RPlus.Access.Infrastructure, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: EAF2AFCF-7B6C-4BF5-806A-4B3105E7710C
// Assembly location: F:\RPlus Framework\Recovery\access\RPlus.Access.Infrastructure.dll

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RPlus.Access.Application.Interfaces;
using RPlus.Access.Domain.Entities;
using System;
using System.Linq.Expressions;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

#nullable enable
namespace RPlus.Access.Infrastructure.Services;

public class RootAccessService : IRootAccessService
{
  private readonly IAccessDbContext _dbContext;
  private readonly IConfiguration _configuration;
  private readonly ILogger<RootAccessService> _logger;

  public RootAccessService(
    IAccessDbContext dbContext,
    IConfiguration configuration,
    ILogger<RootAccessService> logger)
  {
    this._dbContext = dbContext;
    this._configuration = configuration;
    this._logger = logger;
  }

  public async Task<bool> IsRootAsync(string userId, CancellationToken ct = default (CancellationToken))
  {
    try
    {
      string key = this._configuration["RootAccess:Secret"] ?? this._configuration["RootAccess__Secret"] ?? this._configuration["RPLUS_INTERNAL_SERVICE_SECRET"];
      if (string.IsNullOrWhiteSpace(key))
        return false;
      string input = (userId ?? string.Empty).Trim();
      if (Guid.TryParse(input, out var guid))
        input = guid.ToString("D");
      string hash = RootAccessService.ComputeHash(input, key);
      bool flag = await this._dbContext.RootRegistry.AnyAsync<RootRegistryEntry>((Expression<Func<RootRegistryEntry, bool>>) (r => r.HashedUserId == hash && r.Status == "ACTIVE"), ct);
      if (flag)
        this._logger.LogWarning("ROOT ACCESS DETECTED for user {UserIdHash} (Masked). Bypass granted.", (object) hash.Substring(0, 8));
      return flag;
    }
    catch (Exception ex)
    {
      this._logger.LogError(ex, "Error checking root status. Defaulting to FALSE.");
      return false;
    }
  }

  private static string ComputeHash(string input, string key)
  {
    using (HMACSHA256 hmacshA256 = new HMACSHA256(Encoding.UTF8.GetBytes(key)))
      return Convert.ToHexString(hmacshA256.ComputeHash(Encoding.UTF8.GetBytes(input))).ToLowerInvariant();
  }
}
