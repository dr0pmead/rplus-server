// Decompiled with JetBrains decompiler
// Type: RPlus.Users.Infrastructure.Services.CryptoService
// Assembly: RPlus.Users.Infrastructure, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 9CF06FE7-40AC-4ED9-B2CD-559A2CFCED24
// Assembly location: F:\RPlus Framework\Recovery\users\RPlus.Users.Infrastructure.dll

using RPlus.Users.Application.Interfaces.Services;
using System;
using System.Security.Cryptography;
using System.Text;

#nullable enable
namespace RPlus.Users.Infrastructure.Services;

public sealed class CryptoService : ICryptoService
{
  public string HashPhone(string phone)
  {
    if (string.IsNullOrWhiteSpace(phone))
      return string.Empty;
    string s = phone.Trim();
    if (!s.StartsWith("+"))
      s = "+" + s;
    using (SHA256 shA256 = SHA256.Create())
    {
      byte[] bytes = Encoding.UTF8.GetBytes(s);
      return Convert.ToHexString(shA256.ComputeHash(bytes)).ToLowerInvariant();
    }
  }
}
