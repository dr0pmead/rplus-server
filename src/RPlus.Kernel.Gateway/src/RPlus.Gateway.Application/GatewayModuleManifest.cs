using RPlus.SDK.Core.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RPlus.Gateway.Application;

public class GatewayModuleManifest : IModuleManifest
{
    public string ModuleId => "gateway";
    public string Name => "Gateway Service";
    public Version ModuleVersion => new Version(1, 0, 0);

    public ModuleContractProfile Contract => new ModuleContractProfile("1.0.0", BreakingChangePolicy.Strict);
    public ModuleSecurityProfile Security => new ModuleSecurityProfile(true, Array.Empty<string>());
    public ModuleDataBudget DataBudget => new ModuleDataBudget(1024 * 1024 * 10, 1024 * 512);
    public ModuleRuntimeProfile Runtime => new ModuleRuntimeProfile
    {
        RequiresDatabase = true,
        RequiresKafka = true,
        RequiresRedis = true,
        IsCritical = true
    };
    public ModuleCacheProfile Cache => new ModuleCacheProfile(true, TimeSpan.FromMinutes(10));
    public ModuleMetricsProfile Metrics => new ModuleMetricsProfile(true, Array.Empty<string>());
    public ModuleHealthProfile Health => new ModuleHealthProfile(true, 30);

    public IEnumerable<PermissionDefinition> Permissions => Enumerable.Empty<PermissionDefinition>();
    public IEnumerable<ModuleDependency> Dependencies => Enumerable.Empty<ModuleDependency>();
    public IEnumerable<Type> PublishedEvents => Enumerable.Empty<Type>();
    public IEnumerable<Type> ConsumedEvents => Enumerable.Empty<Type>();
}
