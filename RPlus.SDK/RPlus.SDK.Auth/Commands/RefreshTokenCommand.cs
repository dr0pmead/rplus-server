using MediatR;
using RPlus.SDK.Core.Abstractions;

#nullable enable
namespace RPlus.SDK.Auth.Commands;

public record RefreshTokenCommand(
    string RefreshToken,
    string DeviceId,
    string? DpopPublicJwk,
    string? ClientIp,
    string? UserAgent) : IRequest<RefreshTokenResponse>, IBaseRequest;
