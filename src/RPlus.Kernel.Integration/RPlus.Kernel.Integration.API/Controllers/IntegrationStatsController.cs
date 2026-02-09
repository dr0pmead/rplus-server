// Decompiled with JetBrains decompiler
// Type: RPlus.Kernel.Integration.Api.Controllers.IntegrationStatsController
// Assembly: ExecuteService, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: C69F7836-BD02-4299-8BB3-623377DB3595
// Assembly location: F:\RPlus Framework\Recovery\integration\app\ExecuteService.dll

using Microsoft.AspNetCore.Mvc;
using RPlus.Kernel.Integration.Infrastructure.Services;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

#nullable enable
namespace RPlus.Kernel.Integration.Api.Controllers;

[ApiController]
[Route("api/integration/admin/stats")]
public class IntegrationStatsController : ControllerBase
{
  private readonly IntegrationStatsQueryService _queryService;
  public IntegrationStatsController(
    IntegrationStatsQueryService queryService)
  {
    this._queryService = queryService;
  }

  [HttpGet("summary")]
  public async Task<IActionResult> GetSummary(
    [FromQuery] Guid? partnerId,
    [FromQuery] string? scope,
    [FromQuery] string? endpoint,
    [FromQuery] string? env,
    [FromQuery] DateTime? from,
    [FromQuery] DateTime? to,
    CancellationToken cancellationToken)
  {
    IntegrationStatsController integrationStatsController = this;
    ref DateTime? local1 = ref to;
    DateTime valueOrDefault;
    DateTime dateTime1;
    if (!local1.HasValue)
    {
      dateTime1 = DateTime.UtcNow;
    }
    else
    {
      valueOrDefault = local1.GetValueOrDefault();
      dateTime1 = valueOrDefault.ToUniversalTime();
    }
    DateTime toUtc = dateTime1;
    ref DateTime? local2 = ref from;
    DateTime dateTime2;
    if (!local2.HasValue)
    {
      dateTime2 = toUtc.AddHours(-24.0);
    }
    else
    {
      valueOrDefault = local2.GetValueOrDefault();
      dateTime2 = valueOrDefault.ToUniversalTime();
    }
    DateTime fromUtc = dateTime2;
    if (fromUtc > toUtc)
      return (IActionResult) integrationStatsController.BadRequest((object) new
      {
        error = "invalid_period"
      });
    IntegrationStatsQuery query = new IntegrationStatsQuery(partnerId, scope, endpoint, env, fromUtc, toUtc);
    IReadOnlyList<IntegrationStatsSummaryRow> summaryAsync = await integrationStatsController._queryService.GetSummaryAsync(query, cancellationToken);
    return (IActionResult) integrationStatsController.Ok((object) new IntegrationStatsSummaryResponse(fromUtc, toUtc, summaryAsync));
  }

}
