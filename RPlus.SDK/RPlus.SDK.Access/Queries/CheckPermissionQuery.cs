using System;
using MediatR;
using RPlus.SDK.Core.Abstractions;

#nullable enable
namespace RPlus.SDK.Access.Queries;

public sealed record CheckPermissionQuery(
    Guid UserId,
    Guid TenantId,
    string PermissionId,
    string ApplicationId,
    string? Context = null) : IRequest<CheckPermissionResponse>, IBaseRequest;
