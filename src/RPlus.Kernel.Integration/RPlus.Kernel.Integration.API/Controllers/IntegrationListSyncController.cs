using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using RPlus.Kernel.Integration.Api.Services;

namespace RPlus.Kernel.Integration.Api.Controllers;

[ApiController]
[Route("api/integration/v1/lists")]
public sealed class IntegrationListSyncController : ControllerBase
{
    private readonly IIntegrationListSyncService _syncService;
    private readonly IOptionsMonitor<IntegrationScanOptions> _options;

    public IntegrationListSyncController(
        IIntegrationListSyncService syncService,
        IOptionsMonitor<IntegrationScanOptions> options)
    {
        _syncService = syncService;
        _options = options;
    }

    [HttpPost("{listId:guid}/sync")]
    public async Task<IActionResult> Sync(
        Guid listId,
        [FromBody] IntegrationListSyncRequest request,
        CancellationToken cancellationToken)
    {
        if (request == null)
            return BadRequest(new { error = "missing_payload" });

        var headerName = _options.CurrentValue.ApiKeyHeaderName;
        if (!Request.Headers.TryGetValue(headerName, out var keyHeader) || string.IsNullOrWhiteSpace(keyHeader.ToString()))
            return Unauthorized(new { error = "missing_integration_key" });

        var result = await _syncService.SyncAsync(
            keyHeader.ToString(),
            listId,
            request,
            cancellationToken);

        if (!result.Success)
            return StatusCode(result.StatusCode, new { error = result.Error });

        return Ok(result.Response);
    }
}
