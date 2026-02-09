// Decompiled with JetBrains decompiler
// Type: RPlus.Kernel.Integration.Api.Controllers.ExternalPingController
// Assembly: ExecuteService, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: C69F7836-BD02-4299-8BB3-623377DB3595
// Assembly location: F:\RPlus Framework\Recovery\integration\app\ExecuteService.dll

using Microsoft.AspNetCore.Mvc;
using RPlus.SDK.Contracts.External;
using System;
using System.Linq;

#nullable enable
namespace RPlus.Kernel.Integration.Api.Controllers;

[ApiController]
[Route("external/v1/ping")]
public class ExternalPingController : ControllerBase
{
  [HttpGet]
  [HttpPost]
  [RPlus.SDK.Core.Abstractions.External("external.system.ping", null)]
  public IActionResult Ping()
  {
    return (IActionResult) this.Ok((object) ExternalResult<string>.Ok("pong", this.HttpContext.Response.Headers["X-Correlation-Id"].FirstOrDefault<string>() ?? this.HttpContext.Request.Headers["X-Correlation-Id"].FirstOrDefault<string>() ?? Guid.NewGuid().ToString()));
  }
}
