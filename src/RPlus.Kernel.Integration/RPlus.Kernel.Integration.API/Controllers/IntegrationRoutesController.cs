// Decompiled with JetBrains decompiler
// Type: RPlus.Kernel.Integration.Api.Controllers.IntegrationRoutesController
// Assembly: ExecuteService, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: C69F7836-BD02-4299-8BB3-623377DB3595
// Assembly location: F:\RPlus Framework\Recovery\integration\app\ExecuteService.dll

using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RPlus.Kernel.Integration.Application;
using RPlus.Kernel.Integration.Application.Features.Routes.Commands;
using RPlus.Kernel.Integration.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

#nullable enable
namespace RPlus.Kernel.Integration.Api.Controllers;

[ApiController]
[Route("api/integration/routes")]
public class IntegrationRoutesController : ControllerBase
{
  private readonly ISender _sender;
  private readonly IIntegrationDbContext _db;

  public IntegrationRoutesController(ISender sender, IIntegrationDbContext db)
  {
    this._sender = sender;
    this._db = db;
  }

  [HttpGet]
  public async Task<IActionResult> GetRoutes([FromQuery] Guid? partnerId)
  {
    IntegrationRoutesController routesController = this;
    IQueryable<IntegrationRoute> source = routesController._db.Routes.AsNoTracking<IntegrationRoute>();
    if (partnerId.HasValue)
      source = source.Where<IntegrationRoute>((Expression<Func<IntegrationRoute, bool>>) (r => r.PartnerId == partnerId));
    List<IntegrationRoute> listAsync = await source.ToListAsync<IntegrationRoute>();
    return (IActionResult) routesController.Ok((object) new
    {
      items = listAsync,
      totalCount = listAsync.Count
    });
  }

  [HttpGet("{id}")]
  public async Task<IActionResult> GetRoute(Guid id)
  {
    IntegrationRoutesController routesController = this;
    IntegrationRoute async = await routesController._db.Routes.FindAsync((object) id);
    return async != null ? (IActionResult) routesController.Ok((object) async) : (IActionResult) routesController.NotFound();
  }

  [HttpPost]
  public async Task<IActionResult> CreateRoute([FromBody] CreateRouteRequest request)
  {
    IntegrationRoutesController routesController = this;
    CreateRouteCommand createRouteCommand = new CreateRouteCommand(request.Name, request.RoutePattern, request.TargetHost, request.TargetService, request.TargetMethod, request.PartnerId, request.Priority);
    Guid guid = await routesController._sender.Send<Guid>((IRequest<Guid>) createRouteCommand);
    return (IActionResult) routesController.CreatedAtAction("GetRoute", (object) new
    {
      id = guid
    }, (object) null);
  }

  [HttpPatch("{id}")]
  public async Task<IActionResult> UpdateRoute(Guid id, [FromBody] UpdateRouteRequest request)
  {
    IntegrationRoutesController routesController = this;
    IntegrationRoute route = await routesController._db.Routes.FindAsync((object) id);
    if (route == null)
      return (IActionResult) routesController.NotFound();
    route.Update(request.Name ?? route.Name, request.RoutePattern ?? route.RoutePattern, request.TargetHost ?? route.TargetHost, request.TargetService ?? route.TargetService, request.TargetMethod ?? route.TargetMethod, request.Priority ?? route.Priority, request.PartnerId ?? route.PartnerId);
    bool? isActive = request.IsActive;
    if (isActive.HasValue)
    {
      isActive = request.IsActive;
      if (isActive.Value)
        route.Activate();
      else
        route.Deactivate();
    }
    int num = await routesController._db.SaveChangesAsync(CancellationToken.None);
    return (IActionResult) routesController.Ok((object) route);
  }

  [HttpDelete("{id}")]
  public async Task<IActionResult> DeleteRoute(Guid id)
  {
    IntegrationRoutesController routesController = this;
    IntegrationRoute async = await routesController._db.Routes.FindAsync((object) id);
    if (async == null)
      return (IActionResult) routesController.NotFound();
    routesController._db.Routes.Remove(async);
    int num = await routesController._db.SaveChangesAsync(CancellationToken.None);
    return (IActionResult) routesController.NoContent();
  }
}
