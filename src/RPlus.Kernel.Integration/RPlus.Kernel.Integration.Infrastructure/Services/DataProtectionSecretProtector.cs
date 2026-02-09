// Decompiled with JetBrains decompiler
// Type: RPlus.Kernel.Integration.Infrastructure.Services.DataProtectionSecretProtector
// Assembly: RPlus.Kernel.Integration.Infrastructure, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 62B7ABAE-4A2B-4AF9-BC30-AC25C64E0B51
// Assembly location: F:\RPlus Framework\Recovery\integration\app\RPlus.Kernel.Integration.Infrastructure.dll

using Microsoft.AspNetCore.DataProtection;

#nullable enable
namespace RPlus.Kernel.Integration.Infrastructure.Services;

public sealed class DataProtectionSecretProtector : ISecretProtector
{
  private readonly IDataProtector _protector;

  public DataProtectionSecretProtector(IDataProtectionProvider provider)
  {
    this._protector = provider.CreateProtector("rplus.integration.api-keys");
  }

  public string Protect(string plaintext) => this._protector.Protect(plaintext);

  public string Unprotect(string protectedValue) => this._protector.Unprotect(protectedValue);
}
