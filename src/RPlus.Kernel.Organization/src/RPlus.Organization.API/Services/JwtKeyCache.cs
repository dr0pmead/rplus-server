// Decompiled with JetBrains decompiler
// Type: RPlus.Organization.Api.Services.JwtKeyCache
// Assembly: ExecuteService, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 8ABF1D32-8F85-446A-8A49-54981F839476
// Assembly location: F:\RPlus Framework\Recovery\organization\ExecuteService.dll

using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;

#nullable enable
namespace RPlus.Organization.Api.Services;

public class JwtKeyCache
{
  private IReadOnlyList<SecurityKey> _keys = (IReadOnlyList<SecurityKey>) Array.Empty<SecurityKey>();
  private readonly object _lock = new object();

  public void UpdateKeys(IReadOnlyList<SecurityKey> keys)
  {
    lock (this._lock)
      this._keys = keys;
  }

  public IEnumerable<SecurityKey> GetKeys()
  {
    lock (this._lock)
      return (IEnumerable<SecurityKey>) this._keys;
  }
}
