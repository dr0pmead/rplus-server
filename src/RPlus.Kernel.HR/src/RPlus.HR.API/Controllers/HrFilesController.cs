using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RPlus.HR.Api.Authorization;
using RPlus.HR.Api.Services;
using RPlus.HR.Application.Interfaces;
using RPlus.HR.Domain.Entities;
using RPlus.SDK.Access.Authorization;

namespace RPlus.HR.Api.Controllers;

[ApiController]
[Route("api/hr")]
[Authorize]
public sealed class HrFilesController : ControllerBase
{
    private const long MaxFileBytes = 10 * 1024 * 1024;
    private const int FileNameMaxLength = 256;
    private readonly IHrDbContext _db;
    private readonly DocumentsGateway _documents;

    public HrFilesController(IHrDbContext db, DocumentsGateway documents)
    {
        _db = db;
        _documents = documents;
    }

    [HttpPost("profiles/{userId:guid}/files")]
    [AllowSelf]
    [RequiresPermission("hr.profile.edit")]
    [RequestSizeLimit(MaxFileBytes)]
    public async Task<IActionResult> UploadProfileFile(Guid userId, IFormFile file, CancellationToken ct)
    {
        if (userId == Guid.Empty)
            return BadRequest(new { error = "invalid_user_id" });
        if (file == null)
            return BadRequest(new { error = "file_missing" });
        if (file.Length <= 0)
            return BadRequest(new { error = "empty_file" });
        if (file.Length > MaxFileBytes)
            return BadRequest(new { error = "file_too_large" });

        var profileExists = await _db.EmployeeProfiles.AsNoTracking().AnyAsync(x => x.UserId == userId, ct);
        if (!profileExists)
            return NotFound(new { error = "profile_not_found" });

        try
        {
            var stored = await SaveFileAsync(userId, file, ct);
            return Ok(new { id = stored.Id, fileName = stored.FileName, contentType = stored.ContentType, size = stored.Size });
        }
        catch
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = "documents_unavailable" });
        }
    }

    [HttpPost("profiles/{userId:guid}/photo")]
    [AllowSelf]
    [RequiresPermission("hr.profile.edit")]
    [RequestSizeLimit(MaxFileBytes)]
    public async Task<IActionResult> UploadProfilePhoto(Guid userId, IFormFile file, CancellationToken ct)
    {
        if (userId == Guid.Empty)
            return BadRequest(new { error = "invalid_user_id" });
        if (file == null)
            return BadRequest(new { error = "file_missing" });
        if (file.Length <= 0)
            return BadRequest(new { error = "empty_file" });
        if (file.Length > MaxFileBytes)
            return BadRequest(new { error = "file_too_large" });

        var profile = await _db.EmployeeProfiles.FirstOrDefaultAsync(x => x.UserId == userId, ct);
        if (profile == null)
            return NotFound(new { error = "profile_not_found" });

        Guid? previousDocumentId = null;
        if (profile.PhotoFileId.HasValue)
        {
            var previousFile = await _db.HrFiles.FirstOrDefaultAsync(
                x => x.Id == profile.PhotoFileId && x.OwnerUserId == userId,
                ct);

            if (previousFile != null)
            {
                previousDocumentId = previousFile.DocumentId;
                _db.HrFiles.Remove(previousFile);
            }
        }

        try
        {
            var stored = await SaveFileAsync(userId, file, ct);
            profile.PhotoFileId = stored.Id;
            profile.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);

            if (previousDocumentId.HasValue)
            {
                await _documents.DeleteFileAsync(previousDocumentId.Value, ct);
            }

            return Ok(new { id = stored.Id, fileName = stored.FileName, contentType = stored.ContentType, size = stored.Size });
        }
        catch
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = "documents_unavailable" });
        }
    }

    [HttpGet("profiles/{userId:guid}/files/{fileId:guid}")]
    [AllowSelf]
    [RequiresPermission("hr.profile.view")]
    public async Task<IActionResult> GetProfileFile(Guid userId, Guid fileId, CancellationToken ct)
    {
        if (userId == Guid.Empty || fileId == Guid.Empty)
            return BadRequest(new { error = "invalid_request" });

        var file = await _db.HrFiles.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == fileId && x.OwnerUserId == userId, ct);

        if (file == null)
            return NotFound(new { error = "file_not_found" });

        if (file.DocumentId.HasValue)
        {
            var stream = await _documents.DownloadFileAsync(file.DocumentId.Value, ct);
            if (stream != null)
                return File(stream, file.ContentType, file.FileName);
        }

        return File(file.Data, file.ContentType, file.FileName);
    }

    private async Task<HrFile> SaveFileAsync(Guid userId, IFormFile file, CancellationToken ct)
    {
        var profile = await _db.EmployeeProfiles.FirstOrDefaultAsync(x => x.UserId == userId, ct);
        Guid? folderId = profile?.DocumentsFolderId;
        if (folderId == null)
        {
            var createdFolder = await _documents.EnsureUserFolderAsync(userId, ct);
            if (createdFolder.HasValue && profile != null)
            {
                profile.DocumentsFolderId = createdFolder.Value;
                profile.UpdatedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync(ct);
            }
            folderId = createdFolder;
        }

        if (!folderId.HasValue)
            throw new InvalidOperationException("documents_folder_missing");

        var documentId = await _documents.UploadFileAsync(
            userId,
            folderId.Value,
            file,
            isLocked: true,
            documentType: "HR",
            subjectType: "hr.profile",
            subjectId: userId,
            ct);

        if (!documentId.HasValue)
            throw new InvalidOperationException("documents_upload_failed");

        var data = Array.Empty<byte>();

        var stored = new HrFile
        {
            Id = Guid.NewGuid(),
            OwnerUserId = userId,
            FileName = NormalizeFileName(file.FileName),
            ContentType = NormalizeContentType(file.ContentType),
            Size = file.Length,
            Data = data,
            DocumentId = documentId,
            CreatedAt = DateTime.UtcNow
        };

        _db.HrFiles.Add(stored);
        await _db.SaveChangesAsync(ct);

        return stored;
    }

    private static string NormalizeFileName(string fileName)
    {
        var name = string.IsNullOrWhiteSpace(fileName) ? "file" : fileName.Trim();
        return name.Length > FileNameMaxLength ? name[..FileNameMaxLength] : name;
    }

    private static string NormalizeContentType(string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
            return "application/octet-stream";
        var normalized = contentType.Trim();
        return normalized.Length > 128 ? normalized[..128] : normalized;
    }
}
