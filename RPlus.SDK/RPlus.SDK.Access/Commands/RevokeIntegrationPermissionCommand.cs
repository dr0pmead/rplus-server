using System;
using MediatR;
using RPlus.SDK.Core.Abstractions;
using RPlus.SDK.Core.Primitives;

#nullable enable
namespace RPlus.SDK.Access.Commands;

public sealed record RevokeIntegrationPermissionCommand(
    Guid ApiKeyId,
    string PermissionId) : IRequest<Result>, IBaseRequest;
