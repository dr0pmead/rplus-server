using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RPlus.Gateway.Api.Controllers;

[ApiController]
[Route("api/internal/auth")]
public sealed class InternalAuthController : ControllerBase
{
    private const string InternalHeaderName = "X-RPlus-Internal";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<InternalAuthController> _logger;

    public InternalAuthController(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<InternalAuthController> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    [HttpGet("jwks")]
    [AllowAnonymous]
    [Produces("application/json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetJwks(CancellationToken ct)
    {
        var sharedSecret = _configuration["Gateway:Internal:SharedSecret"]
                          ?? _configuration["Gateway__Internal__SharedSecret"]
                          ?? _configuration["RPLUS_INTERNAL_SERVICE_SECRET"];

        if (string.IsNullOrWhiteSpace(sharedSecret))
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = "internal_secret_missing" });

        if (!Request.Headers.TryGetValue(InternalHeaderName, out var provided) ||
            string.IsNullOrWhiteSpace(provided))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "forbidden" });
        }

        if (!FixedTimeEquals(provided.ToString(), sharedSecret))
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "forbidden" });

        var authHttp = _configuration["Services:Auth:Http"]
                      ?? _configuration["Services__Auth__Http"]
                      ?? "http://rplus-kernel-auth:5006";

        var upstream = $"{authHttp.TrimEnd('/')}/jwks";

        try
        {
            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(5);

            using var response = await client.GetAsync(upstream, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Internal JWKS proxy failed with {StatusCode}", response.StatusCode);
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = "upstream_unavailable" });
            }

            var json = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(json))
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = "empty_upstream" });

            return Content(json, "application/json");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Internal JWKS proxy failed.");
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = "upstream_unavailable" });
        }
    }

    private static bool FixedTimeEquals(string left, string right)
    {
        var leftBytes = Encoding.UTF8.GetBytes(left);
        var rightBytes = Encoding.UTF8.GetBytes(right);
        return CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
    }
}
