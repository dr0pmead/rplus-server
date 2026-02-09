using MediatR;
using RPlus.SDK.Core.Abstractions;
using RPlus.SDK.Core.Primitives;

#nullable enable
namespace RPlus.SDK.Access.Commands;

public sealed record ActivatePermissionCommand(
    string PermissionId,
    string ApplicationId) : IRequest<Result>, IBaseRequest;
