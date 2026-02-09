using Microsoft.AspNetCore.Mvc;
using RPlus.Loyalty.Application.Graph;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace RPlus.Loyalty.Api.Controllers;

[ApiController]
[Produces("application/json")]
[Route("api/loyalty/graph-nodes")]
public sealed class GraphNodesController : ControllerBase
{
    private readonly ILoyaltyGraphNodeCatalog _catalog;

    public GraphNodesController(ILoyaltyGraphNodeCatalog catalog)
    {
        _catalog = catalog;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<LoyaltyGraphNodeTemplate>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<LoyaltyGraphNodeTemplate>>> Get(CancellationToken ct)
    {
        var items = await _catalog.GetItemsAsync(ct);
        return Ok(items);
    }
}
