// Decompiled with JetBrains decompiler
// Type: RPlus.Gateway.Api.Middleware.SystemModeMiddleware
// Assembly: ExecuteService, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 53C73046-40B0-469F-A259-3E029837F0C4
// Assembly location: F:\RPlus Framework\Recovery\rplus-kernel-gateway\ExecuteService.dll

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using RPlus.Gateway.Api.Services;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

#nullable enable
namespace RPlus.Gateway.Api.Middleware;

public class SystemModeMiddleware
{
  private readonly RequestDelegate _next;
  private readonly ILogger<SystemModeMiddleware> _logger;
  private static readonly HashSet<string> Whitelist = new HashSet<string>((IEqualityComparer<string>) StringComparer.OrdinalIgnoreCase)
  {
    "/auth/login",
    "/auth/refresh",
    "/system/license",
    "/system/export",
    "/health"
  };

  public SystemModeMiddleware(RequestDelegate next, ILogger<SystemModeMiddleware> logger)
  {
    this._next = next;
    this._logger = logger;
  }

  public async Task InvokeAsync(HttpContext context, ISystemModeProvider modeProvider)
  {
    SystemMode currentModeAsync = await modeProvider.GetCurrentModeAsync();
    string lower = context.Request.Path.ToString().ToLower();
    context.Response.Headers["X-System-Mode"] = (StringValues) currentModeAsync.ToString();
    if (SystemModeMiddleware.Whitelist.Contains(lower) || lower.StartsWith("/system/"))
    {
      await this._next(context);
    }
    else
    {
      switch (currentModeAsync)
      {
        case SystemMode.READ_ONLY:
          if (HttpMethods.IsPost(context.Request.Method) || HttpMethods.IsPut(context.Request.Method) || HttpMethods.IsPatch(context.Request.Method) || HttpMethods.IsDelete(context.Request.Method))
          {
            this._logger.LogWarning("[Gateway] Write operation blocked in READ_ONLY mode: {Method} {Path}", (object) context.Request.Method, (object) lower);
            context.Response.StatusCode = 403;
            await context.Response.WriteAsJsonAsync(new
            {
              error = "System is in READ_ONLY mode",
              message = "Write operations are temporarily disabled",
              systemMode = currentModeAsync.ToString()
            });
            return;
          }
          break;
        case SystemMode.EMERGENCY:
          this._logger.LogWarning("[Gateway] Request blocked in EMERGENCY mode: {Method} {Path}", (object) context.Request.Method, (object) lower);
          context.Response.StatusCode = 503;
          await context.Response.WriteAsJsonAsync(new
          {
            error = "System is in EMERGENCY mode",
            message = "Only system administration endpoints are available",
            systemMode = currentModeAsync.ToString()
          });
          return;
      }
      await this._next(context);
    }
  }
}


