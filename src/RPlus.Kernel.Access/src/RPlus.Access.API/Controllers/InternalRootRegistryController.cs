using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RPlus.Access.Application.Interfaces;
using RPlus.Access.Domain.Entities;
using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RPlus.Access.Api.Controllers;

[ApiController]
[Route("api/internal/root-registry")]
[AllowAnonymous]
public sealed class InternalRootRegistryController : ControllerBase
{
    private readonly IAccessDbContext _db;
    private readonly IConfiguration _configuration;
    private readonly ILogger<InternalRootRegistryController> _logger;

    public InternalRootRegistryController(
        IAccessDbContext db,
        IConfiguration configuration,
        ILogger<InternalRootRegistryController> logger)
    {
        _db = db;
        _configuration = configuration;
        _logger = logger;
    }

    [HttpPost("ensure")]
    public async Task<IActionResult> Ensure([FromBody] EnsureRootRegistryRequest request, CancellationToken ct)
    {
        var expected =
            _configuration["RootAccess:BootstrapSecret"]
            ?? _configuration["RootAccess__BootstrapSecret"]
            ?? _configuration["ROOT_BOOTSTRAP_SECRET"]
            ?? _configuration["RPLUS_INTERNAL_SERVICE_SECRET"]
            ?? string.Empty;

        var provided = Request.Headers["X-Root-Bootstrap-Secret"].ToString();
        if (string.IsNullOrWhiteSpace(expected) || !CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(expected),
                Encoding.UTF8.GetBytes(provided ?? string.Empty)))
        {
            return Unauthorized(new { error = "unauthorized" });
        }

        if (request is null || string.IsNullOrWhiteSpace(request.UserId) || !Guid.TryParse(request.UserId, out var userGuid))
            return BadRequest(new { error = "invalid_request" });

        var rootSecret =
            _configuration["RootAccess:Secret"]
            ?? _configuration["RootAccess__Secret"]
            ?? _configuration["RPLUS_INTERNAL_SERVICE_SECRET"]
            ?? string.Empty;

        if (string.IsNullOrWhiteSpace(rootSecret))
            return StatusCode(500, new { error = "root_secret_not_configured" });

        // Normalize to a stable canonical representation so hashes are consistent across services/tokens.
        var normalizedUserId = userGuid.ToString("D");
        var hashedUserId = ComputeHash(normalizedUserId, rootSecret);

        var entry = await _db.RootRegistry.FirstOrDefaultAsync(x => x.HashedUserId == hashedUserId, ct);
        if (entry is null)
        {
            _db.RootRegistry.Add(new RootRegistryEntry
            {
                HashedUserId = hashedUserId,
                CreatedAt = DateTime.UtcNow,
                Status = "ACTIVE"
            });
        }
        else
        {
            entry.Status = "ACTIVE";
        }

        await _db.SaveChangesAsync(ct);

        // Root changes should apply immediately; invalidate cached rights snapshots (all tenants/contexts).
        var snapshots = await _db.EffectiveSnapshots
            .Where(x => x.UserId == userGuid)
            .ToListAsync(ct);

        if (snapshots.Count > 0)
        {
            _db.EffectiveSnapshots.RemoveRange(snapshots);
            await _db.SaveChangesAsync(ct);
        }

        _logger.LogWarning("Root registry ensured for userId={UserId} (hash={Hash8}...).", request.UserId, hashedUserId[..8]);
        return Ok(new { ensured = true });
    }

    private static string ComputeHash(string input, string key)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(key));
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(input))).ToLowerInvariant();
    }

    public sealed record EnsureRootRegistryRequest(string UserId);
}
