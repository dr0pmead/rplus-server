using System;
using System.Threading;
using System.Threading.Tasks;

namespace RPlus.Auth.Application.Interfaces;

public sealed record SystemAuthFlow(
    Guid UserId,
    string DeviceId,
    string ClientIp,
    string UserAgent,
    DateTime CreatedAtUtc);

public interface ISystemAuthFlowStore
{
    Task<string> CreateAsync(SystemAuthFlow flow, TimeSpan ttl, CancellationToken ct);
    Task<SystemAuthFlow?> GetAsync(string tempToken, CancellationToken ct);
    Task DeleteAsync(string tempToken, CancellationToken ct);
}

