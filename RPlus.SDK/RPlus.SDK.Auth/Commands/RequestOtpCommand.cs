using MediatR;
using RPlus.SDK.Core.Abstractions;

#nullable enable
namespace RPlus.SDK.Auth.Commands;

public record RequestOtpCommand(
    string Phone,
    string DeviceId,
    string? ClientIp,
    string? UserAgent,
    string? Channel = null,
    string? ChallengeId = null,
    string? Nonce = null) : IRequest<RequestOtpResponse>, IBaseRequest;
