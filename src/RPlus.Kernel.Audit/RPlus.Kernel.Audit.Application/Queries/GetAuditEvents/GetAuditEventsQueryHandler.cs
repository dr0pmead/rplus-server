// Decompiled with JetBrains decompiler
// Type: RPlus.Kernel.Audit.Application.Queries.GetAuditEvents.GetAuditEventsQueryHandler
// Assembly: RPlus.Kernel.Audit.Application, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 47CD16EE-F06C-4FE6-B257-E7E3B39F4C9C
// Assembly location: F:\RPlus Framework\Recovery\audit\RPlus.Kernel.Audit.Application.dll

using MediatR;
using RPlus.Kernel.Audit.Application.Commands.RecordAuditEvent;
using RPlus.Kernel.Audit.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

#nullable enable
namespace RPlus.Kernel.Audit.Application.Queries.GetAuditEvents;

public class GetAuditEventsQueryHandler : IRequestHandler<GetAuditEventsQuery, AuditEventsResponse>
{
  private readonly IAuditRepository _repository;

  public GetAuditEventsQueryHandler(IAuditRepository repository) => this._repository = repository;

  public async Task<AuditEventsResponse> Handle(
    GetAuditEventsQuery request,
    CancellationToken cancellationToken)
  {
    List<AuditEvent> eventsAsync = await this._repository.GetEventsAsync(request.Source, request.Since, request.Until, request.Limit);
    return new AuditEventsResponse()
    {
      Events = eventsAsync.Select<AuditEvent, AuditEventDto>((Func<AuditEvent, AuditEventDto>) (e => new AuditEventDto()
      {
        Id = e.Id,
        Source = e.Source.ToString(),
        EventType = e.EventType.ToString(),
        Severity = e.Severity.ToString(),
        Actor = e.Actor,
        Action = e.Action,
        Resource = e.Resource,
        Metadata = e.Metadata,
        Timestamp = e.Timestamp
      })).ToList<AuditEventDto>(),
      TotalCount = eventsAsync.Count
    };
  }
}
