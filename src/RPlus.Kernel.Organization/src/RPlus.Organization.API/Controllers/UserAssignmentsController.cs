using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RPlus.Organization.Api.Contracts;
using RPlus.Organization.Application.Interfaces;
using RPlus.Organization.Domain.Entities;
using RPlus.SDK.Core.Attributes;
using RPlus.SDK.Eventing.Abstractions;
using RPlus.SDK.Organization.Events;
using System.Security.Claims;
using System.Text.Json;

namespace RPlus.Organization.Api.Controllers;

[ApiController]
[Route("api/organization/assignments")]
[Authorize]
public sealed class UserAssignmentsController : ControllerBase
{
    private static readonly HashSet<string> AllowedRoles = new(StringComparer.OrdinalIgnoreCase)
    {
        "HEAD",
        "DEPUTY",
        "EMPLOYEE"
    };

    private readonly IOrganizationDbContext _db;
    private readonly IEventPublisher _eventPublisher;

    public UserAssignmentsController(IOrganizationDbContext db, IEventPublisher eventPublisher)
    {
        _db = db;
        _eventPublisher = eventPublisher;
    }

    [HttpPost]
    [Permission("organization.assignments.create", new[] { "WebAdmin" })]
    public async Task<IActionResult> Create([FromBody] CreateUserAssignmentRequest request, CancellationToken ct)
    {
        if (request is null)
            return BadRequest(new { error = "invalid_request" });

        if (!TryResolveTenantId(out var tenantId))
            return BadRequest(new { error = "missing_tenant" });

        var role = NormalizeRole(request.Role);
        if (!AllowedRoles.Contains(role))
            return BadRequest(new { error = "invalid_role" });

        if (request.UserId == Guid.Empty)
            return BadRequest(new { error = "invalid_user_id" });
        var hasPositionId = request.PositionId.HasValue && request.PositionId.Value != Guid.Empty;
        var hasNodeId = request.NodeId.HasValue && request.NodeId.Value != Guid.Empty;
        if (!hasPositionId && !hasNodeId)
            return BadRequest(new { error = "missing_position_or_node" });

        Position? position = null;
        OrgNode? node = null;
        if (hasPositionId)
        {
            position = await _db.Positions.AsNoTracking().FirstOrDefaultAsync(
                p => p.TenantId == tenantId && p.Id == request.PositionId!.Value && !p.IsDeleted,
                ct);
            if (position is null)
                return NotFound(new { error = "position_not_found" });

            node = await _db.OrgNodes.AsNoTracking().FirstOrDefaultAsync(
                n => n.TenantId == tenantId && n.Id == position.NodeId && !n.IsDeleted,
                ct);
            if (node is null)
                return NotFound(new { error = "node_not_found" });
        }
        else if (hasNodeId)
        {
            node = await _db.OrgNodes.AsNoTracking().FirstOrDefaultAsync(
                n => n.TenantId == tenantId && n.Id == request.NodeId!.Value && !n.IsDeleted,
                ct);
            if (node is null)
                return NotFound(new { error = "node_not_found" });
        }

        var now = DateTime.UtcNow;
        if (role == "HEAD")
        {
            var existingHead = await _db.UserAssignments.AsNoTracking()
                .Where(a =>
                    a.TenantId == tenantId
                    && a.NodeId == node!.Id
                    && !a.IsDeleted
                    && a.Role == "HEAD"
                    && a.ValidFrom <= now
                    && (a.ValidTo == null || a.ValidTo > now))
                .OrderByDescending(a => a.ValidFrom)
                .FirstOrDefaultAsync(ct);

            if (existingHead is not null)
            {
                if (existingHead.UserId == request.UserId)
                {
                    return Ok(new { id = existingHead.Id, assignmentId = existingHead.Id, alreadyAssigned = true });
                }

                return Conflict(new
                {
                    error = "head_already_assigned",
                    assignmentId = existingHead.Id,
                    userId = existingHead.UserId
                });
            }
        }

        if (position is null)
        {
            position = await GetOrCreateSystemPositionAsync(
                tenantId,
                node!,
                role,
                request.PositionTitle,
                ct);
        }

        var assignment = new UserAssignment
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            UserId = request.UserId,
            PositionId = position.Id,
            NodeId = position.NodeId,
            Role = role,
            Type = "REGULAR",
            IsPrimary = request.IsPrimary,
            FtePercent = request.FtePercent <= 0 ? 100m : request.FtePercent,
            ValidFrom = request.ValidFrom ?? DateTime.UtcNow,
            ValidTo = request.ValidTo,
            IsDeleted = false
        };

        _db.UserAssignments.Add(assignment);
        await _db.SaveChangesAsync(ct);

        await PublishAssignmentChangedAsync(assignment, ct);

        return Ok(new { assignment.Id, assignmentId = assignment.Id });
    }

    [HttpPut("{id:guid}")]
    [Permission("organization.assignments.update", new[] { "WebAdmin" })]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateUserAssignmentRequest request, CancellationToken ct)
    {
        if (request is null)
            return BadRequest(new { error = "invalid_request" });

        if (!TryResolveTenantId(out var tenantId))
            return BadRequest(new { error = "missing_tenant" });

        var assignment = await _db.UserAssignments.FirstOrDefaultAsync(a => a.TenantId == tenantId && a.Id == id, ct);
        if (assignment is null)
            return NotFound(new { error = "assignment_not_found" });

        if (request.Role != null)
        {
            var role = NormalizeRole(request.Role);
            if (!AllowedRoles.Contains(role))
                return BadRequest(new { error = "invalid_role" });
            if (role == "HEAD")
            {
                var now = DateTime.UtcNow;
                var existingHead = await _db.UserAssignments.AsNoTracking()
                    .Where(a =>
                        a.TenantId == tenantId
                        && a.NodeId == assignment.NodeId
                        && a.Id != assignment.Id
                        && !a.IsDeleted
                        && a.Role == "HEAD"
                        && a.ValidFrom <= now
                        && (a.ValidTo == null || a.ValidTo > now))
                    .OrderByDescending(a => a.ValidFrom)
                    .FirstOrDefaultAsync(ct);

                if (existingHead is not null)
                {
                    return Conflict(new
                    {
                        error = "head_already_assigned",
                        assignmentId = existingHead.Id,
                        userId = existingHead.UserId
                    });
                }
            }
            assignment.Role = role;
        }

        if (request.ValidFrom.HasValue)
            assignment.ValidFrom = request.ValidFrom.Value;
        if (request.ValidTo.HasValue || request.ValidTo == null)
            assignment.ValidTo = request.ValidTo;

        if (request.IsPrimary.HasValue)
            assignment.IsPrimary = request.IsPrimary.Value;
        if (request.FtePercent.HasValue)
            assignment.FtePercent = request.FtePercent.Value;
        if (request.IsDeleted.HasValue)
            assignment.IsDeleted = request.IsDeleted.Value;

        await _db.SaveChangesAsync(ct);
        await PublishAssignmentChangedAsync(assignment, ct);

        return Ok(new { success = true });
    }

    private async Task PublishAssignmentChangedAsync(UserAssignment assignment, CancellationToken ct)
    {
        var payload = new OrganizationAssignmentChangedPayload(
            assignment.TenantId,
            assignment.UserId,
            assignment.PositionId,
            assignment.NodeId,
            assignment.Role,
            assignment.IsDeleted,
            assignment.ValidFrom,
            assignment.ValidTo,
            DateTime.UtcNow);

        await _eventPublisher.PublishAsync(
            payload,
            OrganizationEventTopics.AssignmentChanged,
            assignment.UserId.ToString(),
            ct);
    }

    private static string NormalizeRole(string? raw) =>
        (raw ?? string.Empty).Trim().ToUpperInvariant();

    private bool TryResolveTenantId(out Guid tenantId)
    {
        tenantId = Guid.Empty;

        var raw = User.FindFirstValue("tenant_id")
                  ?? User.FindFirstValue("tenantId")
                  ?? Request.Headers["x-tenant-id"].ToString();

        return Guid.TryParse(raw, out tenantId) && tenantId != Guid.Empty;
    }

    private async Task<Position> GetOrCreateSystemPositionAsync(
        Guid tenantId,
        OrgNode node,
        string role,
        string? positionTitle,
        CancellationToken ct)
    {
        var positions = await _db.Positions
            .Where(p => p.TenantId == tenantId && p.NodeId == node.Id && !p.IsDeleted)
            .ToListAsync(ct);

        var existing = positions.FirstOrDefault(p => IsSystemPosition(p, role));
        if (existing is not null)
        {
            return existing;
        }

        var title = string.IsNullOrWhiteSpace(positionTitle)
            ? BuildDefaultTitle(node.Name, role)
            : positionTitle.Trim();

        var position = new Position
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            NodeId = node.Id,
            Title = title,
            Level = 0,
            ReportsToPositionId = null,
            IsVacant = false,
            Attributes = BuildSystemAttributes(role),
            ValidFrom = DateTime.UtcNow,
            ValidTo = null,
            IsDeleted = false
        };

        _db.Positions.Add(position);
        return position;
    }

    private static string BuildDefaultTitle(string nodeName, string role)
    {
        if (role == "HEAD")
        {
            return $"Head of {nodeName}".Trim();
        }

        if (role == "DEPUTY")
        {
            return $"Deputy of {nodeName}".Trim();
        }

        return $"Employee of {nodeName}".Trim();
    }

    private static JsonDocument BuildSystemAttributes(string role)
    {
        var payload = role == "HEAD"
            ? "{\"systemHead\":true,\"systemRole\":\"HEAD\"}"
            : $"{{\"systemRole\":\"{role}\"}}";

        return JsonDocument.Parse(payload);
    }

    private static bool IsSystemPosition(Position position, string role)
    {
        if (position.Attributes is not null)
        {
            var root = position.Attributes.RootElement;
            if (root.TryGetProperty("systemRole", out var systemRole)
                && string.Equals(systemRole.GetString(), role, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (role == "HEAD"
                && root.TryGetProperty("systemHead", out var systemHead)
                && systemHead.ValueKind == JsonValueKind.True)
            {
                return true;
            }
        }

        if (role == "HEAD" && position.Title.StartsWith("Head of", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }
}
