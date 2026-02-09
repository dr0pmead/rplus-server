using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace RPlus.Loyalty.Application.Graph;

public interface ILoyaltyGraphNodeCatalog
{
    Task<IReadOnlyList<LoyaltyGraphNodeTemplate>> GetItemsAsync(CancellationToken ct = default);
}
