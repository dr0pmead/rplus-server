using System;
using MediatR;
using RPlus.SDK.Core.Abstractions;

#nullable enable
namespace RPlus.SDK.Access.Queries;

public sealed record GetEffectiveRightsQuery(
    Guid UserId,
    Guid TenantId,
    string? Context = null) : IRequest<GetEffectiveRightsResponse>, IBaseRequest;
