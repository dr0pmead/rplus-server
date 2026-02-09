// Decompiled with JetBrains decompiler
// Type: RPlus.Auth.Domain.Entities.PasskeyCredentialEntity
// Assembly: RPlus.Auth.Domain, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 30F033C3-72B9-4343-BF9A-347F69FE04BB
// Assembly location: F:\RPlus Framework\Recovery\rplus-kernel-auth\RPlus.Auth.Domain.dll

using System;
using System.ComponentModel.DataAnnotations;

#nullable enable
namespace RPlus.Auth.Domain.Entities;

public sealed class PasskeyCredentialEntity
{
  [Key]
  public byte[] DescriptorId { get; set; } = Array.Empty<byte>();

  public Guid UserId { get; set; }

  public byte[] PublicKey { get; set; } = Array.Empty<byte>();

  public byte[] UserHandle { get; set; } = Array.Empty<byte>();

  public uint SignatureCounter { get; set; }

  public string CredType { get; set; } = "public-key";

  public DateTime RegDate { get; set; }

  public Guid AaGuid { get; set; }

  public string? DeviceName { get; set; }

  public AuthUserEntity User { get; set; } = null!;
}
