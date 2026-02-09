// Decompiled with JetBrains decompiler
// Type: RPlus.SDK.Core.Abstractions.PluginHeader
// Assembly: RPlus.SDK.Core, Version=1.0.53.0, Culture=neutral, PublicKeyToken=null
// MVID: C7BF4574-BF4E-421C-9B89-0A828A452EA1
// Assembly location: F:\RPlus Framework\Recovery\loyalty\RPlus.SDK.Core.dll

using System;

#nullable enable
namespace RPlus.SDK.Core.Abstractions;

public class PluginHeader
{
  public const string MagicString = "RPLG";
  public const int HeaderVersion = 1;

  public string Magic { get; set; } = "RPLG";

  public int FormatVersion { get; set; } = 1;

  public string PluginId { get; set; } = string.Empty;

  public string BuildHash { get; set; } = string.Empty;

  public byte[] Signature { get; set; } = Array.Empty<byte>();

  public bool IsValid => this.Magic == "RPLG" && this.FormatVersion > 0;
}
