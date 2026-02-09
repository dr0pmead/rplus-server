using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RPlus.Auth.Application.Interfaces;
using System.Security.Claims;

namespace RPlus.Auth.Api.Controllers;

/// <summary>
/// Short Code API for partner scan fallback.
/// Allows users to generate 6-digit codes to dictate to cashiers
/// when QR scanning is unavailable.
/// </summary>
[ApiController]
[Route("api/auth/v1/shortcode")]
[Authorize]
public class ShortCodeController : ControllerBase
{
    private readonly IShortCodeService _shortCodeService;
    private readonly ILogger<ShortCodeController> _logger;

    public ShortCodeController(
        IShortCodeService shortCodeService,
        ILogger<ShortCodeController> logger)
    {
        _shortCodeService = shortCodeService;
        _logger = logger;
    }

    /// <summary>
    /// Generates a new short code for the authenticated user.
    /// The code is valid for 120 seconds and can only be used once.
    /// </summary>
    /// <response code="200">Short code generated successfully.</response>
    /// <response code="401">User is not authenticated.</response>
    /// <response code="500">Failed to generate code after retries.</response>
    [HttpPost("generate")]
    [ProducesResponseType(typeof(ShortCodeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Generate(CancellationToken ct)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier)
                          ?? User.FindFirstValue("sub");

        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            _logger.LogWarning("Short code generation failed: invalid user claim");
            return Unauthorized(new { error = "invalid_user_claim" });
        }

        var result = await _shortCodeService.GenerateAsync(userId, ct);

        if (!result.Success)
        {
            _logger.LogError("Short code generation failed for user {UserId}: {Error}", userId, result.Error);
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = result.Error });
        }

        return Ok(new ShortCodeResponse
        {
            Code = result.Code!,
            ExpiresInSeconds = result.ExpiresInSeconds,
            ValidUntil = result.ValidUntil
        });
    }
}

/// <summary>
/// Response for short code generation.
/// </summary>
public class ShortCodeResponse
{
    /// <summary>
    /// Formatted 6-digit code (e.g., "384 912").
    /// </summary>
    public required string Code { get; init; }

    /// <summary>
    /// Seconds until the code expires (120).
    /// </summary>
    public required int ExpiresInSeconds { get; init; }

    /// <summary>
    /// Absolute expiration timestamp (UTC).
    /// </summary>
    public required DateTime ValidUntil { get; init; }
}
