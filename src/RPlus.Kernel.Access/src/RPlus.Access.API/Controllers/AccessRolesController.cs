using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RPlus.Access.Domain.Entities;
using RPlus.Access.Infrastructure.Persistence;
using RPlus.SDK.Access.Authorization;
using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace RPlus.Access.Api.Controllers;

[ApiController]
[Route("api/access/roles")]
[Authorize]
public sealed class AccessRolesController : ControllerBase
{
    private static readonly Regex RoleCodeRegex = new("^[a-zA-Z0-9][a-zA-Z0-9_-]{1,49}$", RegexOptions.Compiled);

    private readonly AccessDbContext _db;

    public AccessRolesController(AccessDbContext db) => _db = db;

    [HttpGet]
    [RequiresPermission("access.roles.read")]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var roles = await _db.Roles
            .AsNoTracking()
            .OrderBy(r => r.Code)
            .Select(r => new { r.Id, r.Code, r.Name })
            .ToListAsync(ct);

        return Ok(roles);
    }

    [HttpGet("{code}")]
    [RequiresPermission("access.roles.read")]
    public async Task<IActionResult> Get(string code, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(code))
            return BadRequest(new { error = "invalid_role_code" });

        var role = await _db.Roles.AsNoTracking().FirstOrDefaultAsync(r => r.Code == code, ct);
        return role == null
            ? NotFound(new { error = "role_not_found" })
            : Ok(new { role.Id, role.Code, role.Name });
    }

    [HttpPost]
    [RequiresPermission("access.roles.create")]
    public async Task<IActionResult> Create([FromBody] CreateRoleRequest request, CancellationToken ct)
    {
        if (request is null)
            return BadRequest(new { error = "invalid_request" });

        var code = (request.Code ?? string.Empty).Trim();
        var name = (request.Name ?? string.Empty).Trim();

        if (!RoleCodeRegex.IsMatch(code))
            return BadRequest(new { error = "invalid_role_code" });
        if (string.IsNullOrWhiteSpace(name) || name.Length > 200)
            return BadRequest(new { error = "invalid_role_name" });

        var exists = await _db.Roles.AnyAsync(r => r.Code == code, ct);
        if (exists)
            return Conflict(new { error = "role_already_exists" });

        var role = Role.Create(code, name);
        _db.Roles.Add(role);
        await _db.SaveChangesAsync(ct);

        return Created($"/api/access/roles/{role.Code}", new { role.Id, role.Code, role.Name });
    }

    [HttpPut("{code}")]
    [RequiresPermission("access.roles.update")]
    public async Task<IActionResult> Update(string code, [FromBody] UpdateRoleRequest request, CancellationToken ct)
    {
        if (request is null)
            return BadRequest(new { error = "invalid_request" });

        if (string.IsNullOrWhiteSpace(code))
            return BadRequest(new { error = "invalid_role_code" });

        var role = await _db.Roles.FirstOrDefaultAsync(r => r.Code == code, ct);
        if (role == null)
            return NotFound(new { error = "role_not_found" });

        var name = (request.Name ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(name) || name.Length > 200)
            return BadRequest(new { error = "invalid_role_name" });

        role.Name = name;
        await _db.SaveChangesAsync(ct);

        return Ok(new { role.Id, role.Code, role.Name });
    }

    [HttpDelete("{code}")]
    [RequiresPermission("access.roles.delete")]
    public async Task<IActionResult> Delete(string code, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(code))
            return BadRequest(new { error = "invalid_role_code" });

        var role = await _db.Roles.FirstOrDefaultAsync(r => r.Code == code, ct);
        if (role == null)
            return NotFound(new { error = "role_not_found" });

        var hasPolicies = await _db.AccessPolicies.AnyAsync(p => p.RoleId == role.Id, ct);
        if (hasPolicies)
            return Conflict(new { error = "role_has_policies" });

        _db.Roles.Remove(role);
        await _db.SaveChangesAsync(ct);
        return Ok(new { success = true });
    }

    public sealed record CreateRoleRequest(string Code, string Name);

    public sealed record UpdateRoleRequest(string Name);
}
