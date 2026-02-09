using MediatR;
using RPlus.SDK.Core.Abstractions;

#nullable enable
namespace RPlus.SDK.Auth.Commands;

public record RevokeSessionCommand(
    string DeviceId,
    string? SessionId = null,
    string? ClientIp = null,
    string? UserAgent = null) : IRequest<bool>, IBaseRequest;
