using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using RPlus.Kernel.Integration.Api.Models.Partners;
using RPlus.Kernel.Integration.Api.Services;
using RPlus.Kernel.Integration.Infrastructure.Security;

namespace RPlus.Kernel.Integration.Api.Controllers;

/// <summary>
/// Partner integration endpoints for Intent → Commit flow.
/// Used by POS plugins (e.g., iiko).
/// </summary>
[ApiController]
[Route("api/partners")]
public class PartnerIntegrationController : ControllerBase
{
    private readonly IPartnerIntegrationService _service;
    private readonly IPartnerApiKeyValidator _apiKeyValidator;
    private readonly ILogger<PartnerIntegrationController> _logger;

    public PartnerIntegrationController(
        IPartnerIntegrationService service,
        IPartnerApiKeyValidator apiKeyValidator,
        ILogger<PartnerIntegrationController> logger)
    {
        _service = service;
        _apiKeyValidator = apiKeyValidator;
        _logger = logger;
    }

    /// <summary>
    /// Register a scan intent (before order is closed).
    /// Accepts either { "qrToken": "jwt" } or { "otpCode": "000000" }.
    /// Returns predicted discounts and ScanId for commit.
    /// </summary>
    [HttpPost("scan")]
    [ProducesResponseType(typeof(PartnerScanResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ScanAsync(
        [FromBody] PartnerScanRequest request,
        CancellationToken ct)
    {
        // Validate API key
        var integrationKey = Request.Headers["X-Integration-Key"].ToString();
        var keyResult = await _apiKeyValidator.ValidateAsync(integrationKey, ct);

        if (!keyResult.Success || keyResult.Context?.Metadata?.PartnerId is null)
        {
            return Unauthorized(new { error = keyResult.Error ?? "invalid_integration_key" });
        }

        var partnerId = keyResult.Context.Metadata.PartnerId.Value;

        // Validate that at least one token is provided
        var token = request.ResolvedToken;
        if (string.IsNullOrWhiteSpace(token))
        {
            return BadRequest(new { error = "missing_token", message = "Provide qrToken or otpCode" });
        }

        // Use explicit Idempotency-Key header if provided, otherwise compute
        var idempotencyKey = Request.Headers["Idempotency-Key"].ToString();
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            idempotencyKey = ComputeIdempotencyKey(token, request.OrderId);
        }

        try
        {
            var response = await _service.ProcessScanAsync(partnerId, idempotencyKey, request, ct);
            return Ok(response);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("expired"))
        {
            return BadRequest(new { error = "qr_token_expired", message = ex.Message });
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("invalid"))
        {
            return BadRequest(new { error = "invalid_qr_token", message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Scan failed: {Message}", ex.Message);
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Commit an order (lock financial facts).
    /// Must reference a valid ScanId.
    /// HMAC signature required for production keys.
    /// Headers: X-Integration-Key, Idempotency-Key (= scanId), X-Signature, X-Signature-Timestamp
    /// </summary>
    [HttpPost("orders/closed")]
    [PartnerSignatureAuth] // HMAC signature verification
    [ProducesResponseType(typeof(PartnerCommitResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CommitAsync(
        [FromBody] PartnerCommitRequest request,
        CancellationToken ct)
    {
        // Validate API key
        var integrationKey = Request.Headers["X-Integration-Key"].ToString();
        var keyResult = await _apiKeyValidator.ValidateAsync(integrationKey, ct);

        if (!keyResult.Success || keyResult.Context?.Metadata?.PartnerId is null)
        {
            return Unauthorized(new { error = keyResult.Error ?? "invalid_integration_key" });
        }

        var partnerId = keyResult.Context.Metadata.PartnerId.Value;

        // Validate Idempotency-Key = scanId
        var idempotencyKey = Request.Headers["Idempotency-Key"].ToString();
        if (!string.IsNullOrWhiteSpace(idempotencyKey) && idempotencyKey != request.ScanId.ToString())
        {
            _logger.LogWarning("Idempotency-Key mismatch: header={Header}, body.scanId={ScanId}",
                idempotencyKey, request.ScanId);
        }

        try
        {
            var response = await _service.ProcessCommitAsync(partnerId, request, ct);
            return Ok(response);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Commit failed - scan not found: {ScanId}", request.ScanId);
            return NotFound(new { error = "scan_not_found", scanId = request.ScanId });
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("mismatch"))
        {
            _logger.LogWarning(ex, "Commit failed - fraud attempt: {Message}", ex.Message);
            return Conflict(new { error = "order_id_mismatch", message = ex.Message });
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("cancelled"))
        {
            _logger.LogWarning(ex, "Commit failed - scan cancelled: {ScanId}", request.ScanId);
            return Conflict(new { error = "scan_cancelled", scanId = request.ScanId });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Commit failed: {Message}", ex.Message);
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Cancel a scan intent (order cancelled/storned before close).
    /// Headers: X-Integration-Key (required), Idempotency-Key = scanId (required)
    /// HMAC signature optional (verified if X-Signature header present).
    /// </summary>
    [HttpPost("orders/cancelled")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CancelAsync(
        [FromBody] PartnerCancelRequest request,
        CancellationToken ct)
    {
        // Validate API key
        var integrationKey = Request.Headers["X-Integration-Key"].ToString();
        var keyResult = await _apiKeyValidator.ValidateAsync(integrationKey, ct);

        if (!keyResult.Success || keyResult.Context?.Metadata?.PartnerId is null)
        {
            return Unauthorized(new { error = keyResult.Error ?? "invalid_integration_key" });
        }

        var partnerId = keyResult.Context.Metadata.PartnerId.Value;

        try
        {
            await _service.ProcessCancelAsync(partnerId, request, ct);
            return Ok();
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Cancel failed - scan not found: {ScanId}", request.ScanId);
            return NotFound(new { error = "scan_not_found", scanId = request.ScanId });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Cancel failed: {Message}", ex.Message);
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Receive telemetry events from POS plugins.
    /// Headers: X-Integration-Key (required), Idempotency-Key = eventId (required)
    /// No HMAC signature required.
    /// </summary>
    [HttpPost("events")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ReceiveEventAsync(
        [FromBody] PartnerEventRequest request,
        CancellationToken ct)
    {
        // Validate API key
        var integrationKey = Request.Headers["X-Integration-Key"].ToString();
        var keyResult = await _apiKeyValidator.ValidateAsync(integrationKey, ct);

        if (!keyResult.Success || keyResult.Context?.Metadata?.PartnerId is null)
        {
            return Unauthorized(new { error = keyResult.Error ?? "invalid_integration_key" });
        }

        var partnerId = keyResult.Context.Metadata.PartnerId.Value;
        var idempotencyKey = Request.Headers["Idempotency-Key"].ToString();

        try
        {
            await _service.ProcessEventAsync(partnerId, idempotencyKey, request, ct);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Event processing failed: {Message}", ex.Message);
            // Events should never fail the POS — always return 200
            return Ok();
        }
    }

    /// <summary>
    /// Generate OTP for a user (admin/testing only).
    /// Returns existing OTP if one is active, otherwise creates new.
    /// </summary>
    [HttpPost("otp/generate")]
    [ProducesResponseType(typeof(OtpGenerationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GenerateOtpAsync(
        [FromBody] OtpGenerationRequest request,
        [FromServices] IShortCodeValidator shortCodeValidator,
        CancellationToken ct)
    {
        if (request.UserId == Guid.Empty)
        {
            return BadRequest(new { error = "invalid_user_id" });
        }

        var result = await shortCodeValidator.GetOrCreateAsync(request.UserId, ct);

        if (!result.Success)
        {
            return BadRequest(new { error = result.Error });
        }

        return Ok(new OtpGenerationResponse
        {
            Code = result.Code!,
            ExpiresInSeconds = result.ExpiresIn
        });
    }

    /// <summary>
    /// Get existing OTP for a user if one is active.
    /// Does not generate a new code.
    /// </summary>
    [HttpGet("otp/{userId:guid}")]
    [ProducesResponseType(typeof(OtpGenerationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetOtpAsync(
        [FromRoute] Guid userId,
        [FromServices] IShortCodeValidator shortCodeValidator,
        CancellationToken ct)
    {
        if (userId == Guid.Empty)
        {
            return BadRequest(new { error = "invalid_user_id" });
        }

        var result = await shortCodeValidator.GetExistingAsync(userId, ct);

        if (result is null || !result.Success)
        {
            return NotFound(new { error = "no_active_otp" });
        }

        return Ok(new OtpGenerationResponse
        {
            Code = result.Code!,
            ExpiresInSeconds = result.ExpiresIn
        });
    }

    private static string ComputeIdempotencyKey(string qrToken, Guid orderId)
    {
        var input = $"{qrToken}:{orderId}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}

/// <summary>
/// Request to cancel a scan intent.
/// </summary>
public class PartnerCancelRequest
{
    public Guid ScanId { get; set; }
    public Guid OrderId { get; set; }
    public string? Reason { get; set; }
}

/// <summary>
/// Telemetry event from POS plugin.
/// </summary>
public class PartnerEventRequest
{
    public string EventId { get; set; } = string.Empty;
    public DateTime At { get; set; }
    public string Type { get; set; } = string.Empty;
    public Guid? ScanId { get; set; }
    public Guid? OrderId { get; set; }
    public string? TerminalId { get; set; }
    public JsonElement? Details { get; set; }
}

/// <summary>
/// Request to generate OTP for a user.
/// </summary>
public class OtpGenerationRequest
{
    public Guid UserId { get; set; }
}

/// <summary>
/// Response with generated OTP.
/// </summary>
public class OtpGenerationResponse
{
    public string Code { get; set; } = string.Empty;
    public int ExpiresInSeconds { get; set; }
}
