using MediatR;
using RPlus.SDK.Core.Abstractions;

#nullable enable
namespace RPlus.SDK.Auth.Commands;

public record LoginWithPasswordCommand(
    string Phone,
    string Password,
    string? DeviceId,
    string? DeviceFingerprint,
    string? ClientIp,
    string? UserAgent,
    string? ChallengeId = null,
    string? Nonce = null) : IRequest<LoginWithPasswordResponse>, IBaseRequest;
