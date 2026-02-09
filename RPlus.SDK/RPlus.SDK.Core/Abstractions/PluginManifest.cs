// Decompiled with JetBrains decompiler
// Type: RPlus.SDK.Core.Abstractions.PluginManifest
// Assembly: RPlus.SDK.Core, Version=1.0.53.0, Culture=neutral, PublicKeyToken=null
// MVID: C7BF4574-BF4E-421C-9B89-0A828A452EA1
// Assembly location: F:\RPlus Framework\Recovery\loyalty\RPlus.SDK.Core.dll

using System;

#nullable enable
namespace RPlus.SDK.Core.Abstractions;

public sealed class PluginManifest
{
  public string PluginId { get; set; } = string.Empty;

  public string Version { get; set; } = "1.0.0";

  public string EntryPoint { get; set; } = string.Empty;

  public string Type { get; set; } = "System";

  public bool RequiresLicense { get; set; }

  public string? UiBasePath { get; set; }

  public string? ManifestAssembly { get; set; }

  public string? ManifestType { get; set; }

  public string[] AdditionalAssemblies { get; set; } = Array.Empty<string>();

  public RuntimeRequirements RuntimeRequirements { get; set; } = new RuntimeRequirements();

  public string[] Dependencies { get; set; } = Array.Empty<string>();
}
