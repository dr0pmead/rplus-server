// Decompiled with JetBrains decompiler
// Type: RPlus.SDK.Core.Abstractions.IModuleManifest
// Assembly: RPlus.SDK.Core, Version=1.0.53.0, Culture=neutral, PublicKeyToken=null
// MVID: C7BF4574-BF4E-421C-9B89-0A828A452EA1
// Assembly location: F:\RPlus Framework\Recovery\loyalty\RPlus.SDK.Core.dll

using System;
using System.Collections.Generic;

#nullable enable
namespace RPlus.SDK.Core.Abstractions;

public interface IModuleManifest
{
  string ModuleId { get; }

  string Name { get; }

  Version ModuleVersion { get; }

  ModuleContractProfile Contract { get; }

  ModuleSecurityProfile Security { get; }

  ModuleDataBudget DataBudget { get; }

  ModuleRuntimeProfile Runtime { get; }

  ModuleCacheProfile Cache { get; }

  ModuleMetricsProfile Metrics { get; }

  ModuleHealthProfile Health { get; }

  IEnumerable<PermissionDefinition> Permissions { get; }

  IEnumerable<ModuleDependency> Dependencies { get; }

  IEnumerable<Type> PublishedEvents { get; }

  IEnumerable<Type> ConsumedEvents { get; }
}
