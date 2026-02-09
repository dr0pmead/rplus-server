using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RPlus.Documents.Application.Interfaces;
using RPlus.Documents.Domain.Entities;
using RPlus.Documents.Infrastructure.Encryption;
using RPlus.SDK.Access.Authorization;
using System.Security.Claims;

namespace RPlus.Documents.Api.Controllers;

[ApiController]
[Route("api/documents/folders")]
public sealed class DocumentsFoldersController(
    IDocumentsDbContext dbContext,
    IFieldEncryptor encryptor) : ControllerBase
{
    [HttpGet]
    [RequiresAnyPermission("documents.folders.read", "documents.hr.folders.read", "documents.org.folders.read", "documents.department.folders.read")]
    public async Task<IActionResult> List([FromQuery] Guid? ownerUserId, [FromQuery] string? type, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var isService = IsServiceRequest();

        if (userId == null && !isService)
            return Unauthorized(new { error = "unauthorized" });

        var query = dbContext.DocumentFolders.AsNoTracking().AsQueryable();

        if (isService && ownerUserId.HasValue)
        {
            query = query.Where(f => f.OwnerUserId == ownerUserId.Value);
        }
        else
        {
            query = query.Where(f => f.OwnerUserId == userId || dbContext.DocumentFolderMembers.Any(m => m.FolderId == f.Id && m.UserId == userId));
        }

        if (!string.IsNullOrWhiteSpace(type))
            query = query.Where(f => f.Type == type);

        var folders = await query
            .OrderByDescending(f => f.CreatedAt)
            .ToListAsync(ct);

        return Ok(folders.Select(f => new DocumentFolderDto(f, encryptor)));
    }

    [HttpGet("{id:guid}")]
    [RequiresAnyPermission("documents.folders.read", "documents.hr.folders.read", "documents.org.folders.read", "documents.department.folders.read")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var folder = await dbContext.DocumentFolders.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (folder == null)
            return NotFound(new { error = "folder_not_found" });

        var userId = GetCurrentUserId();
        if (userId == null)
            return Unauthorized(new { error = "unauthorized" });

        var membership = await dbContext.DocumentFolderMembers.AsNoTracking()
            .FirstOrDefaultAsync(m => m.FolderId == id && m.UserId == userId, ct);

        if (folder.OwnerUserId != userId && membership == null)
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "forbidden" });

        return Ok(new DocumentFolderDto(folder, encryptor));
    }

    [HttpPost]
    [RequiresAnyPermission("documents.folders.create", "documents.hr.folders.create", "documents.org.folders.create", "documents.department.folders.create")]
    public async Task<IActionResult> Create([FromBody] CreateFolderRequest request, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var isService = IsServiceRequest();

        if (userId == null && !isService)
            return Unauthorized(new { error = "unauthorized" });

        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new { error = "name_required" });

        var ownerId = request.OwnerUserId ?? userId;
        if (ownerId == null)
            return BadRequest(new { error = "owner_required" });

        var isSystem = request.IsSystem;
        if (isSystem)
        {
            if (!isService)
            {
                var hasSystemPermission = await HasPermissionAsync(userId!.Value, "documents.system.manage", ct);
                if (!hasSystemPermission)
                    return StatusCode(StatusCodes.Status403Forbidden, new { error = "forbidden" });
            }
        }

        var folder = new DocumentFolder
        {
            Id = Guid.NewGuid(),
            OwnerUserId = ownerId.Value,
            OrganizationId = request.OrganizationId,
            DepartmentId = request.DepartmentId,
            Name = encryptor.Encrypt(request.Name.Trim()),
            Type = request.Type ?? "Project",
            IsSystem = isSystem,
            IsImmutable = request.IsImmutable || isSystem,
            CreatedAt = DateTime.UtcNow
        };

        dbContext.DocumentFolders.Add(folder);

        dbContext.DocumentFolderMembers.Add(new DocumentFolderMember
        {
            Id = Guid.NewGuid(),
            FolderId = folder.Id,
            UserId = ownerId.Value,
            IsOwner = true,
            CanView = true,
            CanDownload = true,
            CanUpload = true,
            CanEdit = true,
            CanDeleteFiles = true,
            CanDeleteFolder = true,
            CanShareLinks = true,
            CanManageMembers = true,
            CreatedAt = DateTime.UtcNow
        });

        await dbContext.SaveChangesAsync(ct);
        return Ok(new DocumentFolderDto(folder, encryptor));
    }

    [HttpPut("{id:guid}")]
    [RequiresAnyPermission("documents.folders.update", "documents.hr.folders.update", "documents.org.folders.update", "documents.department.folders.update")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateFolderRequest request, CancellationToken ct)
    {
        var folder = await dbContext.DocumentFolders.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (folder == null)
            return NotFound(new { error = "folder_not_found" });

        if (folder.IsImmutable)
            return BadRequest(new { error = "folder_immutable" });

        var userId = GetCurrentUserId();
        if (userId == null)
            return Unauthorized(new { error = "unauthorized" });

        var membership = await dbContext.DocumentFolderMembers.AsNoTracking()
            .FirstOrDefaultAsync(m => m.FolderId == id && m.UserId == userId, ct);

        if (folder.OwnerUserId != userId && (membership?.CanEdit != true))
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "forbidden" });

        if (!string.IsNullOrWhiteSpace(request.Name))
            folder.Name = encryptor.Encrypt(request.Name.Trim());

        await dbContext.SaveChangesAsync(ct);
        return Ok(new DocumentFolderDto(folder, encryptor));
    }

    [HttpDelete("{id:guid}")]
    [RequiresAnyPermission("documents.folders.delete", "documents.hr.folders.delete", "documents.org.folders.delete", "documents.department.folders.delete")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var folder = await dbContext.DocumentFolders.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (folder == null)
            return NotFound(new { error = "folder_not_found" });

        if (folder.IsImmutable)
            return BadRequest(new { error = "folder_immutable" });

        var userId = GetCurrentUserId();
        if (userId == null)
            return Unauthorized(new { error = "unauthorized" });

        var membership = await dbContext.DocumentFolderMembers.AsNoTracking()
            .FirstOrDefaultAsync(m => m.FolderId == id && m.UserId == userId, ct);

        if (folder.OwnerUserId != userId && (membership?.CanDeleteFolder != true))
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "forbidden" });

        dbContext.DocumentFolders.Remove(folder);
        await dbContext.SaveChangesAsync(ct);

        return Ok(new { deleted = true });
    }

    private Guid? GetCurrentUserId()
    {
        var raw = User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(raw, out var id) ? id : null;
    }

    private bool IsServiceRequest()
    {
        return string.Equals(User.FindFirstValue("auth_type"), "service_secret", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<bool> HasPermissionAsync(Guid userId, string permissionId, CancellationToken ct)
    {
        var access = HttpContext.RequestServices.GetRequiredService<RPlusGrpc.Access.AccessService.AccessServiceClient>();
        try
        {
            var response = await access.CheckPermissionAsync(new RPlusGrpc.Access.CheckPermissionRequest
            {
                UserId = userId.ToString(),
                TenantId = Guid.Empty.ToString(),
                PermissionId = permissionId,
                ApplicationId = "documents"
            }, cancellationToken: ct);
            return response.IsAllowed;
        }
        catch
        {
            return false;
        }
    }
}

public sealed record CreateFolderRequest(
    string Name,
    string? Type,
    Guid? OrganizationId,
    Guid? DepartmentId,
    bool IsSystem,
    bool IsImmutable,
    Guid? OwnerUserId);

public sealed record UpdateFolderRequest(string? Name);

public sealed record DocumentFolderDto(
    Guid Id,
    Guid OwnerUserId,
    Guid? OrganizationId,
    Guid? DepartmentId,
    string Name,
    string Type,
    bool IsSystem,
    bool IsImmutable,
    DateTime CreatedAt)
{
    public DocumentFolderDto(DocumentFolder folder, IFieldEncryptor encryptor)
        : this(
            folder.Id,
            folder.OwnerUserId,
            folder.OrganizationId,
            folder.DepartmentId,
            encryptor.Decrypt(folder.Name),
            folder.Type,
            folder.IsSystem,
            folder.IsImmutable,
            folder.CreatedAt)
    {
    }
}
