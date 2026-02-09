// Decompiled with JetBrains decompiler
// Type: RPlus.Access.Api.Authentication.AccessClaimsTransformation
// Assembly: ExecuteService, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 809913E0-E790-491D-8B90-21CE464D2E43
// Assembly location: F:\RPlus Framework\Recovery\access\ExecuteService.dll

using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RPlus.Access.Application.Interfaces;
using RPlus.Access.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Security.Claims;
using System.Threading.Tasks;

#nullable enable
namespace RPlus.Access.Api.Authentication;

public class AccessClaimsTransformation : IClaimsTransformation
{
  private readonly IServiceProvider _serviceProvider;
  private readonly ILogger<AccessClaimsTransformation> _logger;

  public AccessClaimsTransformation(IServiceProvider serviceProvider, ILogger<AccessClaimsTransformation> logger)
  {
    this._serviceProvider = serviceProvider;
    this._logger = logger;
  }

  public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
  {
    ClaimsPrincipal clone = principal.Clone();
    ClaimsIdentity newIdentity = (ClaimsIdentity) clone.Identity;
    if (!newIdentity.IsAuthenticated)
      return principal;
    Claim claim = newIdentity.FindFirst("sub") ?? newIdentity.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier");
    Guid userId;
    if (claim == null || !Guid.TryParse(claim.Value, out userId))
      return principal;
    using (IServiceScope scope = this._serviceProvider.CreateScope())
    {
      List<string> listAsync = await scope.ServiceProvider.GetRequiredService<IAccessDbContext>().UserAssignments.Where<LocalUserAssignment>((Expression<Func<LocalUserAssignment, bool>>) (x => x.UserId == userId)).Select<LocalUserAssignment, string>((Expression<Func<LocalUserAssignment, string>>) (x => x.RoleCode)).ToListAsync<string>();
      this._logger.LogDebug("Resolved roles for user {UserId}: {Count}", (object) userId, (object) listAsync.Count);
      foreach (string str in listAsync)
      {
        if (!newIdentity.HasClaim("http://schemas.microsoft.com/ws/2008/06/identity/claims/role", str))
          newIdentity.AddClaim(new Claim("http://schemas.microsoft.com/ws/2008/06/identity/claims/role", str));
      }
      return clone;
    }
  }
}
