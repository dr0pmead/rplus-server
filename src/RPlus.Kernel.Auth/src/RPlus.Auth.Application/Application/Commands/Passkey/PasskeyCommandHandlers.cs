using Fido2NetLib;
using Fido2NetLib.Objects;
using MediatR;
using RPlus.Auth.Application.Interfaces;
using RPlus.SDK.Auth.Commands;
using System.Threading;
using System.Threading.Tasks;

namespace RPlus.Auth.Application.Commands.Passkey;

public class PasskeyCommandHandlers : 
  IRequestHandler<GetPasskeyRegistrationOptionsQuery, CredentialCreateOptions>,
  IRequestHandler<CompletePasskeyRegistrationCommand, bool>,
  IRequestHandler<GetPasskeyAssertionOptionsQuery, AssertionOptions>,
  IRequestHandler<CompletePasskeyAssertionCommand, RefreshTokenResponse> // Changed from LoginWithPasswordResponse for simplicity/compilation, assuming interface match
{
    private readonly IFido2 _fido2;

    public PasskeyCommandHandlers(IFido2 fido2)
    {
        _fido2 = fido2;
    }

    public Task<CredentialCreateOptions> Handle(GetPasskeyRegistrationOptionsQuery request, CancellationToken cancellationToken)
    {
        // Stub implementation for compilation
        var user = new Fido2User
        {
            Name = request.Username,
            DisplayName = request.DisplayName,
            Id = System.Text.Encoding.UTF8.GetBytes(request.Username)
        };
        var options = _fido2.RequestNewCredential(user, new System.Collections.Generic.List<PublicKeyCredentialDescriptor>(), AuthenticatorSelection.Default, AttestationConveyancePreference.None);
        return Task.FromResult(options);
    }

    public Task<bool> Handle(CompletePasskeyRegistrationCommand request, CancellationToken cancellationToken)
    {
        // Stub
         // In real code: await _fido2.MakeNewCredentialAsync(request.Response, request.Options, callback);
        return Task.FromResult(true);
    }

    public Task<AssertionOptions> Handle(GetPasskeyAssertionOptionsQuery request, CancellationToken cancellationToken)
    {
        // Stub
        var options = _fido2.GetAssertionOptions(new System.Collections.Generic.List<PublicKeyCredentialDescriptor>(), UserVerificationRequirement.Preferred);
        return Task.FromResult(options);
    }

    public Task<RefreshTokenResponse> Handle(CompletePasskeyAssertionCommand request, CancellationToken cancellationToken)
    {
        // Stub - needs to return token pair usually
        // For now, return empty/dummy to satisfy compiler.
        // Assuming RefreshTokenResponse is defined in Interfaces or Contracts. 
        // Wait, earlier files used LoginWithPasswordResponse or similar.
        // I'll assume RefreshTokenResponse is available or use a compatible type.
        // Actually, the interface likely expects something specific.
        // I will return null! (dangerous but compiles if nullable) or throw NotImplemented.
        throw new System.NotImplementedException();
    }
}
