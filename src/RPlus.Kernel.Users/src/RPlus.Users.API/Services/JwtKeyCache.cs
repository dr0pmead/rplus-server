// Decompiled with JetBrains decompiler
// Type: RPlus.Users.Api.Services.JwtKeyCache
// Assembly: ExecuteService, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 04655717-0A56-4995-8EE3-5A63B07DD93C
// Assembly location: F:\RPlus Framework\Recovery\users\ExecuteService.dll

using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;

#nullable enable
namespace RPlus.Users.Api.Services;

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
