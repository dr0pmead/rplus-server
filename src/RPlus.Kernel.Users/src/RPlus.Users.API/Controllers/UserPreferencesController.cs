using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RPlus.Users.Infrastructure.Persistence;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace RPlus.Users.Api.Controllers;

[ApiController]
[Route("api/users/preferences")]
public sealed class UserPreferencesController : ControllerBase
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly UsersDbContext _db;
    private readonly ILogger<UserPreferencesController> _logger;

    public UserPreferencesController(UsersDbContext db, ILogger<UserPreferencesController> logger)
    {
        _db = db;
        _logger = logger;
    }

    [HttpGet("me")]
    public async Task<IActionResult> GetMyPreferences(CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var user = await _db.Users.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == userId, ct);

        if (user is null)
            return NotFound(new { error = "user_not_found" });

        var advancedMode = ExtractAdvancedMode(user.PreferencesJson);
        return Ok(new
        {
            userId = userId.ToString("D"),
            preferences = new
            {
                advancedMode
            }
        });
    }

    [HttpPatch("me")]
    public async Task<IActionResult> UpdateMyPreferences([FromBody] UpdatePreferencesRequest request, CancellationToken ct)
    {
        if (request is null || request.AdvancedMode is null)
            return BadRequest(new { error = "invalid_request", missing = new[] { "advancedMode" } });

        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var user = await _db.Users.FirstOrDefaultAsync(x => x.Id == userId, ct);
        if (user is null)
            return NotFound(new { error = "user_not_found" });

        var preferences = ParsePreferences(user.PreferencesJson);
        preferences["advancedMode"] = request.AdvancedMode.Value;
        user.PreferencesJson = JsonSerializer.Serialize(preferences, JsonOptions);
        user.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        return Ok(new
        {
            success = true,
            userId = userId.ToString("D"),
            preferences = new
            {
                advancedMode = request.AdvancedMode.Value
            }
        });
    }

    private bool TryGetUserId(out Guid userId)
    {
        userId = Guid.Empty;
        var userIdRaw = User.FindFirst("sub")?.Value
                        ?? User.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")?.Value;

        return Guid.TryParse(userIdRaw, out userId);
    }

    private static bool ExtractAdvancedMode(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return false;

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("advancedMode", out var value))
                return false;

            return value.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.String => bool.TryParse(value.GetString(), out var parsed) && parsed,
                _ => false
            };
        }
        catch (Exception)
        {
            return false;
        }
    }

    private Dictionary<string, object?> ParsePreferences(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        try
        {
            var parsed = JsonSerializer.Deserialize<Dictionary<string, object?>>(json, JsonOptions);
            return parsed ?? new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse user preferences JSON. Resetting to empty.");
            return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        }
    }

    public sealed record UpdatePreferencesRequest(bool? AdvancedMode);
}
