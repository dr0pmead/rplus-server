// Decompiled with JetBrains decompiler
// Type: RPlus.Access.Api.Services.JwtKeyCache
// Assembly: ExecuteService, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 809913E0-E790-491D-8B90-21CE464D2E43
// Assembly location: F:\RPlus Framework\Recovery\access\ExecuteService.dll

using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;

#nullable enable
namespace RPlus.Access.Api.Services;

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
