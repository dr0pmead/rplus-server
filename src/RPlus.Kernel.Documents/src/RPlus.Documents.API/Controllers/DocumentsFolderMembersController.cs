using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RPlus.Documents.Application.Interfaces;
using RPlus.Documents.Domain.Entities;
using RPlus.SDK.Access.Authorization;
using System.Security.Claims;

namespace RPlus.Documents.Api.Controllers;

[ApiController]
[Route("api/documents/folders/{folderId:guid}/members")]
public sealed class DocumentsFolderMembersController(IDocumentsDbContext dbContext) : ControllerBase
{
    [HttpGet]
    [RequiresAnyPermission("documents.folders.members.read", "documents.hr.folders.members.read", "documents.org.folders.members.read", "documents.department.folders.members.read")]
    public async Task<IActionResult> List(Guid folderId, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
            return Unauthorized(new { error = "unauthorized" });

        var isAllowed = await CanManageMembers(folderId, userId.Value, ct);
        if (!isAllowed)
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "forbidden" });

        var members = await dbContext.DocumentFolderMembers.AsNoTracking()
            .Where(m => m.FolderId == folderId)
            .ToListAsync(ct);

        return Ok(members.Select(m => new DocumentFolderMemberDto(m)));
    }

    [HttpPost]
    [RequiresAnyPermission("documents.folders.members.manage", "documents.hr.folders.members.manage", "documents.org.folders.members.manage", "documents.department.folders.members.manage")]
    public async Task<IActionResult> Add(Guid folderId, [FromBody] UpsertFolderMemberRequest request, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
            return Unauthorized(new { error = "unauthorized" });

        var isAllowed = await CanManageMembers(folderId, userId.Value, ct);
        if (!isAllowed)
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "forbidden" });

        var existing = await dbContext.DocumentFolderMembers
            .FirstOrDefaultAsync(m => m.FolderId == folderId && m.UserId == request.UserId, ct);

        if (existing != null)
            return Conflict(new { error = "member_exists" });

        var member = new DocumentFolderMember
        {
            Id = Guid.NewGuid(),
            FolderId = folderId,
            UserId = request.UserId,
            IsOwner = false,
            CanView = request.CanView,
            CanDownload = request.CanDownload,
            CanUpload = request.CanUpload,
            CanEdit = request.CanEdit,
            CanDeleteFiles = request.CanDeleteFiles,
            CanDeleteFolder = request.CanDeleteFolder,
            CanShareLinks = request.CanShareLinks,
            CanManageMembers = request.CanManageMembers,
            CreatedAt = DateTime.UtcNow
        };

        dbContext.DocumentFolderMembers.Add(member);
        await dbContext.SaveChangesAsync(ct);

        return Ok(new DocumentFolderMemberDto(member));
    }

    [HttpPut("{userId:guid}")]
    [RequiresAnyPermission("documents.folders.members.manage", "documents.hr.folders.members.manage", "documents.org.folders.members.manage", "documents.department.folders.members.manage")]
    public async Task<IActionResult> Update(Guid folderId, Guid userId, [FromBody] UpsertFolderMemberRequest request, CancellationToken ct)
    {
        var actorId = GetCurrentUserId();
        if (actorId == null)
            return Unauthorized(new { error = "unauthorized" });

        var isAllowed = await CanManageMembers(folderId, actorId.Value, ct);
        if (!isAllowed)
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "forbidden" });

        var existing = await dbContext.DocumentFolderMembers
            .FirstOrDefaultAsync(m => m.FolderId == folderId && m.UserId == userId, ct);

        if (existing == null)
            return NotFound(new { error = "member_not_found" });

        if (existing.IsOwner)
            return BadRequest(new { error = "owner_permissions_locked" });

        existing.CanView = request.CanView;
        existing.CanDownload = request.CanDownload;
        existing.CanUpload = request.CanUpload;
        existing.CanEdit = request.CanEdit;
        existing.CanDeleteFiles = request.CanDeleteFiles;
        existing.CanDeleteFolder = request.CanDeleteFolder;
        existing.CanShareLinks = request.CanShareLinks;
        existing.CanManageMembers = request.CanManageMembers;

        await dbContext.SaveChangesAsync(ct);
        return Ok(new DocumentFolderMemberDto(existing));
    }

    [HttpDelete("{userId:guid}")]
    [RequiresAnyPermission("documents.folders.members.manage", "documents.hr.folders.members.manage", "documents.org.folders.members.manage", "documents.department.folders.members.manage")]
    public async Task<IActionResult> Remove(Guid folderId, Guid userId, CancellationToken ct)
    {
        var actorId = GetCurrentUserId();
        if (actorId == null)
            return Unauthorized(new { error = "unauthorized" });

        var isAllowed = await CanManageMembers(folderId, actorId.Value, ct);
        if (!isAllowed)
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "forbidden" });

        var existing = await dbContext.DocumentFolderMembers
            .FirstOrDefaultAsync(m => m.FolderId == folderId && m.UserId == userId, ct);

        if (existing == null)
            return NotFound(new { error = "member_not_found" });

        if (existing.IsOwner)
            return BadRequest(new { error = "owner_remove_forbidden" });

        dbContext.DocumentFolderMembers.Remove(existing);
        await dbContext.SaveChangesAsync(ct);
        return Ok(new { removed = true });
    }

    private Guid? GetCurrentUserId()
    {
        var raw = User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(raw, out var id) ? id : null;
    }

    private async Task<bool> CanManageMembers(Guid folderId, Guid userId, CancellationToken ct)
    {
        var folder = await dbContext.DocumentFolders.AsNoTracking().FirstOrDefaultAsync(x => x.Id == folderId, ct);
        if (folder == null)
            return false;

        if (folder.OwnerUserId == userId)
            return true;

        var member = await dbContext.DocumentFolderMembers.AsNoTracking()
            .FirstOrDefaultAsync(m => m.FolderId == folderId && m.UserId == userId, ct);

        return member?.CanManageMembers == true;
    }
}

public sealed record UpsertFolderMemberRequest(
    Guid UserId,
    bool CanView,
    bool CanDownload,
    bool CanUpload,
    bool CanEdit,
    bool CanDeleteFiles,
    bool CanDeleteFolder,
    bool CanShareLinks,
    bool CanManageMembers);

public sealed record DocumentFolderMemberDto(
    Guid Id,
    Guid FolderId,
    Guid UserId,
    bool IsOwner,
    bool CanView,
    bool CanDownload,
    bool CanUpload,
    bool CanEdit,
    bool CanDeleteFiles,
    bool CanDeleteFolder,
    bool CanShareLinks,
    bool CanManageMembers,
    DateTime CreatedAt)
{
    public DocumentFolderMemberDto(DocumentFolderMember member)
        : this(
            member.Id,
            member.FolderId,
            member.UserId,
            member.IsOwner,
            member.CanView,
            member.CanDownload,
            member.CanUpload,
            member.CanEdit,
            member.CanDeleteFiles,
            member.CanDeleteFolder,
            member.CanShareLinks,
            member.CanManageMembers,
            member.CreatedAt)
    {
    }
}
