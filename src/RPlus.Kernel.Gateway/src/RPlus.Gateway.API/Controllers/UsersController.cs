using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using RPlus.Gateway.Api.Auth;
using RPlus.Gateway.Api.Services;
using RPlusGrpc.Access;
using RPlusGrpc.Auth;
using RPlusGrpc.Users;
using RPlusGrpc.Wallet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace RPlus.Gateway.Api.Controllers;

[ApiController]
[Route("api/users")]
public sealed class UsersController : ControllerBase
{
    private const string UserKindStaff = "staff";
    private const string UserKindPartner = "partner";

    private readonly AccessService.AccessServiceClient _access;
    private readonly AuthService.AuthServiceClient _auth;
    private readonly UsersService.UsersServiceClient _users;
    private readonly WalletService.WalletServiceClient _wallet;
    private readonly PermissionGuard _permissionGuard;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<UsersController> _logger;
    private readonly IMemoryCache _cache;
    private readonly AuthCookieOptions _cookies;

    public UsersController(
        AccessService.AccessServiceClient access,
        AuthService.AuthServiceClient auth,
        UsersService.UsersServiceClient users,
        WalletService.WalletServiceClient wallet,
        PermissionGuard permissionGuard,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        IMemoryCache cache,
        IOptions<AuthCookieOptions> cookies,
        ILogger<UsersController> logger)
    {
        _access = access;
        _auth = auth;
        _users = users;
        _wallet = wallet;
        _permissionGuard = permissionGuard;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
        _cache = cache;
        _cookies = cookies.Value;
    }

    [HttpGet]
    [Authorize]
    public async Task<IActionResult> List([FromQuery] int pageNumber, [FromQuery] int pageSize, [FromQuery] string? q, CancellationToken ct)
    {
        var denied = await RequirePermissionAsync("users.read", ct);
        if (denied is not null) return denied;

        var resolvedPage = pageNumber > 0 ? pageNumber : 1;
        var resolvedPageSize = pageSize > 0 ? pageSize : 25;
        if (resolvedPageSize > 200) resolvedPageSize = 200;

        try
        {
            var usersResponse = await _users.ListUsersAsync(
                new ListUsersRequest
                {
                    PageNumber = resolvedPage,
                    PageSize = resolvedPageSize,
                    SearchTerm = q ?? string.Empty
                },
                cancellationToken: ct);

            var ids = usersResponse.Users
                .Select(u => u.Id)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            Dictionary<string, AuthUserInfo> authById = new(StringComparer.OrdinalIgnoreCase);
            try
            {
                if (ids.Length > 0)
                {
                    var authResponse = await _auth.ListAuthUsersAsync(
                        new ListAuthUsersRequest { UserIds = { ids } },
                        cancellationToken: ct);

                    foreach (var user in authResponse.Users)
                    {
                        if (!string.IsNullOrWhiteSpace(user.UserId))
                            authById[user.UserId] = user;
                    }
                }
            }
            catch (Grpc.Core.RpcException ex)
            {
                _logger.LogWarning(ex, "Failed to proxy ListAuthUsers to Auth gRPC for users list.");
            }

            var items = usersResponse.Users.Select(u =>
            {
                authById.TryGetValue(u.Id, out var auth);
                return new
                {
                    userId = u.Id,
                    firstName = u.FirstName ?? string.Empty,
                    lastName = u.LastName ?? string.Empty,
                    middleName = string.IsNullOrWhiteSpace(u.MiddleName) ? null : u.MiddleName,
                    status = u.Status ?? string.Empty,
                    createdAt = u.CreatedAt?.ToDateTime().ToUniversalTime().ToString("O"),
                    updatedAt = u.UpdatedAt?.ToDateTime().ToUniversalTime().ToString("O"),
                    login = auth?.Login ?? string.Empty,
                    email = auth?.Email ?? string.Empty,
                    isBlocked = auth?.IsBlocked ?? false,
                    isActive = auth?.IsActive ?? false
                };
            }).ToArray();

            return Ok(new
            {
                items,
                totalCount = usersResponse.TotalCount,
                pageNumber = resolvedPage,
                pageSize = resolvedPageSize
            });
        }
        catch (Grpc.Core.RpcException ex)
        {
            _logger.LogWarning(ex, "Failed to proxy Users gRPC ListUsers.");
            return StatusCode(503, new { error = "users_unavailable" });
        }
    }

    [HttpGet("me")]
    [AllowAnonymous]
    public async Task<IActionResult> Me([FromQuery] string? tenantId, CancellationToken ct)
    {
        // TenantId is optional in the platform, but when present in the token we should return it to the frontend
        // so it can pass it through to tenant-aware services (Organization, HR, etc).
        var tenantFromClaim = User.FindFirst("tenant_id")?.Value ?? User.FindFirst("tenantId")?.Value;
        var resolvedTenantIdRaw = !string.IsNullOrWhiteSpace(tenantId)
            ? tenantId
            : (!string.IsNullOrWhiteSpace(tenantFromClaim) ? tenantFromClaim : Guid.Empty.ToString());

        if (!Guid.TryParse(resolvedTenantIdRaw, out var resolvedTenantGuid))
            return BadRequest(new { error = "invalid_tenant_id" });

        var resolvedTenantId = resolvedTenantGuid.ToString();

        if (!(User?.Identity?.IsAuthenticated ?? false))
        {
            return Ok(new
            {
                authenticated = false,
                id = string.Empty,
                tenantId = resolvedTenantId,
                permissions = Array.Empty<string>(),
                version = 0,
                preferences = new { advancedMode = false }
            });
        }

        var userId = User.FindFirst("sub")?.Value
            ?? User.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")?.Value;

        if (string.IsNullOrWhiteSpace(userId))
        {
            _logger.LogWarning("Authenticated request missing user id claim (sub/nameidentifier).");
            return Ok(new
            {
                authenticated = false,
                id = string.Empty,
                tenantId = resolvedTenantId,
                permissions = Array.Empty<string>(),
                version = 0,
                preferences = new { advancedMode = false }
            });
        }

        try
        {
            var response = await _access.GetEffectiveRightsAsync(
                new GetEffectiveRightsRequest { UserId = userId, TenantId = resolvedTenantId },
                cancellationToken: ct);

            Dictionary<string, bool>? map = null;
            try
            {
                map = string.IsNullOrWhiteSpace(response.PermissionsJson)
                    ? new Dictionary<string, bool>()
                    : JsonSerializer.Deserialize<Dictionary<string, bool>>(response.PermissionsJson);
            }
            catch (JsonException)
            {
                map = new Dictionary<string, bool>();
            }

            map ??= new Dictionary<string, bool>();
            var permissions = map.Where(kv => kv.Value).Select(kv => kv.Key).OrderBy(x => x, StringComparer.Ordinal).ToArray();

            var bearer = TryGetAccessToken();
            var tenantHeader = Request.Headers.TryGetValue("X-Tenant-Id", out var tenantHeaderValue) ? tenantHeaderValue.ToString() : null;
            var appHeader = Request.Headers.TryGetValue("X-App-Id", out var appHeaderValue) ? appHeaderValue.ToString() : null;
            var preferences = await TryGetUserPreferencesAsync(userId, bearer, tenantHeader, appHeader, ct);
            var advancedMode = preferences?.Preferences?.AdvancedMode ?? false;

            return Ok(new
            {
                authenticated = true,
                id = userId,
                tenantId = resolvedTenantId,
                permissions,
                version = response.Version,
                preferences = new { advancedMode }
            });
        }
        catch (Grpc.Core.RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.Cancelled && ct.IsCancellationRequested)
        {
            // Client aborted the request (navigation/redirect/refresh). Avoid noisy logs.
            return new EmptyResult();
        }
        catch (Grpc.Core.RpcException ex)
        {
            _logger.LogWarning(ex, "Failed to resolve effective rights for user {UserId}", userId);
            return StatusCode(503, new { error = "access_unavailable" });
        }
    }

    [HttpPatch("me")]
    [Authorize]
    public async Task<IActionResult> UpdateMyPreferences([FromBody] UpdateMyPreferencesRequest request, CancellationToken ct)
    {
        if (request is null || request.AdvancedMode is null)
            return BadRequest(new { error = "invalid_request", missing = new[] { "advancedMode" } });

        var userId = User.FindFirst("sub")?.Value
            ?? User.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")?.Value;

        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        var bearer = TryGetAccessToken();
        if (string.IsNullOrWhiteSpace(bearer))
            return Unauthorized();

        var tenantHeader = Request.Headers.TryGetValue("X-Tenant-Id", out var tenantHeaderValue) ? tenantHeaderValue.ToString() : null;
        var appHeader = Request.Headers.TryGetValue("X-App-Id", out var appHeaderValue) ? appHeaderValue.ToString() : null;

        var usersHttp = _configuration["Services:Users:Http"]
                      ?? _configuration["Services__Users__Http"]
                      ?? "http://rplus-kernel-users:5014";

        var client = CreateInternalClient(usersHttp, bearer, tenantHeader, appHeader);
        var payload = JsonContent.Create(
            new { advancedMode = request.AdvancedMode.Value },
            options: new JsonSerializerOptions(JsonSerializerDefaults.Web));

        var message = new HttpRequestMessage(HttpMethod.Patch, "/api/users/preferences/me")
        {
            Content = payload
        };

        var response = await client.SendAsync(message, ct);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            if (!string.IsNullOrWhiteSpace(body))
            {
                try
                {
                    var json = JsonSerializer.Deserialize<object>(body);
                    return StatusCode((int)response.StatusCode, json ?? new { error = "preferences_update_failed" });
                }
                catch
                {
                    return StatusCode((int)response.StatusCode, new { error = "preferences_update_failed", details = body });
                }
            }

            return StatusCode((int)response.StatusCode, new { error = "preferences_update_failed" });
        }

        var updated = await response.Content.ReadFromJsonAsync<UserPreferencesResponse>(cancellationToken: ct);
        var advancedMode = updated?.Preferences?.AdvancedMode ?? request.AdvancedMode.Value;

        if (updated is not null)
        {
            _cache.Set(GetPreferencesCacheKey(userId), updated,
                new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(10) });
        }
        else
        {
            _cache.Remove(GetPreferencesCacheKey(userId));
        }

        return Ok(new
        {
            success = true,
            preferences = new { advancedMode }
        });
    }

    [HttpGet("{userId:guid}/auth")]
    [Authorize]
    public async Task<IActionResult> GetAuthUser(Guid userId, CancellationToken ct)
    {
        var denied = await RequirePermissionAsync("users.read", ct);
        if (denied is not null) return denied;

        try
        {
            var response = await _auth.ListAuthUsersAsync(
                new ListAuthUsersRequest { UserIds = { userId.ToString() } },
                cancellationToken: ct);

            var user = response.Users.FirstOrDefault(x => string.Equals(x.UserId, userId.ToString(), StringComparison.OrdinalIgnoreCase));
            if (user is null)
                return NotFound(new { error = "not_found" });

            return Ok(new
            {
                userId = user.UserId,
                login = user.Login ?? string.Empty,
                email = user.Email ?? string.Empty,
                phoneHash = user.PhoneHash ?? string.Empty,
                isBlocked = user.IsBlocked,
                isActive = user.IsActive
            });
        }
        catch (Grpc.Core.RpcException ex)
        {
            _logger.LogWarning(ex, "Failed to proxy ListAuthUsers to Auth gRPC");
            return StatusCode(503, new { error = "auth_unavailable" });
        }
    }

    [HttpGet("{userId:guid}")]
    [Authorize]
    public async Task<IActionResult> Get(Guid userId, [FromQuery] string? tenantId, CancellationToken ct)
    {
        var denied = await RequirePermissionAsync("users.read", ct);
        if (denied is not null) return denied;

        if (userId == Guid.Empty)
            return BadRequest(new { error = "invalid_user_id" });

        var tenantFromClaim = User.FindFirst("tenant_id")?.Value ?? User.FindFirst("tenantId")?.Value;
        var resolvedTenantIdRaw = !string.IsNullOrWhiteSpace(tenantId)
            ? tenantId
            : (!string.IsNullOrWhiteSpace(tenantFromClaim) ? tenantFromClaim : Guid.Empty.ToString());

        if (!Guid.TryParse(resolvedTenantIdRaw, out var resolvedTenantGuid))
            return BadRequest(new { error = "invalid_tenant_id" });

        var resolvedTenantId = resolvedTenantGuid.ToString("D");

        AuthUserInfo? authUser = null;
        UserProfile? profile = null;
        GetEffectiveRightsResponse? effective = null;

        // Services protected by bearer token (admin). Keep consistency with the UI: the gateway proxies via cookies.
        var bearer = TryGetAccessToken();
        var tenantHeader = Request.Headers.TryGetValue("X-Tenant-Id", out var tenantHeaderValue) ? tenantHeaderValue.ToString() : null;
        var appHeader = Request.Headers.TryGetValue("X-App-Id", out var appHeaderValue) ? appHeaderValue.ToString() : null;

        try
        {
            var usersCall = _users.GetProfileAsync(new GetProfileRequest { UserId = userId.ToString("D") }, cancellationToken: ct);
            var authCall = _auth.ListAuthUsersAsync(new ListAuthUsersRequest { UserIds = { userId.ToString("D") } }, cancellationToken: ct);
            var accessCall = _access.GetEffectiveRightsAsync(
                new GetEffectiveRightsRequest { UserId = userId.ToString("D"), TenantId = resolvedTenantId },
                cancellationToken: ct);

            await Task.WhenAll(usersCall.ResponseAsync, authCall.ResponseAsync, accessCall.ResponseAsync);

            profile = await usersCall.ResponseAsync;
            effective = await accessCall.ResponseAsync;

            var au = (await authCall.ResponseAsync).Users.FirstOrDefault(x => string.Equals(x.UserId, userId.ToString("D"), StringComparison.OrdinalIgnoreCase));
            if (au is not null) authUser = au;
        }
        catch (Grpc.Core.RpcException ex)
        {
            _logger.LogWarning(ex, "User read-model: gRPC calls failed for user {UserId}", userId);
        }

        object rolesModel;
        try
        {
            var accessHttp = _configuration["Services:Access:Http"]
                            ?? _configuration["Services__Access__Http"]
                            ?? "http://rplus-kernel-access:5002";

            var client = CreateInternalClient(accessHttp, bearer, tenantHeader, appHeader);
            var res = await client.GetAsync($"/api/access/users/{userId:D}/roles", ct);
            if (res.IsSuccessStatusCode)
            {
                rolesModel = await res.Content.ReadFromJsonAsync<object>(cancellationToken: ct) ?? new { items = Array.Empty<object>() };
            }
            else if (res.StatusCode is System.Net.HttpStatusCode.Forbidden or System.Net.HttpStatusCode.Unauthorized)
            {
                rolesModel = new { error = "forbidden" };
            }
            else
            {
                rolesModel = new { error = "unavailable", status = (int)res.StatusCode };
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "User read-model: roles fetch failed for user {UserId}", userId);
            rolesModel = new { error = "unavailable" };
        }

        object hrModel;
        try
        {
            var hrBase = _configuration.GetSection("Gateway:Upstreams:hr").GetValue<string>("BaseAddress")
                         ?? _configuration["Gateway:Upstreams:hr:BaseAddress"]
                         ?? "http://rplus-kernel-hr:5015";

            var client = CreateInternalClient(hrBase, bearer, tenantHeader, appHeader);
            var res = await client.GetAsync($"/api/hr/profiles/{userId:D}", ct);
            if (res.IsSuccessStatusCode)
            {
                var payload = await res.Content.ReadFromJsonAsync<object>(cancellationToken: ct);
                hrModel = new { status = "ok", profile = payload };
            }
            else if (res.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                hrModel = new { status = "missing" };
            }
            else if (res.StatusCode is System.Net.HttpStatusCode.Forbidden or System.Net.HttpStatusCode.Unauthorized)
            {
                hrModel = new { status = "forbidden" };
            }
            else
            {
                hrModel = new { status = "error", code = (int)res.StatusCode };
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "User read-model: HR fetch failed for user {UserId}", userId);
            hrModel = new { status = "error" };
        }

        var permissions = Array.Empty<string>();
        var version = 0L;
        try
        {
            version = effective?.Version ?? 0;
            var map = string.IsNullOrWhiteSpace(effective?.PermissionsJson)
                ? new Dictionary<string, bool>()
                : JsonSerializer.Deserialize<Dictionary<string, bool>>(effective!.PermissionsJson) ?? new Dictionary<string, bool>();

            permissions = map.Where(kv => kv.Value).Select(kv => kv.Key).OrderBy(x => x, StringComparer.Ordinal).ToArray();
        }
        catch
        {
            permissions = Array.Empty<string>();
        }

        if (profile is null && authUser is null)
            return NotFound(new { error = "not_found" });

        return Ok(new
        {
            userId = userId.ToString("D"),
            tenantId = resolvedTenantId,
            profile = profile is null ? null : new
            {
                id = profile.Id,
                firstName = profile.FirstName ?? string.Empty,
                lastName = profile.LastName ?? string.Empty,
                middleName = string.IsNullOrWhiteSpace(profile.MiddleName) ? null : profile.MiddleName,
                status = profile.Status ?? string.Empty,
                createdAt = profile.CreatedAt?.ToDateTime().ToUniversalTime().ToString("O"),
                updatedAt = profile.UpdatedAt?.ToDateTime().ToUniversalTime().ToString("O")
            },
            auth = authUser is null ? null : new
            {
                userId = authUser.UserId,
                login = authUser.Login ?? string.Empty,
                email = authUser.Email ?? string.Empty,
                phoneHash = authUser.PhoneHash ?? string.Empty,
                isBlocked = authUser.IsBlocked,
                isActive = authUser.IsActive
            },
            access = new { permissions, version },
            roles = rolesModel,
            hr = hrModel
        });
    }

    [HttpPut("{userId:guid}")]
    [Authorize]
    public async Task<IActionResult> Update(Guid userId, [FromBody] UpdateUserRequest request, [FromQuery] string? tenantId, CancellationToken ct)
    {
        var denied = await RequirePermissionAsync("users.manage", ct);
        if (denied is not null) return denied;

        if (userId == Guid.Empty)
            return BadRequest(new { error = "invalid_user_id" });
        if (request is null)
            return BadRequest(new { error = "invalid_request" });

        var tenantFromClaim = User.FindFirst("tenant_id")?.Value ?? User.FindFirst("tenantId")?.Value;
        var resolvedTenantIdRaw = !string.IsNullOrWhiteSpace(tenantId)
            ? tenantId
            : (!string.IsNullOrWhiteSpace(tenantFromClaim) ? tenantFromClaim : Guid.Empty.ToString());

        if (!Guid.TryParse(resolvedTenantIdRaw, out var resolvedTenantGuid))
            return BadRequest(new { error = "invalid_tenant_id" });

        var bearer = TryGetAccessToken();
        var tenantHeader = Request.Headers.TryGetValue("X-Tenant-Id", out var tenantHeaderValue) ? tenantHeaderValue.ToString() : null;
        var appHeader = Request.Headers.TryGetValue("X-App-Id", out var appHeaderValue) ? appHeaderValue.ToString() : null;

        // Update Users profile (names only for now)
        if (request.FirstName != null || request.LastName != null || request.MiddleName != null)
        {
            try
            {
                var current = await _users.GetProfileAsync(new GetProfileRequest { UserId = userId.ToString("D") }, cancellationToken: ct);
                var firstName = request.FirstName ?? current.FirstName;
                var lastName = request.LastName ?? current.LastName;
                var middleName = request.MiddleName ?? (string.IsNullOrWhiteSpace(current.MiddleName) ? null : current.MiddleName);

                await _users.UpdateProfileAsync(
                    new UpdateProfileRequest
                    {
                        UserId = userId.ToString("D"),
                        FirstName = firstName ?? string.Empty,
                        LastName = lastName ?? string.Empty,
                        MiddleName = middleName
                    },
                    cancellationToken: ct);
            }
            catch (Grpc.Core.RpcException ex)
            {
                _logger.LogWarning(ex, "Failed to proxy Users gRPC UpdateProfile for user {UserId}", userId);
                return StatusCode(503, new { error = "users_unavailable" });
            }
        }

        // Update Auth identity fields via Admin HTTP endpoint (login/email/phone)
        if (request.Login != null || request.Email != null || request.Phone != null)
        {
            try
            {
                var authHttp = _configuration["Services:Auth:Http"]
                              ?? _configuration["Services__Auth__Http"]
                              ?? "http://rplus-kernel-auth:5006";

                var client = CreateInternalClient(authHttp, bearer, tenantHeader, appHeader);
                var res = await client.PutAsJsonAsync(
                    $"/api/v1/auth/admin/users/{userId:D}",
                    new
                    {
                        login = request.Login,
                        email = request.Email,
                        phone = request.Phone
                    },
                    new JsonSerializerOptions(JsonSerializerDefaults.Web),
                    ct);

                if (!res.IsSuccessStatusCode)
                {
                    var body = await res.Content.ReadAsStringAsync(ct);
                    _logger.LogWarning("Auth admin update user failed for {UserId}. Status={Status}. Body={Body}", userId, (int)res.StatusCode, body);

                    if (res.StatusCode == System.Net.HttpStatusCode.NotFound)
                        return NotFound(new { error = "not_found" });

                    if (res.StatusCode == System.Net.HttpStatusCode.BadRequest)
                        return BadRequest(new { error = "invalid_auth_update" });

                    return StatusCode(502, new { error = "auth_update_failed" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Auth admin update user call failed for {UserId}", userId);
                return StatusCode(503, new { error = "auth_unavailable" });
            }
        }

        // Best-effort: if HR profile exists and names changed, keep it in sync (admin-only fields).
        if (request.FirstName != null || request.LastName != null || request.MiddleName != null)
        {
            try
            {
                var hrBase = _configuration.GetSection("Gateway:Upstreams:hr").GetValue<string>("BaseAddress")
                             ?? _configuration["Gateway:Upstreams:hr:BaseAddress"]
                             ?? "http://rplus-kernel-hr:5015";

                var client = CreateInternalClient(hrBase, bearer, tenantHeader, appHeader);
                await client.PutAsJsonAsync(
                    $"/api/hr/profiles/{userId:D}",
                    new
                    {
                        firstName = request.FirstName,
                        lastName = request.LastName,
                        middleName = request.MiddleName
                    },
                    new JsonSerializerOptions(JsonSerializerDefaults.Web),
                    ct);
            }
            catch
            {
                // ignore
            }
        }

        return Ok(new { success = true, userId = userId.ToString("D"), tenantId = resolvedTenantGuid.ToString("D") });
    }

    [HttpGet("{userId:guid}/wallet")]
    [Authorize]
    public async Task<IActionResult> GetWallet(Guid userId, CancellationToken ct)
    {
        var denied = await RequirePermissionAsync("users.read", ct);
        if (denied is not null) return denied;

        try
        {
            var balance = await _wallet.GetBalanceAsync(new GetBalanceRequest { UserId = userId.ToString() }, cancellationToken: ct);
            var history = await _wallet.GetHistoryAsync(new GetHistoryRequest { UserId = userId.ToString(), Limit = 20 }, cancellationToken: ct);

            return Ok(new
            {
                userId = userId.ToString(),
                balance = balance?.Balance ?? 0,
                history = history?.Items?.Select(x => (object)new
                {
                    operationId = x.OperationId,
                    amount = x.Amount,
                    status = x.Status,
                    source = x.Source,
                    createdAt = x.CreatedAt?.ToDateTime().ToUniversalTime().ToString("O"),
                    description = x.Description
                }).ToArray() ?? Array.Empty<object>()
            });
        }
        catch (Grpc.Core.RpcException ex)
        {
            _logger.LogWarning(ex, "Failed to proxy Wallet gRPC");
            return StatusCode(503, new { error = "wallet_unavailable" });
        }
    }

    [HttpGet("{userId:guid}/permissions")]
    [Authorize]
    public async Task<IActionResult> GetPermissions(Guid userId, [FromQuery] string? tenantId, CancellationToken ct)
    {
        var denied = await RequirePermissionAsync("users.read", ct);
        if (denied is not null) return denied;

        var tenantFromClaim = User.FindFirst("tenant_id")?.Value ?? User.FindFirst("tenantId")?.Value;
        var resolvedTenantIdRaw = !string.IsNullOrWhiteSpace(tenantId)
            ? tenantId
            : (!string.IsNullOrWhiteSpace(tenantFromClaim) ? tenantFromClaim : Guid.Empty.ToString());

        if (!Guid.TryParse(resolvedTenantIdRaw, out var resolvedTenantGuid))
            return BadRequest(new { error = "invalid_tenant_id" });

        try
        {
            var response = await _access.GetEffectiveRightsAsync(
                new GetEffectiveRightsRequest { UserId = userId.ToString(), TenantId = resolvedTenantGuid.ToString() },
                cancellationToken: ct);

            Dictionary<string, bool>? map = null;
            try
            {
                map = string.IsNullOrWhiteSpace(response.PermissionsJson)
                    ? new Dictionary<string, bool>()
                    : JsonSerializer.Deserialize<Dictionary<string, bool>>(response.PermissionsJson);
            }
            catch (JsonException)
            {
                map = new Dictionary<string, bool>();
            }

            map ??= new Dictionary<string, bool>();
            var permissions = map.Where(kv => kv.Value).Select(kv => kv.Key).OrderBy(x => x, StringComparer.Ordinal).ToArray();

            return Ok(new
            {
                userId = userId.ToString(),
                tenantId = resolvedTenantGuid.ToString(),
                permissions,
                version = response.Version
            });
        }
        catch (Grpc.Core.RpcException ex)
        {
            _logger.LogWarning(ex, "Failed to resolve effective rights for user {UserId}", userId);
            return StatusCode(503, new { error = "access_unavailable" });
        }
    }

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> Create([FromBody] CreateUserRequest request, CancellationToken ct)
    {
        var denied = await RequirePermissionAsync("users.manage", ct);
        if (denied is not null) return denied;

        if (request is null)
            return BadRequest(new { error = "invalid_request" });

        var kind = (request.Kind ?? UserKindStaff).Trim().ToLowerInvariant();
        if (kind is not UserKindStaff and not UserKindPartner)
            return BadRequest(new { error = "invalid_user_kind" });

        if (kind == UserKindPartner)
            return BadRequest(new { error = "use_partners_endpoint", hint = "Use POST /api/partners for partner accounts." });

        var missing = new List<string>(8);
        if (string.IsNullOrWhiteSpace(request.Login)) missing.Add("login");
        if (string.IsNullOrWhiteSpace(request.Email)) missing.Add("email");
        if (string.IsNullOrWhiteSpace(request.Phone)) missing.Add("phone");
        if (string.IsNullOrWhiteSpace(request.Password)) missing.Add("password");
        if (string.IsNullOrWhiteSpace(request.FirstName)) missing.Add("firstName");
        if (string.IsNullOrWhiteSpace(request.LastName)) missing.Add("lastName");
        // HR profile is mandatory for staff users.
        if (string.IsNullOrWhiteSpace(request.Iin)) missing.Add("iin");
        if (string.IsNullOrWhiteSpace(request.BirthDate)) missing.Add("birthDate");
        if (string.IsNullOrWhiteSpace(request.HireDate)) missing.Add("hireDate");
        if (missing.Count > 0)
            return BadRequest(new { error = "invalid_request", missing });

        var tenantFromClaim = User.FindFirst("tenant_id")?.Value ?? User.FindFirst("tenantId")?.Value;
        Guid tenantGuid = Guid.Empty;
        if (!string.IsNullOrWhiteSpace(tenantFromClaim))
            Guid.TryParse(tenantFromClaim, out tenantGuid);

        var authHttp = _configuration["Services:Auth:Http"]
                      ?? _configuration["Services__Auth__Http"]
                      ?? "http://rplus-kernel-auth:5006";

        var createdUserId = await CreateAuthUserAsync(authHttp, request, tenantGuid, UserKindStaff, ct);
        if (createdUserId == Guid.Empty)
            return StatusCode(502, new { error = "auth_create_failed" });

        // Best-effort enrichment: HR profile and Organization assignment.
        var bearer = TryGetAccessToken();
        var tenantHeader = Request.Headers.TryGetValue("X-Tenant-Id", out var tenantHeaderValue) ? tenantHeaderValue.ToString() : null;
        var appHeader = Request.Headers.TryGetValue("X-App-Id", out var appHeaderValue) ? appHeaderValue.ToString() : null;

        await TryUpsertHrProfileAsync(createdUserId, request, bearer, tenantHeader, appHeader, ct);
        await TryAssignOrganizationAsync(createdUserId, request, bearer, tenantHeader, appHeader, ct);

        var roleCode = (request.RoleCode ?? string.Empty).Trim();
        if (!string.IsNullOrWhiteSpace(roleCode))
            await TryAssignAccessRoleAsync(createdUserId, roleCode, bearer, tenantHeader, appHeader, ct);

        return Ok(new { userId = createdUserId.ToString() });
    }

    /// <summary>
    /// Terminate/dismiss an employee. Blocks account, revokes all sessions, publishes UserTerminated event.
    /// </summary>
    [HttpPost("{id}/terminate")]
    [Authorize]
    public async Task<IActionResult> Terminate(string id, [FromBody] TerminateRequest? request, CancellationToken ct)
    {
        // Permission check
        var denied = await RequirePermissionAsync("users.delete", ct);
        if (denied != null) return denied;

        if (!Guid.TryParse(id, out var userId))
        {
            return BadRequest(new { error = "invalid_user_id" });
        }

        try
        {
            var grpcRequest = new RPlusGrpc.Auth.TerminateUserRequest
            {
                UserId = userId.ToString(),
                Reason = request?.Reason ?? "Уволен"
            };

            var result = await _auth.TerminateUserAsync(grpcRequest, cancellationToken: ct);

            if (!result.Success)
            {
                return result.ErrorCode switch
                {
                    "user_not_found" => NotFound(new { error = "user_not_found" }),
                    "cannot_terminate_system_user" => BadRequest(new { error = "cannot_terminate_system_user" }),
                    "already_terminated" => BadRequest(new { error = "already_terminated" }),
                    _ => BadRequest(new { error = result.ErrorCode })
                };
            }

            _logger.LogInformation("User {UserId} terminated successfully. Reason: {Reason}", userId, request?.Reason ?? "Уволен");
            return Ok(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to terminate user {UserId}", userId);
            return StatusCode(500, new { error = "termination_failed" });
        }
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

    private async Task<UserPreferencesResponse?> TryGetUserPreferencesAsync(
        string userId,
        string? bearer,
        string? tenantId,
        string? appId,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(bearer))
            return null;

        if (_cache.TryGetValue(GetPreferencesCacheKey(userId), out UserPreferencesResponse cached))
            return cached;

        var usersHttp = _configuration["Services:Users:Http"]
                      ?? _configuration["Services__Users__Http"]
                      ?? "http://rplus-kernel-users:5014";

        try
        {
            var client = CreateInternalClient(usersHttp, bearer, tenantId, appId);
            var res = await client.GetAsync("/api/users/preferences/me", ct);
            if (!res.IsSuccessStatusCode)
                return null;

            var preferences = await res.Content.ReadFromJsonAsync<UserPreferencesResponse>(cancellationToken: ct);
            if (preferences is not null)
            {
                _cache.Set(GetPreferencesCacheKey(userId), preferences,
                    new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(10) });
            }

            return preferences;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch user preferences.");
            return null;
        }
    }

    private static string GetPreferencesCacheKey(string userId) => $"users:preferences:{userId}";

    private async Task<Guid> CreateAuthUserAsync(string authHttpBase, CreateUserRequest request, Guid tenantId, string kind, CancellationToken ct)
    {
        try
        {
            var userType = kind == UserKindPartner ? 1 : 2; // Platform : Staff
            var grpcRequest = new RPlusGrpc.Auth.CreateUserRequest
            {
                Login = request.Login.Trim(),
                Email = request.Email.Trim(),
                Phone = request.Phone.Trim(),
                Password = request.Password,
                FirstName = request.FirstName.Trim(),
                LastName = request.LastName.Trim(),
                MiddleName = string.IsNullOrWhiteSpace(request.MiddleName) ? "" : request.MiddleName.Trim(),
                UserType = userType,
                TenantId = tenantId == Guid.Empty ? "" : tenantId.ToString()
            };

            var response = await _auth.CreateUserAsync(grpcRequest, cancellationToken: ct);

            if (!response.Success)
            {
                _logger.LogWarning("Auth gRPC CreateUser failed. ErrorCode={ErrorCode}", response.ErrorCode);
                return Guid.Empty;
            }

            return Guid.TryParse(response.UserId, out var userId) ? userId : Guid.Empty;
        }
        catch (Grpc.Core.RpcException ex)
        {
            _logger.LogWarning(ex, "Auth gRPC CreateUser call failed.");
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
                    "Access role assignment failed for user {UserId}. Role={RoleCode}. Status={Status}. Body={Body}",
                    userId,
                    roleCode,
                    (int)res.StatusCode,
                    body);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Access role assignment failed for user {UserId}. Role={RoleCode}", userId, roleCode);
        }
    }

    private async Task TryUpsertHrProfileAsync(Guid userId, CreateUserRequest request, string? bearer, string? tenantId, string? appId, CancellationToken ct)
    {
        try
        {
            var hrBase = _configuration.GetSection("Gateway:Upstreams:hr").GetValue<string>("BaseAddress")
                         ?? _configuration["Gateway:Upstreams:hr:BaseAddress"]
                         ?? "http://rplus-kernel-hr:5015";

            var client = CreateInternalClient(hrBase, bearer, tenantId, appId);
            var birthDate = TryParseDateOnly(request.BirthDate);
            var hireDate = TryParseDateOnly(request.HireDate);
            var res = await client.PutAsJsonAsync(
                $"/api/hr/profiles/{userId:D}",
                new
                {
                    iin = string.IsNullOrWhiteSpace(request.Iin) ? null : request.Iin.Trim(),
                    firstName = request.FirstName.Trim(),
                    lastName = request.LastName.Trim(),
                    middleName = string.IsNullOrWhiteSpace(request.MiddleName) ? null : request.MiddleName.Trim(),
                    birthDate,
                    hireDate,
                    status = "Active"
                },
                new JsonSerializerOptions(JsonSerializerDefaults.Web),
                ct);

            if (!res.IsSuccessStatusCode)
            {
                var body = await res.Content.ReadAsStringAsync(ct);
                _logger.LogWarning("HR upsert failed for user {UserId}. Status={Status}. Body={Body}", userId, (int)res.StatusCode, body);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "HR upsert failed for user {UserId}", userId);
        }
    }

    private static DateOnly? TryParseDateOnly(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var s = raw.Trim();
        return DateOnly.TryParse(s, out var value) ? value : null;
    }

    private async Task TryAssignOrganizationAsync(Guid userId, CreateUserRequest request, string? bearer, string? tenantId, string? appId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.OrganizationNodeId) || string.IsNullOrWhiteSpace(request.PositionTitle))
            return;

        if (!Guid.TryParse(request.OrganizationNodeId, out var nodeId) || nodeId == Guid.Empty)
            return;

        if (string.IsNullOrWhiteSpace(tenantId) || !Guid.TryParse(tenantId, out var tenantGuid) || tenantGuid == Guid.Empty)
            return;

        try
        {
            var orgBase = _configuration.GetSection("Gateway:Upstreams:organization").GetValue<string>("BaseAddress")
                          ?? _configuration["Gateway:Upstreams:organization:BaseAddress"]
                          ?? "http://rplus-kernel-organization:5009";

            var client = CreateInternalClient(orgBase, bearer, tenantId, appId);

            // Create a position (v2 model). Position listing is not exposed yet, so we create on-demand.
            var positionRes = await client.PostAsJsonAsync(
                "/api/organization/positions",
                new
                {
                    nodeId,
                    title = request.PositionTitle.Trim(),
                    level = 0
                },
                new JsonSerializerOptions(JsonSerializerDefaults.Web),
                ct);

            if (!positionRes.IsSuccessStatusCode)
            {
                var body = await positionRes.Content.ReadAsStringAsync(ct);
                _logger.LogWarning("Organization position create failed for user {UserId}. Status={Status}. Body={Body}", userId, (int)positionRes.StatusCode, body);
                return;
            }

            var positionPayload = await positionRes.Content.ReadFromJsonAsync<CreatePositionResponse>(cancellationToken: ct);
            if (positionPayload is null || positionPayload.Id == Guid.Empty)
                return;

            var role = string.IsNullOrWhiteSpace(request.OrganizationRole) ? "EMPLOYEE" : request.OrganizationRole.Trim().ToUpperInvariant();
            var assignRes = await client.PostAsJsonAsync(
                "/api/organization/assignments",
                new
                {
                    userId,
                    positionId = positionPayload.Id,
                    role,
                    isPrimary = true,
                    ftePercent = 100
                },
                new JsonSerializerOptions(JsonSerializerDefaults.Web),
                ct);

            if (!assignRes.IsSuccessStatusCode)
            {
                var body = await assignRes.Content.ReadAsStringAsync(ct);
                _logger.LogWarning("Organization assignment create failed for user {UserId}. Status={Status}. Body={Body}", userId, (int)assignRes.StatusCode, body);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Organization assignment failed for user {UserId}", userId);
        }
    }

    public sealed record CreateUserRequest
    {
        // "staff" (default) provisions staff projections (Users->Wallet/Loyalty, HR/Org via best-effort in Gateway).
        // "partner" creates an Auth user only and skips staff-only provisioning.
        public string? Kind { get; init; }
        // Optional: assigns an Access role (by code) after creation (e.g. "partner").
        public string? RoleCode { get; init; }

        public string Login { get; init; } = string.Empty;
        public string Email { get; init; } = string.Empty;
        public string Phone { get; init; } = string.Empty;
        public string Password { get; init; } = string.Empty;

        public string FirstName { get; init; } = string.Empty;
        public string LastName { get; init; } = string.Empty;
        public string? MiddleName { get; init; }

        public string Iin { get; init; } = string.Empty;
        public string BirthDate { get; init; } = string.Empty;
        public string HireDate { get; init; } = string.Empty;

        public string? OrganizationNodeId { get; init; }
        public string? PositionTitle { get; init; }
        public string? OrganizationRole { get; init; }
    }

    public sealed record UpdateUserRequest
    {
        public string? Login { get; init; }
        public string? Email { get; init; }
        public string? Phone { get; init; }

        public string? FirstName { get; init; }
        public string? LastName { get; init; }
        public string? MiddleName { get; init; }
    }

    private sealed record CreateUserResponse(Guid UserId);
    private sealed record CreatePositionResponse(Guid Id);
    public sealed record UpdateMyPreferencesRequest(bool? AdvancedMode);
    private sealed record UserPreferencesResponse(string? UserId, UserPreferences? Preferences);
    private sealed record UserPreferences(bool AdvancedMode);
    public sealed record TerminateRequest(string? Reason);
}
