using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace RPlus.Kernel.Integration.Application.Services;

public sealed record ScanContextPolicy(bool Invalidate, int? TtlSeconds);

public sealed record ScanContextResult(
    string ContextType,
    string ContextId,
    ScanContextPolicy Policy,
    Guid IntegrationId,
    Guid ApiKeyId);

public interface IScanContextResolver
{
    Task<ScanContextResult> ResolveAsync(
        Guid integrationId,
        Guid apiKeyId,
        IReadOnlyDictionary<string, string> headers,
        CancellationToken cancellationToken);
}

public interface IScanVisitStore
{
    Task<bool> ToggleVisitAsync(
        string contextId,
        string userId,
        TimeSpan ttl,
        CancellationToken cancellationToken);
}

public sealed class ScanContextResolutionException : Exception
{
    public string Error { get; }

    public ScanContextResolutionException(string error)
        : base(error)
    {
        Error = error;
    }
}
