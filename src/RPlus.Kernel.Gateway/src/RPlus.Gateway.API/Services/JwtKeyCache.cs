using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;

namespace RPlus.Gateway.Api.Services;

public sealed class JwtKeyCache
{
    private IReadOnlyList<SecurityKey> _keys = Array.Empty<SecurityKey>();
    private readonly object _lock = new();

    public void UpdateKeys(IReadOnlyList<SecurityKey> keys)
    {
        lock (_lock)
        {
            _keys = keys;
        }
    }

    public IEnumerable<SecurityKey> GetKeys()
    {
        lock (_lock)
        {
            return _keys;
        }
    }
}

