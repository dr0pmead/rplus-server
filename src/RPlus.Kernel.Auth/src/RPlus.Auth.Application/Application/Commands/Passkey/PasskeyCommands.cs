using Fido2NetLib;
using Fido2NetLib.Objects;
using MediatR;
using RPlus.Auth.Application.Interfaces;
using RPlus.SDK.Auth.Commands;

namespace RPlus.Auth.Application.Commands.Passkey;

public record GetPasskeyRegistrationOptionsQuery(string Username, string DisplayName) : IRequest<CredentialCreateOptions>;

public record CompletePasskeyRegistrationCommand(string Username, AuthenticatorAttestationRawResponse Response, CredentialCreateOptions Options) : IRequest<bool>;

public record GetPasskeyAssertionOptionsQuery(string? Username) : IRequest<AssertionOptions>;

public record CompletePasskeyAssertionCommand(AuthenticatorAssertionRawResponse Response, AssertionOptions Options, string? ClientIp = null, string? UserAgent = null) : IRequest<RefreshTokenResponse>;
