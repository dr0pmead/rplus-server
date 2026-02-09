using MediatR;
using RPlus.SDK.Core.Abstractions;

#nullable enable
namespace RPlus.SDK.Auth.Commands;

public record VerifyOtpCommand(
    string Phone,
    string Code,
    string DeviceId,
    string? DpopPublicJwk,
    string? ClientIp,
    string? UserAgent) : IRequest<VerifyOtpResponse>, IBaseRequest;
