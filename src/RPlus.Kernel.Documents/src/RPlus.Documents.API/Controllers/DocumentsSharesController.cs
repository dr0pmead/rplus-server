using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RPlus.Documents.Application.Interfaces;
using RPlus.Documents.Domain.Entities;
using RPlus.Documents.Infrastructure.Encryption;
using RPlus.SDK.Access.Authorization;
using System.Security.Claims;

namespace RPlus.Documents.Api.Controllers;

[ApiController]
[Route("api/documents/shares")]
public sealed class DocumentsSharesController(
    IDocumentsDbContext dbContext,
    IStorageService storage,
    IFieldEncryptor encryptor) : ControllerBase
{
    [HttpPost]
    [RequiresAnyPermission(
        "documents.shares.create",
        "documents.hr.shares.create",
        "documents.org.shares.create",
        "documents.department.shares.create")]
    public async Task<IActionResult> Create([FromBody] CreateDocumentShareRequest request, CancellationToken ct)
    {
        if (request.DocumentId == Guid.Empty)
            return BadRequest(new { error = "document_required" });

        if (request.ExpiresAt == default)
            return BadRequest(new { error = "expires_at_required" });

        var document = await dbContext.DocumentFiles.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.DocumentId, ct);
        if (document == null)
            return NotFound(new { error = "file_not_found" });

        var userId = GetCurrentUserId();
        if (userId == null)
            return Unauthorized(new { error = "unauthorized" });

        var canShare = await HasFolderPermissionAsync(document.FolderId, userId.Value, p => p.CanShareLinks, ct);
        if (!canShare)
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "forbidden" });

        var share = new DocumentShare
        {
            Id = Guid.NewGuid(),
            DocumentId = request.DocumentId,
            GrantedToUserId = request.GrantedToUserId,
            ExpiresAt = request.ExpiresAt,
            MaxDownloads = request.MaxDownloads,
            CreatedByUserId = GetCurrentUserId(),
            CreatedAt = DateTime.UtcNow
        };

        dbContext.DocumentShares.Add(share);
        await dbContext.SaveChangesAsync(ct);

        return Ok(new DocumentShareDto(share));
    }

    [HttpGet("{id:guid}")]
    [RequiresAnyPermission(
        "documents.shares.read",
        "documents.hr.shares.read",
        "documents.org.shares.read",
        "documents.department.shares.read")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var share = await dbContext.DocumentShares.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (share == null)
            return NotFound(new { error = "share_not_found" });

        var document = await dbContext.DocumentFiles.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == share.DocumentId, ct);
        if (document == null)
            return NotFound(new { error = "file_not_found" });

        var userId = GetCurrentUserId();
        if (userId == null)
            return Unauthorized(new { error = "unauthorized" });

        var canShare = await HasFolderPermissionAsync(document.FolderId, userId.Value, p => p.CanShareLinks, ct);
        if (!canShare)
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "forbidden" });

        return Ok(new DocumentShareDto(share));
    }

    [HttpGet("{id:guid}/download")]
    public async Task<IActionResult> Download(Guid id, CancellationToken ct)
    {
        var share = await dbContext.DocumentShares.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (share == null)
            return NotFound(new { error = "share_not_found" });

        if (DateTime.UtcNow > share.ExpiresAt)
            return StatusCode(StatusCodes.Status410Gone, new { error = "share_expired" });

        if (share.MaxDownloads.HasValue && share.DownloadCount >= share.MaxDownloads.Value)
            return StatusCode(StatusCodes.Status410Gone, new { error = "share_limit_reached" });

        if (share.GrantedToUserId.HasValue)
        {
            var current = GetCurrentUserId();
            if (current != share.GrantedToUserId)
                return StatusCode(StatusCodes.Status403Forbidden, new { error = "forbidden" });
        }

        var document = await dbContext.DocumentFiles.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == share.DocumentId, ct);
        if (document == null)
            return NotFound(new { error = "file_not_found" });

        if (!encryptor.IsConfigured)
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = "encryption_key_missing" });

        var stream = await storage.DownloadAsync(encryptor.Decrypt(document.StorageKey), ct);
        if (stream == null)
            return NotFound(new { error = "file_not_found" });

        share.DownloadCount += 1;
        await dbContext.SaveChangesAsync(ct);

        return File(stream, encryptor.Decrypt(document.ContentType), encryptor.Decrypt(document.FileName));
    }

    [HttpDelete("{id:guid}")]
    [RequiresAnyPermission(
        "documents.shares.revoke",
        "documents.hr.shares.revoke",
        "documents.org.shares.revoke",
        "documents.department.shares.revoke")]
    public async Task<IActionResult> Revoke(Guid id, CancellationToken ct)
    {
        var share = await dbContext.DocumentShares.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (share == null)
            return NotFound(new { error = "share_not_found" });

        var document = await dbContext.DocumentFiles.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == share.DocumentId, ct);
        if (document == null)
            return NotFound(new { error = "file_not_found" });

        var userId = GetCurrentUserId();
        if (userId == null)
            return Unauthorized(new { error = "unauthorized" });

        var canShare = await HasFolderPermissionAsync(document.FolderId, userId.Value, p => p.CanShareLinks, ct);
        if (!canShare)
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "forbidden" });

        dbContext.DocumentShares.Remove(share);
        await dbContext.SaveChangesAsync(ct);

        return Ok(new { revoked = true });
    }

    private Guid? GetCurrentUserId()
    {
        var raw = User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(raw, out var id) ? id : null;
    }

    private async Task<bool> HasFolderPermissionAsync(
        Guid folderId,
        Guid userId,
        Func<DocumentFolderMember, bool> predicate,
        CancellationToken ct)
    {
        var folder = await dbContext.DocumentFolders.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == folderId, ct);
        if (folder == null)
            return false;

        if (folder.OwnerUserId == userId)
            return true;

        var member = await dbContext.DocumentFolderMembers.AsNoTracking()
            .FirstOrDefaultAsync(m => m.FolderId == folderId && m.UserId == userId, ct);

        return member != null && predicate(member);
    }
}

public sealed record CreateDocumentShareRequest(
    Guid DocumentId,
    DateTime ExpiresAt,
    Guid? GrantedToUserId,
    int? MaxDownloads);

public sealed record DocumentShareDto(
    Guid Id,
    Guid DocumentId,
    Guid? GrantedToUserId,
    DateTime ExpiresAt,
    int? MaxDownloads,
    int DownloadCount,
    Guid? CreatedByUserId,
    DateTime CreatedAt)
{
    public DocumentShareDto(DocumentShare entity)
        : this(entity.Id, entity.DocumentId, entity.GrantedToUserId, entity.ExpiresAt, entity.MaxDownloads, entity.DownloadCount, entity.CreatedByUserId, entity.CreatedAt)
    {
    }
}
