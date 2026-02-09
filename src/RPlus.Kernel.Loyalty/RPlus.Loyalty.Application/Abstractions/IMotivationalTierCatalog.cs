using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace RPlus.Loyalty.Application.Abstractions;

/// <summary>
/// A single motivational tier entry from Meta service.
/// </summary>
/// <param name="Key">Tier key (code), e.g. "bronze", "silver"</param>
/// <param name="Title">Human-readable title</param>
/// <param name="MinPoints">Minimum points required to reach this tier</param>
/// <param name="Discount">Discount percentage for this tier</param>
public sealed record MotivationalTierEntry(string Key, string Title, int MinPoints, decimal Discount);

/// <summary>
/// Catalog for motivational discount tiers, loaded from Meta service.
/// Tiers are completely flexible - can have any number of levels.
/// </summary>
public interface IMotivationalTierCatalog
{
    /// <summary>
    /// Get all active motivational tiers ordered by MinPoints.
    /// </summary>
    Task<IReadOnlyList<MotivationalTierEntry>> GetTiersAsync(CancellationToken ct = default);
}
