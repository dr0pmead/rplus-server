// Decompiled with JetBrains decompiler
// Type: RPlus.Kernel.Audit.Api.Controllers.AuditController
// Assembly: ExecuteService, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 1B6E86C6-C04D-4A27-9376-A218083AF5B8
// Assembly location: F:\RPlus Framework\Recovery\audit\ExecuteService.dll

using MediatR;
using Microsoft.AspNetCore.Mvc;
using RPlus.Kernel.Audit.Application.Commands.RecordAuditEvent;
using RPlus.Kernel.Audit.Application.Queries.GetAuditEvents;
using RPlus.Kernel.Audit.Domain.ValueObjects;
using System;
using System.Threading.Tasks;

#nullable enable
namespace RPlus.Kernel.Audit.Api.Controllers;

[ApiController]
[Route("api/audit")]
public class AuditController : ControllerBase
{
  private readonly IMediator _mediator;

  public AuditController(IMediator mediator) => this._mediator = mediator;

  [HttpPost("events")]
  public async Task<IActionResult> RecordEvent([FromBody] RecordEventRequest request)
  {
    AuditController auditController = this;
    RecordAuditEventCommand auditEventCommand = new RecordAuditEventCommand(request.Source, request.EventType, request.Severity, request.Actor, request.Action, request.Resource, request.Metadata);
    RecordAuditEventResponse auditEventResponse = await auditController._mediator.Send<RecordAuditEventResponse>((IRequest<RecordAuditEventResponse>) auditEventCommand);
    return (IActionResult) auditController.Ok((object) auditEventResponse);
  }

  [HttpGet("events")]
  public async Task<IActionResult> GetEvents(
    [FromQuery] EventSource? source,
    [FromQuery] DateTime? since,
    [FromQuery] DateTime? until,
    [FromQuery] int limit = 100)
  {
    AuditController auditController = this;
    GetAuditEventsQuery auditEventsQuery = new GetAuditEventsQuery(source, since, until, limit);
    AuditEventsResponse auditEventsResponse = await auditController._mediator.Send<AuditEventsResponse>((IRequest<AuditEventsResponse>) auditEventsQuery);
    return (IActionResult) auditController.Ok((object) auditEventsResponse);
  }

  [HttpGet("events/{id}")]
  public async Task<IActionResult> GetEvent(Guid id)
  {
    return (IActionResult) this.Ok((object) new{ id = id });
  }
}
