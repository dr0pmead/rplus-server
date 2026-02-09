// Decompiled with JetBrains decompiler
// Type: RPlus.Auth.Api.Services.AuthKeyGrpcService
// Assembly: ExecuteService, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: E1DD3203-690E-448F-89A2-ED7CA219063C
// Assembly location: F:\RPlus Framework\Recovery\rplus-kernel-auth\ExecuteService.dll

using Grpc.Core;
using Microsoft.Extensions.Logging;
using RPlus.Auth.Application.Interfaces;
using RPlus.Auth.Application.Models;
using RPlusGrpc.Auth;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

#nullable enable
namespace RPlus.Auth.Api.Services;

public class AuthKeyGrpcService : AuthKeyService.AuthKeyServiceBase
{
  private readonly IJwtKeyStore _keyStore;
  private readonly ILogger<AuthKeyGrpcService> _logger;

  public AuthKeyGrpcService(IJwtKeyStore keyStore, ILogger<AuthKeyGrpcService> logger)
  {
    this._keyStore = keyStore;
    this._logger = logger;
  }

  public override Task<GetPublicKeysResponse> GetPublicKeys(
    GetPublicKeysRequest request,
    ServerCallContext context)
  {
    try
    {
      IReadOnlyList<JwtKeyMaterial> allKeys = this._keyStore.GetAllKeys();
      GetPublicKeysResponse result = new GetPublicKeysResponse();
      foreach (JwtKeyMaterial jwtKeyMaterial in (IEnumerable<JwtKeyMaterial>) allKeys)
      {
        if (jwtKeyMaterial.ExpiresAt > DateTimeOffset.UtcNow)
          result.Keys.Add(new PublicKeyInfo()
          {
            Kid = jwtKeyMaterial.KeyId,
            Algorithm = "RS256",
            Pem = jwtKeyMaterial.PublicKeyPem,
            ExpiresAt = jwtKeyMaterial.ExpiresAt.ToUnixTimeSeconds(),
            Use = "sig"
          });
      }
      this._logger.LogDebug("Returned {Count} active public keys", (object) result.Keys.Count);
      return Task.FromResult<GetPublicKeysResponse>(result);
    }
    catch (Exception ex)
    {
      this._logger.LogError(ex, "Failed to get public keys");
      throw new RpcException(new Status(StatusCode.Internal, "Failed to retrieve public keys"));
    }
  }
}
