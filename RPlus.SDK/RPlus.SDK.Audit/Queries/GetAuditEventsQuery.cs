using System;
using MediatR;
using RPlus.SDK.Core.Abstractions;
using RPlus.SDK.Audit.Enums;

#nullable enable
namespace RPlus.SDK.Audit.Queries;

public sealed record GetAuditEventsQuery(
    EventSource? Source = null,
    DateTime? Since = null,
    DateTime? Until = null,
    int Limit = 100) : IRequest<AuditEventsResponse>, IBaseRequest;
