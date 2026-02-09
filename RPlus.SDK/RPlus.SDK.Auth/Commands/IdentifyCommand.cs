using MediatR;
using RPlus.SDK.Core.Abstractions;

#nullable enable
namespace RPlus.SDK.Auth.Commands;

public record IdentifyCommand(
    string Login,
    string? ClientIp = null,
    string? UserAgent = null) : IRequest<IdentifyResponse>, IBaseRequest;
