using System;
using System.Threading;
using System.Threading.Tasks;

namespace RPlus.Loyalty.Application.Abstractions;

public interface ITenureLevelRecalculator
{
    Task<TenureRecalcResult> RecalculateAsync(TenureRecalcRequest request, CancellationToken ct = default);
    Task<SingleUserRecalcResult> RecalculateUserAsync(Guid userId, CancellationToken ct = default);
}

public sealed record TenureRecalcRequest(
    bool Force = false,
    int MaxParallel = 12,
    int BatchSize = 250);

public sealed record TenureRecalcResult(
    bool Success,
    int TotalUsers,
    int UpdatedUsers,
    string? LevelsHash,
    bool Skipped,
    string? Error);

public sealed record SingleUserRecalcResult(
    bool Success,
    string? Level,
    decimal Discount,
    bool Updated,
    string? Error);
