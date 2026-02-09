namespace RPlus.SDK.Core.Abstractions;

using System;

#nullable enable

public record ModuleRuntimeProfile
{
    public bool RequiresDatabase { get; init; }
    public bool RequiresKafka { get; init; }
    public bool RequiresRedis { get; init; }
    public bool RequiresScheduler { get; init; }
    public bool RequiresExternalHttp { get; init; }
    public bool IsCritical { get; init; }
}

public record ModuleContractProfile(string Version, BreakingChangePolicy Policy);
public record ModuleSecurityProfile(bool RequiresAuth, string[] RequiredRoles);
public record ModuleDataBudget(long MaxStateSize, long MaxMessageSize);
public record ModuleCacheProfile(bool Enabled, TimeSpan? DefaultTtl);
public record ModuleMetricsProfile(bool Enabled, string[] CustomTags);
public record ModuleHealthProfile(bool Enabled, int CheckIntervalSeconds);
