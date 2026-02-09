// Decompiled with JetBrains decompiler
// Type: RPlus.Kernel.Integration.Api.Controllers.IntegrationStatusController
// Assembly: ExecuteService, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: C69F7836-BD02-4299-8BB3-623377DB3595
// Assembly location: F:\RPlus Framework\Recovery\integration\app\ExecuteService.dll

using MediatR;
using Microsoft.AspNetCore.Mvc;
using RPlus.Kernel.Integration.Application.Queries.GetIntegrationStatus;
using System.Threading.Tasks;

#nullable enable
namespace RPlus.Kernel.Integration.Api.Controllers;

[ApiController]
[Route("api/integration")]
public class IntegrationStatusController : ControllerBase
{
  private readonly IMediator _mediator;

  public IntegrationStatusController(IMediator mediator) => this._mediator = mediator;

  [HttpGet("status")]
  public async Task<IActionResult> GetStatus()
  {
    IntegrationStatusController statusController = this;
    IntegrationStatusResponse integrationStatusResponse = await statusController._mediator.Send<IntegrationStatusResponse>((IRequest<IntegrationStatusResponse>) new GetIntegrationStatusQuery());
    return (IActionResult) statusController.Ok((object) integrationStatusResponse);
  }
}
