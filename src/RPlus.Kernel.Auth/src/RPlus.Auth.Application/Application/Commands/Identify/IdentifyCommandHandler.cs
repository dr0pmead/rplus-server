// Decompiled with JetBrains decompiler
// Type: RPlus.Auth.Application.Commands.Identify.IdentifyCommandHandler
// Assembly: RPlus.Auth.Application, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 94419EED-98B7-4B52-A7B5-E1ADD668651C
// Assembly location: F:\RPlus Framework\Recovery\rplus-kernel-auth\RPlus.Auth.Application.dll

using MediatR;
using Microsoft.Extensions.Logging;
using RPlus.Auth.Application.Interfaces;
using RPlus.SDK.Auth.Commands;
using RPlus.Auth.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

#nullable enable
namespace RPlus.Auth.Application.Commands.Identify;

public class IdentifyCommandHandler : IRequestHandler<IdentifyCommand, IdentifyResponse>
{
  private readonly IAuthDataService _authDataService;
  private readonly IPhoneUtil _phoneUtil;
  private readonly ICryptoService _crypto;
  private readonly ILogger<IdentifyCommandHandler> _logger;

  public IdentifyCommandHandler(
    IAuthDataService authDataService,
    IPhoneUtil phoneUtil,
    ICryptoService crypto,
    ILogger<IdentifyCommandHandler> logger)
  {
    this._authDataService = authDataService;
    this._phoneUtil = phoneUtil;
    this._crypto = crypto;
    this._logger = logger;
  }

  public async Task<IdentifyResponse> Handle(
    IdentifyCommand request,
    CancellationToken cancellationToken)
  {
    this._logger.LogInformation("Identifying user: {Login}", (object) request.Login);
    List<AuthUserEntity> users = await this._authDataService.GetUsersByIdentifierAsync(request.Login, cancellationToken);
    string normalizedInput = request.Login.Trim().ToLowerInvariant();
    if (users.Count == 0)
    {
      try
      {
        string e164 = this._phoneUtil.NormalizeToE164(request.Login);
        if (!string.IsNullOrEmpty(e164))
        {
          AuthUserEntity byPhoneHashAsync = await this._authDataService.GetUserByPhoneHashAsync(this._crypto.HashPhone(e164), cancellationToken);
          if (byPhoneHashAsync != null)
            users.Add(byPhoneHashAsync);
        }
      }
      catch (ArgumentException ex)
      {
      }
    }
    if (users.Count == 0)
    {
      this._logger.LogWarning("User not found during identification: {Login}", (object) request.Login);
      return new IdentifyResponse(false);
    }
    AuthUserEntity selectedUser = (AuthUserEntity) null;
    AuthCredentialEntity selectedCredential = (AuthCredentialEntity) null;
    bool isPhoneInput = false;
    try
    {
      if (!string.IsNullOrEmpty(this._phoneUtil.NormalizeToE164(request.Login)))
        isPhoneInput = true;
    }
    catch
    {
    }
    foreach (AuthUserEntity authUserEntity in users)
    {
      AuthUserEntity user = authUserEntity;
      AuthCredentialEntity authCredentialAsync = await this._authDataService.GetAuthCredentialAsync(user.Id, cancellationToken);
      bool flag = authCredentialAsync != null && authCredentialAsync.PasswordHash != null && authCredentialAsync.PasswordHash.Length != 0;
      if (user.Login == normalizedInput && !isPhoneInput)
      {
        selectedUser = user;
        selectedCredential = authCredentialAsync;
        break;
      }
      if (isPhoneInput)
      {
        selectedUser = user;
        selectedCredential = authCredentialAsync;
        break;
      }
      if (flag && selectedUser == null)
      {
        selectedUser = user;
        selectedCredential = authCredentialAsync;
      }
      user = (AuthUserEntity) null;
    }
    if (selectedUser == null)
    {
      selectedUser = users.First<AuthUserEntity>();
      selectedCredential = await this._authDataService.GetAuthCredentialAsync(selectedUser.Id, cancellationToken);
    }
    if (selectedUser.IsBlocked)
    {
      this._logger.LogWarning("User {UserId} is blocked", (object) selectedUser.Id);
      return new IdentifyResponse(true, "blocked", "user_blocked");
    }
    string AuthMethod = "otp";
    bool flag1 = selectedCredential != null && selectedCredential.PasswordHash != null && selectedCredential.PasswordHash.Length != 0;
    bool flag2 = !string.IsNullOrEmpty(selectedUser.PhoneHash);
    if (isPhoneInput & flag2)
      AuthMethod = "otp";
    else if (flag1)
      AuthMethod = "password";
    else if (flag2)
      AuthMethod = "otp";
    this._logger.LogInformation("Selected user {UserId} with method {Method}", (object) selectedUser.Id, (object) AuthMethod);
    return new IdentifyResponse(true, AuthMethod);
  }
}
