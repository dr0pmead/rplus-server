using Microsoft.AspNetCore.Mvc;
using RPlus.Kernel.Integration.Api.Services;

namespace RPlus.Kernel.Integration.Api.Controllers;

[ApiController]
[Route("api/integration/v1/scan/fields")]
public sealed class IntegrationScanFieldsController : ControllerBase
{
    private readonly IScanFieldCatalogService _catalogService;

    public IntegrationScanFieldsController(IScanFieldCatalogService catalogService)
    {
        _catalogService = catalogService;
    }

    [HttpGet]
    public async Task<IActionResult> GetFields(CancellationToken cancellationToken)
    {
        var catalog = await _catalogService.GetCatalogAsync(cancellationToken);
        var items = catalog.Fields.Values
            .Where(x => x.Expose)
            .OrderBy(x => x.SortOrder ?? int.MaxValue)
            .ThenBy(x => x.Group)
            .ThenBy(x => x.Title)
            .Select(x => new ScanFieldDto(x.Key, x.Title, x.Group, x.Type, x.Description, x.SortOrder, x.IsAdvanced, x.Expose))
            .ToList();

        return Ok(new { items });
    }
}
