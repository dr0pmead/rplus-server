using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using RPlus.Kernel.Integration.Api.Services;
using System.Collections.Generic;

namespace RPlus.Kernel.Integration.Api.Controllers;

[ApiController]
[Route("api/integration/v1")]
[Route("v1")]
public sealed class IntegrationScanController : ControllerBase
{
    private readonly IPartnerScanService _scanService;
    private readonly IOptionsMonitor<IntegrationScanOptions> _options;

    public IntegrationScanController(IPartnerScanService scanService, IOptionsMonitor<IntegrationScanOptions> options)
    {
        _scanService = scanService;
        _options = options;
    }

    [HttpPost("scan")]
    public async Task<IActionResult> Scan([FromBody] IntegrationScanRequest request, CancellationToken cancellationToken)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.QrToken))
            return BadRequest(new { error = "missing_qr_token" });

        var headerName = _options.CurrentValue.ApiKeyHeaderName;
        if (!Request.Headers.TryGetValue(headerName, out var keyHeader) || string.IsNullOrWhiteSpace(keyHeader.ToString()))
            return Unauthorized(new { error = "missing_integration_key" });

        var signatureHeaderName = _options.CurrentValue.SignatureHeaderName;
        var signatureTimestampHeaderName = _options.CurrentValue.SignatureTimestampHeaderName;
        var signature = Request.Headers.TryGetValue(signatureHeaderName, out var signatureHeader)
            ? signatureHeader.ToString()
            : string.Empty;
        var signatureTimestamp = Request.Headers.TryGetValue(signatureTimestampHeaderName, out var timestampHeader)
            ? timestampHeader.ToString()
            : string.Empty;

        var traceId = HttpContext.TraceIdentifier;
        if (Request.Headers.TryGetValue("x-trace-id", out var traceHeader) && !string.IsNullOrWhiteSpace(traceHeader.ToString()))
            traceId = traceHeader.ToString();

        var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString();

        var contextHeaders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (Request.Headers.TryGetValue("x-scan-context", out var scanContext))
            contextHeaders["x-scan-context"] = scanContext.ToString();
        if (Request.Headers.TryGetValue("x-context-id", out var contextId))
            contextHeaders["x-context-id"] = contextId.ToString();
        if (Request.Headers.TryGetValue("x-device-id", out var deviceId))
            contextHeaders["x-device-id"] = deviceId.ToString();
        if (Request.Headers.TryGetValue("x-device-type", out var deviceType))
            contextHeaders["x-device-type"] = deviceType.ToString();

        var result = await _scanService.ScanAsync(
            keyHeader.ToString(),
            request.QrToken,
            signature,
            signatureTimestamp,
            clientIp,
            traceId,
            contextHeaders,
            cancellationToken);
        if (!result.Success)
            return StatusCode(result.StatusCode, new { error = result.Error });

        return Ok(result.Response);
    }
}

public sealed class IntegrationScanRequest
{
    public string QrToken { get; set; } = string.Empty;
}
