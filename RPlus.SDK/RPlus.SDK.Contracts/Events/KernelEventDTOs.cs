// Decompiled with JetBrains decompiler
// Type: RPlus.Kernel.Core.Contracts.Events.ModuleEventDto
// Assembly: RPlus.SDK.Contracts, Version=1.0.53.0, Culture=neutral, PublicKeyToken=null
// MVID: A6C08EAE-EAE1-417A-A2D9-4C69FE3F3790
// Assembly location: F:\RPlus Framework\Recovery\loyalty\RPlus.SDK.Contracts.dll

using System;
using System.Collections.Generic;

#nullable enable
namespace RPlus.Kernel.Core.Contracts.Events;

public class ModuleEventDto
{
  public string EventId { get; set; } = Guid.NewGuid().ToString();

  public string EventType { get; set; } = string.Empty;

  public string ModuleId { get; set; } = string.Empty;

  public string ModuleName { get; set; } = string.Empty;

  public string Status { get; set; } = string.Empty;

  public Dictionary<string, object> Metadata { get; set; } = new Dictionary<string, object>();

  public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
