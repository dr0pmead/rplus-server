using System.Collections.Generic;
using MediatR;
using RPlus.SDK.Core.Abstractions;
using RPlus.SDK.Core.Primitives;

#nullable enable
namespace RPlus.SDK.Access.Commands;

public sealed record RegisterPermissionCommand(
    string PermissionId,
    string ApplicationId,
    List<string>? SupportedContexts = null) : IRequest<Result>, IBaseRequest;
