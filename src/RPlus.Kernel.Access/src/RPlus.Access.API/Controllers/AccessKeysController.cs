using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RPlus.Access.Api.Services;
using RPlus.Access.Domain.Entities;
using RPlus.Access.Infrastructure.Persistence;
using RPlus.SDK.Access.Authorization;

namespace RPlus.Access.Api.Controllers;

[ApiController]
[Route("api/access/keys")]
[Authorize]
public sealed class AccessKeysController : ControllerBase
{
    private readonly AccessDbContext _db;
    private readonly IIntegrationAdminClient _integrationAdmin;

    public AccessKeysController(AccessDbContext db, IIntegrationAdminClient integrationAdmin)
    {
        _db = db;
        _integrationAdmin = integrationAdmin;
    }

    [HttpGet]
    [RequiresPermission("access.keys.read")]
    public async Task<IActionResult> List([FromQuery] string? applicationId, CancellationToken ct)
    {
        var query = _db.IntegrationApiKeys.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(applicationId))
        {
            if (!TryResolveAppId(applicationId, out var appId))
                return BadRequest(new { error = "invalid_application_id" });

            query = query.Where(k => k.ApplicationId == appId);
        }

        var keys = await query
            .OrderByDescending(k => k.CreatedAt)
            .Select(k => new
            {
                k.Id,
                k.ApplicationId,
                k.Name,
                k.Environment,
                k.Status,
                k.CreatedAt,
                k.ExpiresAt,
                k.RevokedAt
            })
            .ToListAsync(ct);

        return Ok(keys);
    }

    [HttpPost]
    [RequiresPermission("access.keys.create")]
    public async Task<IActionResult> Create([FromBody] CreateKeyRequest request, CancellationToken ct)
    {
        if (request is null)
            return BadRequest(new { error = "invalid_request" });

        var name = (request.Name ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(name) || name.Length > 200)
            return BadRequest(new { error = "invalid_key_name" });

        if (!TryResolveAppId(request.ApplicationId, out var appId))
            return BadRequest(new { error = "invalid_application_id" });

        var app = await _db.Apps.AsNoTracking().FirstOrDefaultAsync(a => a.Id == appId, ct);
        if (app is null)
            return NotFound(new { error = "app_not_found" });

        var env = string.IsNullOrWhiteSpace(request.Environment) ? "live" : request.Environment.Trim().ToLowerInvariant();
        if (env is not ("test" or "live"))
            return BadRequest(new { error = "invalid_env" });

        var partnerName = (app.Name ?? app.Code).Trim();
        await _integrationAdmin.EnsurePartnerAsync(app.Id, partnerName, description: $"Access application {app.Code}", isDiscountPartner: false, ct);

        var issued = await _integrationAdmin.CreateApiKeyAsync(app.Id, env, request.ExpiresAt, request.RequireSignature, ct);

        var record = new IntegrationApiKeyRecord
        {
            Id = issued.ApiKeyId,
            ApplicationId = app.Id,
            Name = name,
            Environment = env,
            Status = issued.Status,
            CreatedAt = issued.CreatedAt,
            ExpiresAt = issued.ExpiresAt
        };

        _db.IntegrationApiKeys.Add(record);
        await _db.SaveChangesAsync(ct);

        return Ok(new
        {
            apiKeyId = issued.ApiKeyId,
            applicationId = app.Id,
            secret = issued.FullKey
        });
    }

    private static bool TryResolveAppId(string? raw, out Guid appId)
    {
        appId = Guid.Empty;
        if (string.IsNullOrWhiteSpace(raw))
            return false;

        return Guid.TryParse(raw, out appId);
    }

    public sealed class CreateKeyRequest
    {
        public string Name { get; set; } = string.Empty;

        // GUID of access.applications.id
        public string ApplicationId { get; set; } = string.Empty;

        public string? Environment { get; set; }

        public DateTime? ExpiresAt { get; set; }

        public bool RequireSignature { get; set; }
    }
}

