using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RPlus.Organization.Api.Contracts;
using RPlus.Organization.Application.Interfaces;
using RPlus.Organization.Domain.Entities;
using RPlus.SDK.Core.Attributes;
using System.Security.Claims;

namespace RPlus.Organization.Api.Controllers;

[ApiController]
[Route("api/organization/positions")]
[Authorize]
public sealed class PositionsController : ControllerBase
{
    private readonly IOrganizationDbContext _db;

    public PositionsController(IOrganizationDbContext db)
    {
        _db = db;
    }

    [HttpPost]
    [Permission("organization.positions.create", new[] { "WebAdmin" })]
    public async Task<IActionResult> Create([FromBody] CreatePositionRequest request, CancellationToken ct)
    {
        if (request is null)
            return BadRequest(new { error = "invalid_request" });

        if (!TryResolveTenantId(out var tenantId))
            return BadRequest(new { error = "missing_tenant" });

        var title = (request.Title ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(title) || title.Length > 256)
            return BadRequest(new { error = "invalid_title" });

        var node = await _db.OrgNodes.AsNoTracking().FirstOrDefaultAsync(
            n => n.TenantId == tenantId && n.Id == request.NodeId && !n.IsDeleted,
            ct);
        if (node is null)
            return NotFound(new { error = "node_not_found" });

        if (request.ReportsToPositionId.HasValue)
        {
            var managerPos = await _db.Positions.AsNoTracking().FirstOrDefaultAsync(
                p => p.TenantId == tenantId && p.Id == request.ReportsToPositionId.Value && !p.IsDeleted,
                ct);
            if (managerPos is null)
                return NotFound(new { error = "reports_to_not_found" });
        }

        var position = new Position
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            NodeId = request.NodeId,
            Title = title,
            Level = request.Level,
            ReportsToPositionId = request.ReportsToPositionId,
            IsVacant = true,
            Attributes = request.Attributes,
            ValidFrom = DateTime.UtcNow,
            ValidTo = null,
            IsDeleted = false
        };

        _db.Positions.Add(position);
        await _db.SaveChangesAsync(ct);

        return Ok(new { position.Id, position.NodeId, position.Title, position.Level, position.ReportsToPositionId });
    }

    private bool TryResolveTenantId(out Guid tenantId)
    {
        tenantId = Guid.Empty;

        var raw = User.FindFirstValue("tenant_id")
                  ?? User.FindFirstValue("tenantId")
                  ?? Request.Headers["x-tenant-id"].ToString();

        return Guid.TryParse(raw, out tenantId) && tenantId != Guid.Empty;
    }
}

