// Decompiled with JetBrains decompiler
// Type: RPlus.Kernel.Audit.Api.Services.AuditGrpcService
// Assembly: ExecuteService, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 1B6E86C6-C04D-4A27-9376-A218083AF5B8
// Assembly location: F:\RPlus Framework\Recovery\audit\ExecuteService.dll

using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using MediatR;
using Microsoft.Extensions.Logging;
using RPlus.Kernel.Audit.Application.Commands.RecordAuditEvent;
using RPlus.Kernel.Audit.Application.Queries.GetAuditEvents;
using RPlus.Kernel.Audit.Domain.ValueObjects;
using RPlusGrpc.Audit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

#nullable enable
namespace RPlus.Kernel.Audit.Api.Services;

public class AuditGrpcService : AuditService.AuditServiceBase
{
  private readonly ILogger<AuditGrpcService> _logger;
  private readonly IMediator _mediator;

  public AuditGrpcService(ILogger<AuditGrpcService> logger, IMediator mediator)
  {
    this._logger = logger;
    this._mediator = mediator;
  }

  public override async Task<RecordEventResponse> RecordEvent(
    RecordEventRequest request,
    ServerCallContext context)
  {
    try
    {
      RecordAuditEventResponse auditEventResponse = await this._mediator.Send<RecordAuditEventResponse>((IRequest<RecordAuditEventResponse>) new RecordAuditEventCommand(AuditGrpcService.ParseEnum<EventSource>(request.Source).GetValueOrDefault(), AuditGrpcService.ParseEnum<AuditEventType>(request.EventType).GetValueOrDefault(), AuditGrpcService.ParseEnum<EventSeverity>(request.Severity) ?? EventSeverity.Info, request.Actor, request.Action, request.Resource, request.Metadata.ToDictionary<KeyValuePair<string, string>, string, object>((Func<KeyValuePair<string, string>, string>) (k => k.Key), (Func<KeyValuePair<string, string>, object>) (v => (object) v.Value))), context.CancellationToken);
      return new RecordEventResponse() { Success = true };
    }
    catch (Exception ex)
    {
      this._logger.LogError(ex, "Error recording audit event via gRPC");
      return new RecordEventResponse() { Success = false };
    }
  }

  public override async Task<GetEventsResponse> GetEvents(
    GetEventsRequest request,
    ServerCallContext context)
  {
    AuditEventsResponse auditEventsResponse = await this._mediator.Send<AuditEventsResponse>((IRequest<AuditEventsResponse>) new GetAuditEventsQuery(AuditGrpcService.ParseEnum<EventSource>(request.Source), request.Since?.ToDateTime(), Limit: request.Limit == 0 ? 100 : request.Limit), context.CancellationToken);
    GetEventsResponse events = new GetEventsResponse();
    events.Events.AddRange(auditEventsResponse.Events.Select<AuditEventDto, AuditEvent>((Func<AuditEventDto, AuditEvent>) (e => new AuditEvent()
    {
      Id = e.Id.ToString(),
      Source = e.Source,
      EventType = e.EventType,
      Severity = e.Severity,
      Actor = e.Actor,
      Action = e.Action,
      Resource = e.Resource,
      OccurredAt = Timestamp.FromDateTime(e.Timestamp.ToUniversalTime()),
      TraceId = ""
    })));
    return events;
  }

  private static T? ParseEnum<T>(string value) where T : struct
  {
    T result;
    return System.Enum.TryParse<T>(value, true, out result) ? new T?(result) : new T?();
  }
}
