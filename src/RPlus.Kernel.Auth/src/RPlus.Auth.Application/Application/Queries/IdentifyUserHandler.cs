// Decompiled with JetBrains decompiler
// Type: RPlus.Auth.Application.Commands.Identify.IdentifyUserHandler
// Assembly: RPlus.Auth.Application, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 94419EED-98B7-4B52-A7B5-E1ADD668651C
// Assembly location: F:\RPlus Framework\Recovery\rplus-kernel-auth\RPlus.Auth.Application.dll

using MediatR;
using RPlus.SDK.Auth.Queries;
using RPlus.Auth.Application.Interfaces;
using RPlus.Auth.Domain.Entities;
using System;
using System.Threading;
using System.Threading.Tasks;

#nullable enable
namespace RPlus.Auth.Application.Commands.Identify;

public class IdentifyUserHandler : IRequestHandler<IdentifyUserQuery, IdentifyUserResult>
{
  private readonly IAuthDataService _dataService;
  private readonly ICryptoService _cryptoService;
  private readonly IPhoneUtil _phoneUtil;

  public IdentifyUserHandler(IAuthDataService dataService, ICryptoService cryptoService, IPhoneUtil phoneUtil)
  {
    this._dataService = dataService;
    this._cryptoService = cryptoService;
    this._phoneUtil = phoneUtil;
  }

  public async Task<IdentifyUserResult> Handle(
    IdentifyUserQuery request,
    CancellationToken cancellationToken)
  {
    if (string.IsNullOrWhiteSpace(request.Identifier))
      return new IdentifyUserResult(false, (string) null, false);
    string identifier = request.Identifier.Trim();

    string normalizedPhone = (string) null;
    try
    {
      normalizedPhone = this._phoneUtil.NormalizeToE164(identifier);
    }
    catch (ArgumentException)
    {
    }

    if (!string.IsNullOrWhiteSpace(normalizedPhone))
    {
      string phoneHash = this._cryptoService.HashPhone(normalizedPhone);

      AuthUserEntity userByPhone = await this._dataService.GetUserByPhoneHashAsync(phoneHash, cancellationToken);
      if (userByPhone != null)
      {
        AuthCredentialEntity authCredentialAsync = await this._dataService.GetAuthCredentialAsync(userByPhone.Id, cancellationToken);
        bool hasPassword = authCredentialAsync != null && authCredentialAsync.PasswordHash != null && authCredentialAsync.PasswordHash.Length != 0;
        return new IdentifyUserResult(true, hasPassword ? "password" : "otp", userByPhone.IsBlocked);
      }

      AuthKnownUserEntity knownUserByPhone = await this._dataService.GetKnownUserByPhoneHashAsync(phoneHash, cancellationToken);
      if (knownUserByPhone != null)
        return new IdentifyUserResult(true, "otp", !knownUserByPhone.IsActive);

      if (normalizedPhone.StartsWith("+", StringComparison.Ordinal))
      {
        string digitsOnlyPhone = normalizedPhone.Substring(1, normalizedPhone.Length - 1);
        string legacyPhoneHash = this._cryptoService.HashPhone(digitsOnlyPhone);

        AuthUserEntity legacyUserByPhone = await this._dataService.GetUserByPhoneHashAsync(legacyPhoneHash, cancellationToken);
        if (legacyUserByPhone != null)
        {
          AuthCredentialEntity legacyCredential = await this._dataService.GetAuthCredentialAsync(legacyUserByPhone.Id, cancellationToken);
          bool legacyHasPassword = legacyCredential != null && legacyCredential.PasswordHash != null && legacyCredential.PasswordHash.Length != 0;
          return new IdentifyUserResult(true, legacyHasPassword ? "password" : "otp", legacyUserByPhone.IsBlocked);
        }

        AuthKnownUserEntity legacyKnownUserByPhone = await this._dataService.GetKnownUserByPhoneHashAsync(legacyPhoneHash, cancellationToken);
        if (legacyKnownUserByPhone != null)
          return new IdentifyUserResult(true, "otp", !legacyKnownUserByPhone.IsActive);
      }

      return new IdentifyUserResult(false, (string) null, false);
    }

    AuthUserEntity userByIdentifier = await this._dataService.GetUserByIdentifierAsync(identifier, cancellationToken);
    if (userByIdentifier == null)
      return new IdentifyUserResult(false, (string) null, false);

    AuthCredentialEntity credential = await this._dataService.GetAuthCredentialAsync(userByIdentifier.Id, cancellationToken);
    bool hasPasswordByIdentifier = credential != null && credential.PasswordHash != null && credential.PasswordHash.Length != 0;
    return new IdentifyUserResult(true, hasPasswordByIdentifier ? "password" : "otp", userByIdentifier.IsBlocked);
  }
}
