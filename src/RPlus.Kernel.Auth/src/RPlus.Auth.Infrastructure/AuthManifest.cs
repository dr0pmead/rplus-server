// Decompiled with JetBrains decompiler
// Type: RPlus.Auth.Infrastructure.AuthManifest
// Assembly: RPlus.Auth.Infrastructure, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: C6806E10-ACC6-4CD0-B785-E31754B39FE4
// Assembly location: F:\RPlus Framework\Recovery\rplus-kernel-auth\RPlus.Auth.Infrastructure.dll

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RPlus.Auth.Options;
using RPlus.SDK.Core.Abstractions;
using RPlus.SDK.Infrastructure.Abstractions;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

#nullable enable
namespace RPlus.Auth.Infrastructure;

public sealed class AuthManifest : IModuleManifest, IModuleLifecycle, IModuleStartup
{
  public string ModuleId => "rplus.auth";

  public string Name => "Kernel Auth";

  public Version ModuleVersion => new Version(1, 0, 0);

  public ModuleRuntimeProfile Runtime
  {
    get
    {
      return new ModuleRuntimeProfile()
      {
        RequiresDatabase = true,
        RequiresKafka = true,
        RequiresRedis = true,
        RequiresScheduler = false,
        RequiresExternalHttp = false,
        IsCritical = true
      };
    }
  }

  public ModuleContractProfile Contract => new ModuleContractProfile("1.0", BreakingChangePolicy.Strict);

  public ModuleSecurityProfile Security => new ModuleSecurityProfile(false, Array.Empty<string>());

  public ModuleDataBudget DataBudget => new ModuleDataBudget(200, 2);

  public IEnumerable<PermissionDefinition> Permissions => new[] 
  { 
      new PermissionDefinition("kernel.auth.login", "Kernel admin authentication", "Kernel") 
  };

  public IEnumerable<ModuleDependency> Dependencies => Array.Empty<ModuleDependency>();

  public IEnumerable<Type> PublishedEvents => Array.Empty<Type>();

  public IEnumerable<Type> ConsumedEvents => Array.Empty<Type>();

  public ModuleCacheProfile Cache => new ModuleCacheProfile(true, null);

  public ModuleMetricsProfile Metrics => new ModuleMetricsProfile(true, Array.Empty<string>());

  public ModuleHealthProfile Health => new ModuleHealthProfile(true, 5000);

  public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
  {
    services.AddOptions<JwtOptions>().BindConfiguration<JwtOptions>("Jwt");
    services.AddInfrastructure(configuration);
  }

  public Task OnStartAsync() => Task.CompletedTask;

  public Task OnStopAsync() => Task.CompletedTask;

  public Task OnUpgradeAsync(string fromVersion) => Task.CompletedTask;

  public Task OnStateChanged(ModuleRunState newState) => Task.CompletedTask;
}
