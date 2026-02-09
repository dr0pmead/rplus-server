using Microsoft.EntityFrameworkCore;
using RPlus.Kernel.Integration.Domain.Entities;
using RPlus.Kernel.Integration.Infrastructure.Persistence;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

#nullable enable
namespace RPlus.Kernel.Integration.Infrastructure.Services;

public sealed class IntegrationStatsQueryService
{
    private readonly IntegrationDbContext _db;

    public IntegrationStatsQueryService(IntegrationDbContext db) => _db = db;

    public async Task<IReadOnlyList<IntegrationStatsSummaryRow>> GetSummaryAsync(
        IntegrationStatsQuery query,
        CancellationToken cancellationToken)
    {
        var q = _db.Set<IntegrationStatsEntry>()
            .AsNoTracking()
            .Where(x => x.CreatedAt >= query.From && x.CreatedAt <= query.To);

        if (query.PartnerId.HasValue)
            q = q.Where(x => x.PartnerId == query.PartnerId.Value);

        if (!string.IsNullOrWhiteSpace(query.Scope))
            q = q.Where(x => x.Scope == query.Scope);

        if (!string.IsNullOrWhiteSpace(query.Endpoint))
            q = q.Where(x => x.Endpoint == query.Endpoint);

        if (!string.IsNullOrWhiteSpace(query.Env))
            q = q.Where(x => x.Env == query.Env);

        var entries = await q
            .Select(x => new
            {
                x.Scope,
                x.Endpoint,
                x.StatusCode,
                x.LatencyMs
            })
            .ToListAsync(cancellationToken);

        if (entries.Count == 0)
            return Array.Empty<IntegrationStatsSummaryRow>();

        var grouped = entries
            .GroupBy(x => new { x.Scope, x.Endpoint, x.StatusCode })
            .Select(g =>
            {
                var latencies = g.Select(x => x.LatencyMs).OrderBy(x => x).ToArray();
                var count = (long)latencies.Length;
                var errorCount = g.LongCount(x => x.StatusCode >= 400);
                var avg = latencies.Length == 0 ? 0 : latencies.Average(x => (double)x);
                var max = latencies.Length == 0 ? 0 : latencies[^1];

                return new IntegrationStatsSummaryRow(
                    g.Key.Scope,
                    g.Key.Endpoint,
                    g.Key.StatusCode,
                    count,
                    errorCount,
                    avg,
                    max);
            })
            .OrderByDescending(x => x.Count)
            .ToList();

        return grouped;
    }
}
