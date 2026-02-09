// Decompiled with JetBrains decompiler
// Type: RPlus.Kernel.Integration.Infrastructure.Services.ISecretProtector
// Assembly: RPlus.Kernel.Integration.Infrastructure, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 62B7ABAE-4A2B-4AF9-BC30-AC25C64E0B51
// Assembly location: F:\RPlus Framework\Recovery\integration\app\RPlus.Kernel.Integration.Infrastructure.dll

#nullable enable
namespace RPlus.Kernel.Integration.Infrastructure.Services;

public interface ISecretProtector
{
  string Protect(string plaintext);

  string Unprotect(string protectedValue);
}
