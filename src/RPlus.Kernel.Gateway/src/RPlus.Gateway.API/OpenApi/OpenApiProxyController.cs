using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using System.Net.Http;

namespace RPlus.Gateway.Api.OpenApi;

[ApiController]
[Route("openapi")]
public sealed class OpenApiProxyController : ControllerBase
{
    private const string InternalHeaderName = "X-RPlus-Internal";
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMemoryCache _cache;
    private readonly IOptionsMonitor<OpenApiProxyOptions> _options;
    private readonly IConfiguration _configuration;

    public OpenApiProxyController(
        IHttpClientFactory httpClientFactory,
        IMemoryCache cache,
        IOptionsMonitor<OpenApiProxyOptions> options,
        IConfiguration configuration)
    {
        _httpClientFactory = httpClientFactory;
        _cache = cache;
        _options = options;
        _configuration = configuration;
    }

    [HttpGet("{service}/{doc}/swagger.json")]
    [Produces("application/json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Get(string service, string doc, CancellationToken ct)
    {
        var options = _options.CurrentValue ?? new OpenApiProxyOptions();
        if (!options.Enabled)
            return NotFound();

        service = (service ?? string.Empty).Trim();
        doc = (doc ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(service) || string.IsNullOrWhiteSpace(doc))
            return NotFound();

        if (!options.Services.TryGetValue(service, out var svc) || string.IsNullOrWhiteSpace(svc.BaseUrl))
            return NotFound();

        var cacheKey = $"openapi:{service}:{doc}";
        if (_cache.TryGetValue(cacheKey, out string? cached) && !string.IsNullOrWhiteSpace(cached))
        {
            return Content(cached, "application/json");
        }

        var baseUrl = svc.BaseUrl.TrimEnd('/');
        var upstreamDoc = string.IsNullOrWhiteSpace(doc) ? (svc.DocName ?? "v1") : doc;
        var upstream = $"{baseUrl}/swagger/{upstreamDoc}/swagger.json";

        try
        {
            var client = _httpClientFactory.CreateClient("OpenApiProxy");
            if (service.Equals("documents", StringComparison.OrdinalIgnoreCase))
            {
                var internalSecret = _configuration["Gateway:Internal:SharedSecret"]
                                    ?? _configuration["Gateway__Internal__SharedSecret"]
                                    ?? _configuration["RPLUS_INTERNAL_SERVICE_SECRET"];

                if (!string.IsNullOrWhiteSpace(internalSecret))
                {
                    client.DefaultRequestHeaders.TryAddWithoutValidation(InternalHeaderName, internalSecret);
                }
            }
            using var response = await client.GetAsync(upstream, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = "upstream_unavailable" });

            var json = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(json))
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = "empty_upstream" });

            _cache.Set(cacheKey, json, TimeSpan.FromMinutes(5));
            return Content(json, "application/json");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = "upstream_unavailable" });
        }
    }
}
