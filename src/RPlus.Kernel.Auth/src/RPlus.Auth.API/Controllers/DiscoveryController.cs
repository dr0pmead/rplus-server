// Decompiled with JetBrains decompiler
// Type: RPlus.Auth.Api.Controllers.DiscoveryController
// Assembly: ExecuteService, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: E1DD3203-690E-448F-89A2-ED7CA219063C
// Assembly location: F:\RPlus Framework\Recovery\rplus-kernel-auth\ExecuteService.dll

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using RPlus.Auth.Options;
using System;

#nullable enable
namespace RPlus.Auth.Api.Controllers;

[ApiController]
[Route(".well-known")]
public sealed class DiscoveryController : ControllerBase
{
  private readonly JwtOptions _jwtOptions;

  public DiscoveryController(IOptions<JwtOptions> jwtOptions)
  {
    this._jwtOptions = jwtOptions.Value;
  }

  [HttpGet("openid-configuration")]
  public IActionResult GetOpenIdConfiguration()
  {
    string input = $"{this.Request.Scheme}://{this.Request.Host}";
    string baseUri = string.IsNullOrWhiteSpace(this._jwtOptions.Issuer) ? DiscoveryController.EnsureTrailingSlash(input) : DiscoveryController.EnsureTrailingSlash(this._jwtOptions.Issuer);
    var data = new
    {
      issuer = baseUri,
      jwks_uri = DiscoveryController.Combine(baseUri, "jwks"),
      authorization_endpoint = DiscoveryController.Combine(baseUri, "auth/otp/request"),
      token_endpoint = DiscoveryController.Combine(baseUri, "auth/otp/verify"),
      revocation_endpoint = DiscoveryController.Combine(baseUri, "auth/logout"),
      grant_types_supported = new string[2]
      {
        "authorization_code",
        "refresh_token"
      },
      response_types_supported = new string[1]{ "code" },
      scopes_supported = new string[2]
      {
        "openid",
        "profile"
      },
      subject_types_supported = new string[1]{ "public" },
      token_endpoint_auth_methods_supported = new string[1]
      {
        "none"
      }
    };
    return (IActionResult) this.Ok((object) data);
  }

  private static string Combine(string baseUri, string path)
  {
    return DiscoveryController.EnsureTrailingSlash(baseUri) + path;
  }

  private static string EnsureTrailingSlash(string input)
  {
    return !input.EndsWith("/", StringComparison.Ordinal) ? input + "/" : input;
  }
}
