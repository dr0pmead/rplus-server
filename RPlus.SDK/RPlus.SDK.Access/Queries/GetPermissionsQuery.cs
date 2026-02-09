using MediatR;
using RPlus.SDK.Core.Abstractions;

#nullable enable
namespace RPlus.SDK.Access.Queries;

public sealed record GetPermissionsQuery() : IRequest<PermissionsList>, IBaseRequest;
