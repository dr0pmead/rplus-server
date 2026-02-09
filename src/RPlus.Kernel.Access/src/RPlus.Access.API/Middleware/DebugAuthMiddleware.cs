// Decompiled with JetBrains decompiler
// Type: RPlus.Access.Api.Middleware.DebugAuthMiddleware
// Assembly: ExecuteService, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 809913E0-E790-491D-8B90-21CE464D2E43
// Assembly location: F:\RPlus Framework\Recovery\access\ExecuteService.dll

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Security.Claims;
using System.Threading.Tasks;

#nullable enable
namespace RPlus.Access.Api.Middleware;

public class DebugAuthMiddleware
{
  private readonly RequestDelegate _next;
  private readonly ILogger<DebugAuthMiddleware> _logger;

  public DebugAuthMiddleware(RequestDelegate next, ILogger<DebugAuthMiddleware> logger)
  {
    this._next = next;
    this._logger = logger;
  }

  public async Task InvokeAsync(HttpContext context)
  {
    ClaimsPrincipal user = context.User;
    this._logger.LogWarning("[DebugAuth] Request: {Path}, User Encoded: {Identity}, IsAuth: {IsAuth}", (object) context.Request.Path, (object) (user?.Identity?.Name ?? "null"), (object) user?.Identity?.IsAuthenticated);
    if (user != null)
    {
      foreach (Claim claim in user.Claims)
        this._logger.LogWarning("[DebugAuth] Claim: {Type} = {Value}", (object) claim.Type, (object) claim.Value);
    }
    else
      this._logger.LogWarning("[DebugAuth] User is NULL");
    await this._next(context);
  }
}
