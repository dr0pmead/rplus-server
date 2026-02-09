// Decompiled with JetBrains decompiler
// Type: RPlus.SDK.Contracts.Events.ModuleRegisteredEvent
// Assembly: RPlus.SDK.Contracts, Version=1.0.53.0, Culture=neutral, PublicKeyToken=null
// MVID: A6C08EAE-EAE1-417A-A2D9-4C69FE3F3790
// Assembly location: F:\RPlus Framework\Recovery\loyalty\RPlus.SDK.Contracts.dll

using RPlus.SDK.Core.Abstractions;
using System.Collections.Generic;

#nullable enable
namespace RPlus.SDK.Contracts.Events;

public record ModuleRegisteredEvent
{
  public required string ModuleId { get; init; }

  public required string Version { get; init; }

  public required string Name { get; init; }

  public required IEnumerable<PermissionDefinition> Permissions { get; init; }

  public required IEnumerable<ModuleDependency> Dependencies { get; init; }

  public ModuleRegisteredEvent()
  {
  }
}
