using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using RPlus.Gateway.Api.Auth;
using RPlus.Gateway.Api.Services;
using RPlusGrpc.Auth;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace RPlus.Gateway.Api.Controllers;

[ApiController]
[Route("api/partners")]
public sealed class PartnersController : ControllerBase
{
    private readonly AuthService.AuthServiceClient _auth;
    private readonly PermissionGuard _permissionGuard;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<PartnersController> _logger;
    private readonly AuthCookieOptions _cookies;

    public PartnersController(
        AuthService.AuthServiceClient auth,
        PermissionGuard permissionGuard,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        IOptions<AuthCookieOptions> cookies,
        ILogger<PartnersController> logger)
    {
        _auth = auth;
        _permissionGuard = permissionGuard;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
        _cookies = cookies.Value;
    }

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> Create([FromBody] CreatePartnerUserRequest request, CancellationToken ct)
    {
        var denied = await RequirePermissionAsync("users.manage", ct);
        if (denied is not null) return denied;

        if (request is null)
            return BadRequest(new { error = "invalid_request" });

        var missing = new List<string>(8);
        if (request.ApplicationId == Guid.Empty) missing.Add("applicationId");
        if (string.IsNullOrWhiteSpace(request.Login)) missing.Add("login");
        if (string.IsNullOrWhiteSpace(request.Email)) missing.Add("email");
        if (string.IsNullOrWhiteSpace(request.Phone)) missing.Add("phone");
        if (string.IsNullOrWhiteSpace(request.Password)) missing.Add("password");
        if (string.IsNullOrWhiteSpace(request.FirstName)) missing.Add("firstName");
        if (string.IsNullOrWhiteSpace(request.LastName)) missing.Add("lastName");
        if (missing.Count > 0)
            return BadRequest(new { error = "invalid_request", missing });

        var tenantFromClaim = User.FindFirst("tenant_id")?.Value ?? User.FindFirst("tenantId")?.Value;
        Guid tenantGuid = Guid.Empty;
        if (!string.IsNullOrWhiteSpace(tenantFromClaim))
            Guid.TryParse(tenantFromClaim, out tenantGuid);

        var authHttp = _configuration["Services:Auth:Http"]
                      ?? _configuration["Services__Auth__Http"]
                      ?? "http://rplus-kernel-auth:5006";

        var createdUserId = await CreateAuthUserAsync(authHttp, request, tenantGuid, ct);
        if (createdUserId == Guid.Empty)
            return StatusCode(502, new { error = "auth_create_failed" });

        var bearer = TryGetAccessToken();
        var tenantHeader = Request.Headers.TryGetValue("X-Tenant-Id", out var tenantHeaderValue) ? tenantHeaderValue.ToString() : null;
        var appHeader = Request.Headers.TryGetValue("X-App-Id", out var appHeaderValue) ? appHeaderValue.ToString() : null;

        await TryLinkPartnerToApplicationAsync(createdUserId, request.ApplicationId, bearer, tenantHeader, appHeader, ct);

        var roleCode = string.IsNullOrWhiteSpace(request.RoleCode) ? "partner" : request.RoleCode.Trim();
        await TryAssignAccessRoleAsync(createdUserId, roleCode, bearer, tenantHeader, appHeader, ct);

        return Ok(new { userId = createdUserId.ToString(), applicationId = request.ApplicationId });
    }

    [HttpPost("scan")]
    [AllowAnonymous]
    public async Task<IActionResult> Scan([FromBody] PartnerScanRequest request, CancellationToken ct)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.QrToken))
            return BadRequest(new { error = "missing_qr_token" });

        if (!Request.Headers.TryGetValue("X-Integration-Key", out var keyHeader) || string.IsNullOrWhiteSpace(keyHeader.ToString()))
            return Unauthorized(new { error = "missing_integration_key" });

        var integrationHttp = _configuration["Gateway:Upstreams:integration:BaseAddress"]
                              ?? _configuration["Gateway__Upstreams__integration__BaseAddress"]
                              ?? "http://rplus-kernel-integration:5008";

        var client = CreateInternalClient(integrationHttp, bearer: null, tenantId: null, appId: null);
        client.DefaultRequestHeaders.Remove("X-Integration-Key");
        client.DefaultRequestHeaders.TryAddWithoutValidation("X-Integration-Key", keyHeader.ToString());

        var res = await client.PostAsJsonAsync(
            "/api/integration/v1/scan",
            new { qrToken = request.QrToken },
            new JsonSerializerOptions(JsonSerializerDefaults.Web),
            ct);

        var body = await res.Content.ReadAsStringAsync(ct);
        var contentType = res.Content.Headers.ContentType?.ToString() ?? "application/json";
        if (!res.IsSuccessStatusCode)
        {
            return new ContentResult
            {
                StatusCode = (int)res.StatusCode,
                ContentType = contentType,
                Content = string.IsNullOrWhiteSpace(body) ? "{\"error\":\"upstream_error\"}" : body
            };
        }

        return Content(body, contentType);
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        var denied = await RequirePermissionAsync("access.partner.self.read", ct);
        if (denied is not null) return denied;

        var accessHttp = _configuration["Services:Access:Http"]
                        ?? _configuration["Services__Access__Http"]
                        ?? "http://rplus-kernel-access:5002";

        var bearer = TryGetAccessToken();
        var client = CreateInternalClient(accessHttp, bearer, tenantId: null, appId: null);
        var res = await client.GetAsync("/api/access/partner-links/me", ct);
        if (!res.IsSuccessStatusCode)
            return StatusCode((int)res.StatusCode, new { error = "access_error" });

        var json = await res.Content.ReadAsStringAsync(ct);
        return Content(json, "application/json");
    }

    [HttpGet("me/keys")]
    [Authorize]
    public async Task<IActionResult> MyKeys(CancellationToken ct)
    {
        var denied = await RequirePermissionAsync("access.partner.self.read", ct);
        if (denied is not null) return denied;

        var accessHttp = _configuration["Services:Access:Http"]
                        ?? _configuration["Services__Access__Http"]
                        ?? "http://rplus-kernel-access:5002";

        var bearer = TryGetAccessToken();
        var client = CreateInternalClient(accessHttp, bearer, tenantId: null, appId: null);
        var res = await client.GetAsync("/api/access/partner-links/me/keys", ct);
        if (!res.IsSuccessStatusCode)
            return StatusCode((int)res.StatusCode, new { error = "access_error" });

        var json = await res.Content.ReadAsStringAsync(ct);
        return Content(json, "application/json");
    }

    /// <summary>
    /// Generate OTP for a user (admin testing only).
    /// </summary>
    [HttpPost("otp/generate")]
    [Authorize]
    public async Task<IActionResult> GenerateOtp([FromBody] OtpGenerationRequest request, CancellationToken ct)
    {
        var denied = await RequirePermissionAsync("integrations.manage", ct);
        if (denied is not null) return denied;

        if (request?.UserId == null || request.UserId == Guid.Empty)
            return BadRequest(new { error = "invalid_user_id" });

        var integrationHttp = _configuration["Gateway:Upstreams:integration:BaseAddress"]
                              ?? _configuration["Gateway__Upstreams__integration__BaseAddress"]
                              ?? "http://rplus-kernel-integration:5008";

        var bearer = TryGetAccessToken();
        var client = CreateInternalClient(integrationHttp, bearer, tenantId: null, appId: null);
        var res = await client.PostAsJsonAsync(
            "/api/partners/otp/generate",
            new { userId = request.UserId },
            new JsonSerializerOptions(JsonSerializerDefaults.Web),
            ct);

        var body = await res.Content.ReadAsStringAsync(ct);
        var contentType = res.Content.Headers.ContentType?.ToString() ?? "application/json";
        if (!res.IsSuccessStatusCode)
        {
            return new ContentResult
            {
                StatusCode = (int)res.StatusCode,
                ContentType = contentType,
                Content = string.IsNullOrWhiteSpace(body) ? "{\"error\":\"upstream_error\"}" : body
            };
        }

        return Content(body, contentType);
    }

    /// <summary>
    /// Get existing OTP for a user (admin testing only).
    /// </summary>
    [HttpGet("otp/{userId:guid}")]
    [Authorize]
    public async Task<IActionResult> GetOtp([FromRoute] Guid userId, CancellationToken ct)
    {
        var denied = await RequirePermissionAsync("integrations.manage", ct);
        if (denied is not null) return denied;

        if (userId == Guid.Empty)
            return BadRequest(new { error = "invalid_user_id" });

        var integrationHttp = _configuration["Gateway:Upstreams:integration:BaseAddress"]
                              ?? _configuration["Gateway__Upstreams__integration__BaseAddress"]
                              ?? "http://rplus-kernel-integration:5008";

        var bearer = TryGetAccessToken();
        var client = CreateInternalClient(integrationHttp, bearer, tenantId: null, appId: null);
        var res = await client.GetAsync($"/api/partners/otp/{userId:D}", ct);

        var body = await res.Content.ReadAsStringAsync(ct);
        var contentType = res.Content.Headers.ContentType?.ToString() ?? "application/json";
        
        return new ContentResult
        {
            StatusCode = (int)res.StatusCode,
            ContentType = contentType,
            Content = string.IsNullOrWhiteSpace(body) ? "{\"error\":\"unknown\"}" : body
        };
    }

    private async Task<IActionResult?> RequirePermissionAsync(string permissionId, CancellationToken ct)
    {
        var (allowed, error) = await _permissionGuard.CheckAsync(HttpContext, permissionId, ct);
        if (allowed) return null;

        var status = error switch
        {
            "unauthorized" => 401,
            "access_unavailable" => 503,
            "access_error" => 502,
            _ => 403
        };

        return StatusCode(status, new { error = error ?? "forbidden", permission = permissionId });
    }

    private string? TryGetAccessToken()
    {
        if (Request.Headers.TryGetValue(HeaderNames.Authorization, out var header) && !string.IsNullOrWhiteSpace(header))
            return header.ToString();

        if (Request.Cookies.TryGetValue(_cookies.AccessTokenCookieName, out var token) && !string.IsNullOrWhiteSpace(token))
            return $"Bearer {token}";

        if (Request.Cookies.TryGetValue("access_token", out var legacy) && !string.IsNullOrWhiteSpace(legacy))
            return $"Bearer {legacy}";

        return null;
    }

    private HttpClient CreateInternalClient(string baseAddress, string? bearer, string? tenantId, string? appId)
    {
        var client = _httpClientFactory.CreateClient();
        client.BaseAddress = new Uri(baseAddress.TrimEnd('/'));
        client.DefaultRequestHeaders.Remove(HeaderNames.Authorization);
        if (!string.IsNullOrWhiteSpace(bearer))
            client.DefaultRequestHeaders.TryAddWithoutValidation(HeaderNames.Authorization, bearer);

        if (!string.IsNullOrWhiteSpace(tenantId))
        {
            client.DefaultRequestHeaders.Remove("x-tenant-id");
            client.DefaultRequestHeaders.TryAddWithoutValidation("x-tenant-id", tenantId);
        }

        if (!string.IsNullOrWhiteSpace(appId))
        {
            client.DefaultRequestHeaders.Remove("x-app-id");
            client.DefaultRequestHeaders.TryAddWithoutValidation("x-app-id", appId);
        }

        return client;
    }

    private async Task<Guid> CreateAuthUserAsync(string authHttpBase, CreatePartnerUserRequest request, Guid tenantId, CancellationToken ct)
    {
        try
        {
            var client = CreateInternalClient(authHttpBase, bearer: null, tenantId: null, appId: null);
            var res = await client.PostAsJsonAsync(
                "/api/v1/auth/admin/users/create",
                new
                {
                    login = request.Login.Trim(),
                    email = request.Email.Trim(),
                    phone = request.Phone.Trim(),
                    password = request.Password,
                    firstName = request.FirstName.Trim(),
                    lastName = request.LastName.Trim(),
                    middleName = string.IsNullOrWhiteSpace(request.MiddleName) ? null : request.MiddleName.Trim(),
                    userType = 1, // Platform (partner)
                    tenantId = tenantId == Guid.Empty ? (Guid?)null : tenantId
                },
                new JsonSerializerOptions(JsonSerializerDefaults.Web),
                ct);

            if (!res.IsSuccessStatusCode)
            {
                var body = await res.Content.ReadAsStringAsync(ct);
                _logger.LogWarning("Auth admin create partner failed. Status={Status}. Body={Body}", (int)res.StatusCode, body);
                return Guid.Empty;
            }

            var payload = await res.Content.ReadFromJsonAsync<CreateUserResponse>(cancellationToken: ct);
            return payload?.UserId ?? Guid.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Auth admin create partner call failed.");
            return Guid.Empty;
        }
    }

    private async Task TryAssignAccessRoleAsync(Guid userId, string roleCode, string? bearer, string? tenantId, string? appId, CancellationToken ct)
    {
        try
        {
            var accessHttp = _configuration["Services:Access:Http"]
                            ?? _configuration["Services__Access__Http"]
                            ?? "http://rplus-kernel-access:5002";

            var client = CreateInternalClient(accessHttp, bearer, tenantId, appId);
            var res = await client.PostAsJsonAsync(
                $"/api/access/users/{userId:D}/roles",
                new { roleCode },
                new JsonSerializerOptions(JsonSerializerDefaults.Web),
                ct);

            if (!res.IsSuccessStatusCode)
            {
                var body = await res.Content.ReadAsStringAsync(ct);
                _logger.LogWarning(
                    "Access role assignment failed for partner {UserId}. Role={RoleCode}. Status={Status}. Body={Body}",
                    userId,
                    roleCode,
                    (int)res.StatusCode,
                    body);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Access role assignment failed for partner {UserId}. Role={RoleCode}", userId, roleCode);
        }
    }

    private async Task TryLinkPartnerToApplicationAsync(Guid userId, Guid applicationId, string? bearer, string? tenantId, string? appId, CancellationToken ct)
    {
        try
        {
            var accessHttp = _configuration["Services:Access:Http"]
                            ?? _configuration["Services__Access__Http"]
                            ?? "http://rplus-kernel-access:5002";

            var client = CreateInternalClient(accessHttp, bearer, tenantId, appId);
            var res = await client.PostAsJsonAsync(
                "/api/access/partner-links",
                new
                {
                    applicationId,
                    userId
                },
                new JsonSerializerOptions(JsonSerializerDefaults.Web),
                ct);

            if (!res.IsSuccessStatusCode)
            {
                var body = await res.Content.ReadAsStringAsync(ct);
                _logger.LogWarning(
                    "Access partner link failed. UserId={UserId} AppId={AppId} Status={Status}. Body={Body}",
                    userId,
                    applicationId,
                    (int)res.StatusCode,
                    body);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Access partner link failed. UserId={UserId} AppId={AppId}", userId, applicationId);
        }
    }

    public sealed record CreatePartnerUserRequest
    {
        public Guid ApplicationId { get; init; }
        public string? RoleCode { get; init; }

        public string Login { get; init; } = string.Empty;
        public string Email { get; init; } = string.Empty;
        public string Phone { get; init; } = string.Empty;
        public string Password { get; init; } = string.Empty;

        public string FirstName { get; init; } = string.Empty;
        public string LastName { get; init; } = string.Empty;
        public string? MiddleName { get; init; }
    }

    public sealed record PartnerScanRequest
    {
        public string QrToken { get; init; } = string.Empty;
    }

    private sealed record CreateUserResponse(Guid UserId);

    public sealed record OtpGenerationRequest
    {
        public Guid UserId { get; init; }
    }
}
