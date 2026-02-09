using RPlus.SDK.Gateway.Realtime;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

#nullable enable

namespace RPlus.Gateway.Api.Realtime;

public interface IRealtimePolicyService
{
    Task<IReadOnlyCollection<RealtimeEventDescriptor>> GetRegistryAsync(string userId, CancellationToken ct);
    Task<bool> IsAllowedAsync(string userId, string? requiredPermission, CancellationToken ct);
    Task<IReadOnlySet<string>> GetGrantedPermissionsAsync(string userId, CancellationToken ct);
}

