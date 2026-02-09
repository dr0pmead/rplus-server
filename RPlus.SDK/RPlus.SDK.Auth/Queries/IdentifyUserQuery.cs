using MediatR;
using RPlus.SDK.Core.Abstractions;

#nullable enable
namespace RPlus.SDK.Auth.Queries;

public record IdentifyUserQuery(
    string Identifier,
    string? ClientIp,
    string? UserAgent) : IRequest<IdentifyUserResult>, IBaseRequest;
