using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RPlus.Organization.Api.Contracts;
using RPlus.Organization.Application.Interfaces;
using RPlus.Organization.Domain;
using RPlus.Organization.Domain.Entities;
using RPlus.SDK.Core.Attributes;
using System.Security.Claims;
using System.Text.Json;

namespace RPlus.Organization.Api.Controllers;

[ApiController]
[Route("api/organization/nodes")]
[Authorize]
public sealed class OrgNodesController : ControllerBase
{
    private readonly IOrganizationDbContext _db;

    public OrgNodesController(IOrganizationDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    [Permission("organization.nodes.read", new[] { "WebAdmin" })]
    public async Task<IActionResult> List([FromQuery] bool includeDeleted, [FromQuery] bool includeHeads, CancellationToken ct)
    {
        if (!TryResolveTenantId(out var tenantId))
            return BadRequest(new { error = "missing_tenant" });

        var query = _db.OrgNodes.AsNoTracking().Where(n => n.TenantId == tenantId);
        if (!includeDeleted)
            query = query.Where(n => !n.IsDeleted && (n.ValidTo == null || n.ValidTo > DateTime.UtcNow));

        Dictionary<Guid, HeadInfo> headMap = new();
        if (includeHeads)
        {
            var now = DateTime.UtcNow;

            var headAssignments = await _db.UserAssignments.AsNoTracking()
                .Where(a =>
                    a.TenantId == tenantId
                    && !a.IsDeleted
                    && a.Role == "HEAD"
                    && a.ValidFrom <= now
                    && (a.ValidTo == null || a.ValidTo > now))
                .Select(a => new
                {
                    a.Id,
                    a.NodeId,
                    a.UserId,
                    a.PositionId,
                    a.ValidFrom
                })
                .ToListAsync(ct);

            if (headAssignments.Count > 0)
            {
                var positionIds = headAssignments.Select(a => a.PositionId).Distinct().ToList();
                var positionTitles = await _db.Positions.AsNoTracking()
                    .Where(p => p.TenantId == tenantId && positionIds.Contains(p.Id))
                    .Select(p => new { p.Id, p.Title })
                    .ToListAsync(ct);

                var positionMap = positionTitles.ToDictionary(p => p.Id, p => p.Title);

                foreach (var group in headAssignments.GroupBy(a => a.NodeId))
                {
                    var head = group.OrderByDescending(a => a.ValidFrom).First();
                    positionMap.TryGetValue(head.PositionId, out var title);
                    headMap[group.Key] = new HeadInfo(head.Id, head.UserId, title);
                }
            }
        }

        var nodeEntities = await query
            .OrderBy(n => n.Path)
            .ToListAsync(ct);

        var nodes = nodeEntities.Select(n => new NodeRow(
            n.Id,
            n.ParentId,
            n.Name,
            n.Type,
            n.Path,
            n.Attributes == null ? null : n.Attributes.RootElement.GetRawText(),
            n.CreatedAt,
            n.UpdatedAt)).ToList();

        var response = nodes.Select(n =>
        {
            headMap.TryGetValue(n.Id, out var head);
            return new
            {
                n.Id,
                n.ParentId,
                n.Name,
                n.Type,
                Path = n.Path,
                AttributesJson = n.AttributesJson,
                n.CreatedAt,
                n.UpdatedAt,
                HeadUserId = includeHeads ? head?.UserId : null,
                HeadAssignmentId = includeHeads ? head?.AssignmentId : null,
                HeadPositionTitle = includeHeads ? head?.PositionTitle : null
            };
        });

        return Ok(response);
    }

    [HttpPost]
    [Permission("organization.nodes.create", new[] { "WebAdmin" })]
    public async Task<IActionResult> Create([FromBody] CreateOrgNodeRequest request, CancellationToken ct)
    {
        if (request is null)
            return BadRequest(new { error = "invalid_request" });

        if (!TryResolveTenantId(out var tenantId))
            return BadRequest(new { error = "missing_tenant" });

        var name = (request.Name ?? string.Empty).Trim();
        var typeRaw = (request.Type ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(name) || name.Length > 256)
            return BadRequest(new { error = "invalid_name" });
        if (string.IsNullOrWhiteSpace(typeRaw) || typeRaw.Length > 64)
            return BadRequest(new { error = "invalid_type" });
        if (!OrgNodeTypeRules.TryParse(typeRaw, out var parsedType))
            return BadRequest(new { error = "invalid_type" });

        OrgNode? parent = null;
        OrgNodeType? parentType = null;
        if (request.ParentId.HasValue)
        {
            parent = await _db.OrgNodes.FirstOrDefaultAsync(
                n => n.TenantId == tenantId && n.Id == request.ParentId.Value && !n.IsDeleted,
                ct);
            if (parent is null)
                return NotFound(new { error = "parent_not_found" });

            if (!OrgNodeTypeRules.TryParse(parent.Type, out var parsedParentType))
                return BadRequest(new { error = "invalid_parent_type" });

            parentType = parsedParentType;
        }

        if (!OrgNodeTypeRules.IsValidParent(parsedType, parentType))
            return BadRequest(new { error = "invalid_parent_type" });

        var id = Guid.NewGuid();
        var node = new OrgNode
        {
            Id = id,
            TenantId = tenantId,
            ParentId = parent?.Id,
            Name = name,
            Type = OrgNodeTypeRules.NormalizeToStorage(parsedType),
            Path = parent == null ? id.ToString("N") : $"{parent.Path}.{id:N}",
            Attributes = request.Attributes,
            ValidFrom = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.OrgNodes.Add(node);
        await _db.SaveChangesAsync(ct);

        return Ok(new { node.Id, node.ParentId, node.Name, node.Type, Path = node.Path });
    }

    [HttpPatch("{id:guid}")]
    [Permission("organization.nodes.update", new[] { "WebAdmin" })]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateOrgNodeRequest request, CancellationToken ct)
    {
        if (request is null)
            return BadRequest(new { error = "invalid_request" });

        if (!TryResolveTenantId(out var tenantId))
            return BadRequest(new { error = "missing_tenant" });

        var node = await _db.OrgNodes.FirstOrDefaultAsync(n => n.TenantId == tenantId && n.Id == id, ct);
        if (node is null)
            return NotFound(new { error = "node_not_found" });

        if (node.IsDeleted)
            return BadRequest(new { error = "node_deleted" });

        if (request.Name is not null)
        {
            var name = request.Name.Trim();
            if (string.IsNullOrWhiteSpace(name) || name.Length > 256)
                return BadRequest(new { error = "invalid_name" });
            node.Name = name;
        }

        if (request.Attributes is not null)
            node.Attributes = request.Attributes;

        node.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return Ok(new { node.Id, node.ParentId, node.Name, node.Type, Path = node.Path });
    }

    [HttpDelete("{id:guid}")]
    [Permission("organization.nodes.delete", new[] { "WebAdmin" })]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        if (!TryResolveTenantId(out var tenantId))
            return BadRequest(new { error = "missing_tenant" });

        var node = await _db.OrgNodes.AsNoTracking()
            .FirstOrDefaultAsync(n => n.TenantId == tenantId && n.Id == id, ct);

        if (node is null)
            return NotFound(new { error = "node_not_found" });

        if (node.IsDeleted)
            return Ok(new { success = true, alreadyDeleted = true });

        var now = DateTime.UtcNow;

        var sql = @"
UPDATE organization.org_nodes
SET ""IsDeleted"" = TRUE,
    ""ValidTo"" = {0},
    ""UpdatedAt"" = {0}
WHERE ""TenantId"" = {1}
  AND (""Path"" = {2} OR ""Path"" LIKE ({2} || '.%'));";

        await (_db as DbContext)!.Database.ExecuteSqlRawAsync(
            sql,
            new object[] { now, tenantId, node.Path },
            ct);

        return Ok(new { success = true });
    }

    [HttpGet("{id:guid}/assignments")]
    [Permission("organization.assignments.read", new[] { "WebAdmin" })]
    public async Task<IActionResult> GetAssignments(
        Guid id,
        [FromQuery] string? role,
        [FromQuery] bool includeDeleted,
        CancellationToken ct)
    {
        if (!TryResolveTenantId(out var tenantId))
            return BadRequest(new { error = "missing_tenant" });

        var nodeExists = await _db.OrgNodes.AsNoTracking()
            .AnyAsync(n => n.TenantId == tenantId && n.Id == id, ct);
        if (!nodeExists)
            return NotFound(new { error = "node_not_found" });

        var query = _db.UserAssignments.AsNoTracking()
            .Where(a => a.TenantId == tenantId && a.NodeId == id);

        var now = DateTime.UtcNow;
        if (!includeDeleted)
        {
            query = query.Where(a =>
                !a.IsDeleted
                && a.ValidFrom <= now
                && (a.ValidTo == null || a.ValidTo > now));
        }

        if (!string.IsNullOrWhiteSpace(role))
        {
            var normalizedRole = role.Trim().ToUpperInvariant();
            query = query.Where(a => a.Role == normalizedRole);
        }

        var assignments = await query
            .Join(
                _db.Positions.AsNoTracking().Where(p => p.TenantId == tenantId),
                a => a.PositionId,
                p => p.Id,
                (a, p) => new
                {
                    a.Id,
                    a.UserId,
                    a.NodeId,
                    a.PositionId,
                    a.Role,
                    a.IsPrimary,
                    a.ValidFrom,
                    a.ValidTo,
                    PositionTitle = p.Title
                })
            .ToListAsync(ct);

        return Ok(assignments);
    }

    [HttpPost("{id:guid}/move")]
    [Permission("organization.nodes.move", new[] { "WebAdmin" })]
    public async Task<IActionResult> Move(Guid id, [FromBody] MoveOrgNodeRequest request, CancellationToken ct)
    {
        if (request is null)
            return BadRequest(new { error = "invalid_request" });

        if (!TryResolveTenantId(out var tenantId))
            return BadRequest(new { error = "missing_tenant" });

        var node = await _db.OrgNodes.FirstOrDefaultAsync(n => n.TenantId == tenantId && n.Id == id, ct);
        if (node is null)
            return NotFound(new { error = "node_not_found" });

        if (node.IsDeleted)
            return BadRequest(new { error = "node_deleted" });

        if (!OrgNodeTypeRules.TryParse(node.Type, out var nodeType))
            return BadRequest(new { error = "invalid_type" });

        OrgNode? newParent = null;
        OrgNodeType? newParentType = null;
        if (request.NewParentId.HasValue)
        {
            newParent = await _db.OrgNodes.AsNoTracking().FirstOrDefaultAsync(
                n => n.TenantId == tenantId && n.Id == request.NewParentId.Value && !n.IsDeleted,
                ct);
            if (newParent is null)
                return NotFound(new { error = "parent_not_found" });

            if (!OrgNodeTypeRules.TryParse(newParent.Type, out var parsedParentType))
                return BadRequest(new { error = "invalid_parent_type" });

            newParentType = parsedParentType;

            var oldPrefix = node.Path;
            var parentPrefix = newParent.Path;
            if (string.Equals(oldPrefix, parentPrefix, StringComparison.OrdinalIgnoreCase)
                || parentPrefix.StartsWith(oldPrefix + ".", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new { error = "cycle_detected" });
            }
        }
        else
        {
            newParentType = null;
        }

        if (!OrgNodeTypeRules.IsValidParent(nodeType, newParentType))
            return BadRequest(new { error = "invalid_parent_type" });

        var oldPath = node.Path;
        var newPath = newParent == null ? id.ToString("N") : $"{newParent.Path}.{id:N}";

        node.ParentId = newParent?.Id;
        node.Path = newPath;
        node.UpdatedAt = DateTime.UtcNow;

        await UpdateSubtreePathsAsync(tenantId, oldPath, newPath, ct);
        await _db.SaveChangesAsync(ct);

        return Ok(new { node.Id, node.ParentId, Path = node.Path });
    }

    private async Task UpdateSubtreePathsAsync(Guid tenantId, string oldPrefix, string newPrefix, CancellationToken ct)
    {
        if (string.Equals(oldPrefix, newPrefix, StringComparison.OrdinalIgnoreCase))
            return;

        // Update descendants in one SQL statement:
        // - node itself (path = oldPrefix) becomes newPrefix
        // - descendants (path LIKE oldPrefix || '.%') keep suffix starting with '.'
        var sql = @"
UPDATE organization.org_nodes
SET ""Path"" = CASE
    WHEN ""Path"" = {0} THEN {1}
    ELSE {1} || substring(""Path"" from char_length({0}) + 1)
END,
    ""UpdatedAt"" = NOW()
WHERE ""TenantId"" = {2}
  AND (""Path"" = {0} OR ""Path"" LIKE ({0} || '.%'));";

        await (_db as DbContext)!.Database.ExecuteSqlRawAsync(
            sql,
            new object[] { oldPrefix, newPrefix, tenantId },
            ct);
    }

    private bool TryResolveTenantId(out Guid tenantId)
    {
        tenantId = Guid.Empty;

        var raw = User.FindFirstValue("tenant_id")
                  ?? User.FindFirstValue("tenantId")
                  ?? Request.Headers["x-tenant-id"].ToString();

        return Guid.TryParse(raw, out tenantId) && tenantId != Guid.Empty;
    }

    private sealed record HeadInfo(Guid AssignmentId, Guid UserId, string? PositionTitle);
    private sealed record NodeRow(
        Guid Id,
        Guid? ParentId,
        string Name,
        string Type,
        string Path,
        string? AttributesJson,
        DateTime CreatedAt,
        DateTime UpdatedAt);
}
