using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace RPlus.Loyalty.Application.Abstractions;

public sealed record LoyaltyLevelEntry(string Key, string Title, int Years, decimal Discount);

public interface ILoyaltyLevelCatalog
{
    Task<IReadOnlyList<LoyaltyLevelEntry>> GetLevelsAsync(CancellationToken ct = default);
}
