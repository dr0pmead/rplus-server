// Decompiled with JetBrains decompiler
// Type: RPlus.Auth.Api.Controllers.JwksController
// Assembly: ExecuteService, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: E1DD3203-690E-448F-89A2-ED7CA219063C
// Assembly location: F:\RPlus Framework\Recovery\rplus-kernel-auth\ExecuteService.dll

using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using RPlus.Auth.Application.Interfaces;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;

#nullable enable
namespace RPlus.Auth.Api.Controllers;

[ApiController]
[Route("jwks")]
public sealed class JwksController : ControllerBase
{
  private readonly IJwtKeyProvider _keyProvider;

  public JwksController(IJwtKeyProvider keyProvider) => this._keyProvider = keyProvider;

  [HttpGet]
  public IActionResult Get()
  {
    IReadOnlyList<RsaSecurityKey> publicKeys = this._keyProvider.GetPublicKeys();
    List<object> objectList = new List<object>(publicKeys.Count);
    foreach (RsaSecurityKey rsaSecurityKey in (IEnumerable<RsaSecurityKey>) publicKeys)
    {
      RSAParameters? nullable = rsaSecurityKey.Rsa?.ExportParameters(false);
      if (nullable.HasValue)
        objectList.Add((object) new
        {
          kty = "RSA",
          use = "sig",
          alg = "RS256",
          kid = rsaSecurityKey.KeyId,
          n = Base64UrlEncoder.Encode(nullable.Value.Modulus ?? Array.Empty<byte>()),
          e = Base64UrlEncoder.Encode(nullable.Value.Exponent ?? Array.Empty<byte>())
        });
    }
    return (IActionResult) this.Ok((object) new
    {
      keys = objectList
    });
  }
}
