// Decompiled with JetBrains decompiler
// Type: RPlus.Auth.Infrastructure.Services.PhoneUtil
// Assembly: RPlus.Auth.Infrastructure, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: C6806E10-ACC6-4CD0-B785-E31754B39FE4
// Assembly location: F:\RPlus Framework\Recovery\rplus-kernel-auth\RPlus.Auth.Infrastructure.dll

using RPlus.Auth.Application.Interfaces;
using System;
using System.CodeDom.Compiler;
using System.Text.RegularExpressions;
using System.Text.RegularExpressions.Generated;

#nullable enable
namespace RPlus.Auth.Infrastructure.Services;

public sealed class PhoneUtil : IPhoneUtil
{
  public string NormalizeToE164(string phone)
  {
    string str1 = !string.IsNullOrWhiteSpace(phone) ? PhoneUtil.DigitsOnly().Replace(phone, string.Empty) : throw new ArgumentException("Phone is required.", nameof (phone));
    if (string.IsNullOrEmpty(str1))
      throw new ArgumentException("Phone must contain digits.", nameof (phone));
    if (str1.StartsWith("00", StringComparison.Ordinal))
    {
      string str2 = str1;
      str1 = str2.Substring(2, str2.Length - 2);
    }
    if (str1.StartsWith("8", StringComparison.Ordinal) && str1.Length == 11)
    {
      string str3 = str1;
      str1 = "7" + str3.Substring(1, str3.Length - 1);
    }
    if (str1.Length == 10)
      str1 = "7" + str1;
    if (str1.Length < 10 || str1.Length > 15)
      throw new ArgumentException("Phone length must be between 10 and 15 digits after normalization.", nameof (phone));
    return "+" + str1;
  }

  private static readonly Regex DigitsOnlyRegex = new Regex("[^0-9]", RegexOptions.Compiled);

  private static Regex DigitsOnly() => DigitsOnlyRegex;
}
