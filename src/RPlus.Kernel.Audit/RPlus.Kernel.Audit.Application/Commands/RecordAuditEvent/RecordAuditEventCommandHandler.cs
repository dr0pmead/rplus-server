// Decompiled with JetBrains decompiler
// Type: RPlus.Kernel.Audit.Application.Commands.RecordAuditEvent.RecordAuditEventCommandHandler
// Assembly: RPlus.Kernel.Audit.Application, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 47CD16EE-F06C-4FE6-B257-E7E3B39F4C9C
// Assembly location: F:\RPlus Framework\Recovery\audit\RPlus.Kernel.Audit.Application.dll

using MediatR;
using RPlus.Kernel.Audit.Domain.Entities;
using System.Threading;
using System.Threading.Tasks;

#nullable enable
namespace RPlus.Kernel.Audit.Application.Commands.RecordAuditEvent;

public class RecordAuditEventCommandHandler : 
  IRequestHandler<RecordAuditEventCommand, RecordAuditEventResponse>
{
  private readonly IAuditRepository _repository;
  private readonly IAuditPublisher _publisher;

  public RecordAuditEventCommandHandler(IAuditRepository repository, IAuditPublisher publisher)
  {
    this._repository = repository;
    this._publisher = publisher;
  }

  public async Task<RecordAuditEventResponse> Handle(
    RecordAuditEventCommand request,
    CancellationToken cancellationToken)
  {
    AuditEvent auditEvent = new AuditEvent(request.Source, request.EventType, request.Severity, request.Actor, request.Action, request.Resource, request.Metadata);
    await this._repository.AddAsync(auditEvent);
    await this._publisher.PublishAsync(auditEvent);
    RecordAuditEventResponse auditEventResponse = new RecordAuditEventResponse()
    {
      EventId = auditEvent.Id,
      Success = true
    };
    return auditEventResponse;
  }
}
