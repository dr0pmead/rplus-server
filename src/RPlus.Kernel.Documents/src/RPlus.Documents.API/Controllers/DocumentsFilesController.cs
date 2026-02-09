using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RPlus.Documents.Api.Options;
using RPlus.Documents.Application.Interfaces;
using RPlus.Documents.Domain.Entities;
using RPlus.SDK.Access.Authorization;
using System.Security.Claims;

namespace RPlus.Documents.Api.Controllers;

[ApiController]
[Route("api/documents/files")]
public sealed class DocumentsFilesController(
    IDocumentsDbContext dbContext,
    IStorageService storage,
    IOptions<DocumentUploadOptions> uploadOptions,
    RPlus.Documents.Infrastructure.Encryption.IFieldEncryptor fieldEncryptor,
    RPlus.Documents.Infrastructure.Antivirus.IAntivirusScanner antivirus,
    RPlus.Documents.Infrastructure.Antivirus.AntivirusOptions antivirusOptions) : ControllerBase
{
    [HttpPost]
    [RequiresAnyPermission(
        "documents.files.upload",
        "documents.hr.files.upload",
        "documents.org.files.upload",
        "documents.department.files.upload")]
    public async Task<IActionResult> Upload([FromForm] UploadDocumentRequest request, CancellationToken ct)
    {
        if (request.File == null || request.File.Length == 0)
        {
            return BadRequest(new { error = "file_required" });
        }

        if (request.FolderId == Guid.Empty)
            return BadRequest(new { error = "folder_required" });

        var createdByUserId = GetCurrentUserId();
        var isServiceRequest = IsServiceRequest();

        if (createdByUserId == null && !isServiceRequest)
            return Unauthorized(new { error = "unauthorized" });

        var ownerUserId = request.OwnerUserId ?? createdByUserId;
        if (ownerUserId == null)
            return BadRequest(new { error = "owner_required" });

        if (!isServiceRequest)
        {
            var canUpload = await HasFolderPermissionAsync(request.FolderId, createdByUserId!.Value, p => p.CanUpload, ct);
            if (!canUpload)
                return StatusCode(StatusCodes.Status403Forbidden, new { error = "forbidden" });
        }

        var fileName = string.IsNullOrWhiteSpace(request.FileName)
            ? request.File.FileName
            : request.FileName.Trim();

        var extension = Path.GetExtension(fileName)?.ToLowerInvariant() ?? string.Empty;
        var options = uploadOptions.Value;

        var isImage = options.AllowedImageExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase);
        var isVideo = options.AllowedVideoExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase);
        var isDocument = options.AllowedDocumentExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase);

        if (!isImage && !isVideo && !isDocument)
        {
            return BadRequest(new { error = "unsupported_file_type" });
        }

        var limit = (isImage || isVideo) ? options.MaxMediaBytes : options.MaxDocumentBytes;
        if (request.File.Length > limit)
        {
            return BadRequest(new { error = "file_too_large", maxBytes = limit });
        }

        var contentType = string.IsNullOrWhiteSpace(request.ContentType)
            ? request.File.ContentType
            : request.ContentType.Trim();

        if (!string.IsNullOrWhiteSpace(contentType))
        {
            var allowedMime = isImage
                ? options.AllowedImageMimeTypes
                : isVideo
                    ? options.AllowedVideoMimeTypes
                    : options.AllowedDocumentMimeTypes;

            if (!allowedMime.Contains(contentType, StringComparer.OrdinalIgnoreCase))
                return BadRequest(new { error = "unsupported_mime_type" });
        }

        if (!fieldEncryptor.IsConfigured)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = "encryption_key_missing" });
        }

        var documentId = Guid.NewGuid();
        var storageKey = BuildStorageKey(
            request.OrganizationId,
            request.DepartmentId,
            ownerUserId,
            request.Folder,
            request.SubjectType,
            request.SubjectId,
            documentId,
            extension);

        var entity = new DocumentFile
        {
            Id = documentId,
            FolderId = request.FolderId,
            OwnerUserId = ownerUserId,
            OrganizationId = request.OrganizationId,
            DepartmentId = request.DepartmentId,
            SubjectType = string.IsNullOrWhiteSpace(request.SubjectType)
                ? null
                : fieldEncryptor.Encrypt(request.SubjectType.Trim()),
            SubjectId = request.SubjectId,
            DocumentType = string.IsNullOrWhiteSpace(request.DocumentType)
                ? null
                : fieldEncryptor.Encrypt(request.DocumentType.Trim()),
            CreatedByUserId = createdByUserId,
            FileName = fieldEncryptor.Encrypt(fileName),
            ContentType = fieldEncryptor.Encrypt(contentType),
            Size = request.File.Length,
            StorageKey = fieldEncryptor.Encrypt(storageKey),
            IsLocked = request.IsLocked,
            CreatedAt = DateTime.UtcNow
        };

        await using (var scanStream = request.File.OpenReadStream())
        {
            if (antivirusOptions.Enabled)
            {
                try
                {
                    var result = await antivirus.ScanAsync(scanStream, ct);
                    if (!result.IsClean)
                    {
                        return BadRequest(new { error = "file_infected" });
                    }
                }
                catch
                {
                    if (antivirusOptions.FailClosed)
                        return StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = "antivirus_unavailable" });
                }
            }
        }

        await using var stream = request.File.OpenReadStream();
        await storage.UploadAsync(storageKey, stream, contentType, ct);

        dbContext.DocumentFiles.Add(entity);
        await dbContext.SaveChangesAsync(ct);

        return Ok(new DocumentFileDto(entity, fieldEncryptor));
    }

    [HttpGet("{id:guid}")]
    [RequiresAnyPermission(
        "documents.files.read",
        "documents.hr.files.read",
        "documents.org.files.read",
        "documents.department.files.read")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var entity = await dbContext.DocumentFiles.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (entity == null)
            return NotFound(new { error = "file_not_found" });

        var userId = GetCurrentUserId();
        if (userId == null && !IsServiceRequest())
            return Unauthorized(new { error = "unauthorized" });

        if (!IsServiceRequest())
        {
            var canView = await HasFolderPermissionAsync(entity.FolderId, userId!.Value, p => p.CanView, ct);
            if (!canView)
                return StatusCode(StatusCodes.Status403Forbidden, new { error = "forbidden" });
        }

        return Ok(new DocumentFileDto(entity, fieldEncryptor));
    }

    [HttpGet("{id:guid}/download")]
    [RequiresAnyPermission(
        "documents.files.read",
        "documents.hr.files.read",
        "documents.org.files.read",
        "documents.department.files.read")]
    public async Task<IActionResult> Download(Guid id, CancellationToken ct)
    {
        var entity = await dbContext.DocumentFiles.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (entity == null)
            return NotFound(new { error = "file_not_found" });

        var userId = GetCurrentUserId();
        if (userId == null && !IsServiceRequest())
            return Unauthorized(new { error = "unauthorized" });

        if (!IsServiceRequest())
        {
            var canDownload = await HasFolderPermissionAsync(entity.FolderId, userId!.Value, p => p.CanDownload, ct);
            if (!canDownload)
                return StatusCode(StatusCodes.Status403Forbidden, new { error = "forbidden" });
        }

        var stream = await storage.DownloadAsync(fieldEncryptor.Decrypt(entity.StorageKey), ct);
        if (stream == null)
            return NotFound(new { error = "file_not_found" });

        return File(stream, fieldEncryptor.Decrypt(entity.ContentType), fieldEncryptor.Decrypt(entity.FileName));
    }

    [HttpPut("{id:guid}")]
    [RequiresAnyPermission(
        "documents.files.update",
        "documents.hr.files.update",
        "documents.org.files.update",
        "documents.department.files.update")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateDocumentRequest request, CancellationToken ct)
    {
        var entity = await dbContext.DocumentFiles.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (entity == null)
            return NotFound(new { error = "file_not_found" });

        var userId = GetCurrentUserId();
        if (userId == null && !IsServiceRequest())
            return Unauthorized(new { error = "unauthorized" });

        if (!IsServiceRequest())
        {
            var canEdit = await HasFolderPermissionAsync(entity.FolderId, userId!.Value, p => p.CanEdit, ct);
            if (!canEdit)
                return StatusCode(StatusCodes.Status403Forbidden, new { error = "forbidden" });
        }

        if (!string.IsNullOrWhiteSpace(request.FileName))
            entity.FileName = fieldEncryptor.Encrypt(request.FileName.Trim());

        if (!string.IsNullOrWhiteSpace(request.ContentType))
            entity.ContentType = fieldEncryptor.Encrypt(request.ContentType.Trim());

        await dbContext.SaveChangesAsync(ct);
        return Ok(new DocumentFileDto(entity, fieldEncryptor));
    }

    [HttpDelete("{id:guid}")]
    [RequiresAnyPermission(
        "documents.files.delete",
        "documents.hr.files.delete",
        "documents.org.files.delete",
        "documents.department.files.delete")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var entity = await dbContext.DocumentFiles.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (entity == null)
            return NotFound(new { error = "file_not_found" });

        var userId = GetCurrentUserId();
        if (userId == null && !IsServiceRequest())
            return Unauthorized(new { error = "unauthorized" });

        if (!IsServiceRequest())
        {
            var canDelete = await HasFolderPermissionAsync(entity.FolderId, userId!.Value, p => p.CanDeleteFiles, ct);
            if (!canDelete)
                return StatusCode(StatusCodes.Status403Forbidden, new { error = "forbidden" });
        }

        if (entity.IsLocked && !IsServiceRequest())
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "file_locked" });

        entity.DeletedAt = DateTime.UtcNow;
        entity.DeletedByUserId = GetCurrentUserId();
        await storage.DeleteAsync(fieldEncryptor.Decrypt(entity.StorageKey), ct);
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

    private static string BuildStorageKey(
        Guid? organizationId,
        Guid? departmentId,
        Guid? ownerUserId,
        string? folder,
        string? subjectType,
        Guid? subjectId,
        Guid documentId,
        string extension)
    {
        var org = organizationId?.ToString() ?? "org";
        var basePath = departmentId.HasValue
            ? $"org/{org}/departments/{departmentId}/documents"
            : $"org/{org}/users/{ownerUserId?.ToString() ?? "user"}/documents";

        var safeFolder = string.IsNullOrWhiteSpace(folder) ? "documents" : folder.Trim();
        var path = $"{basePath}/{safeFolder}";

        if (!string.IsNullOrWhiteSpace(subjectType))
        {
            path = $"{path}/{subjectType.Trim()}";
        }

        if (subjectId.HasValue)
        {
            path = $"{path}/{subjectId}";
        }

        return $"{path}/{documentId}{extension}";
    }
}

public sealed class UploadDocumentRequest
{
    [FromForm(Name = "file")]
    public IFormFile? File { get; set; }

    [FromForm(Name = "fileName")]
    public string? FileName { get; set; }

    [FromForm(Name = "contentType")]
    public string? ContentType { get; set; }

    [FromForm(Name = "ownerUserId")]
    public Guid? OwnerUserId { get; set; }

    [FromForm(Name = "folderId")]
    public Guid FolderId { get; set; }

    [FromForm(Name = "organizationId")]
    public Guid? OrganizationId { get; set; }

    [FromForm(Name = "departmentId")]
    public Guid? DepartmentId { get; set; }

    [FromForm(Name = "subjectType")]
    public string? SubjectType { get; set; }

    [FromForm(Name = "subjectId")]
    public Guid? SubjectId { get; set; }

    [FromForm(Name = "documentType")]
    public string? DocumentType { get; set; }

    [FromForm(Name = "folder")]
    public string? Folder { get; set; }

    [FromForm(Name = "isLocked")]
    public bool IsLocked { get; set; }
}

public sealed class UpdateDocumentRequest
{
    public string? FileName { get; set; }

    public string? ContentType { get; set; }
}

public sealed record DocumentFileDto(
    Guid Id,
    Guid FolderId,
    Guid? OwnerUserId,
    Guid? OrganizationId,
    Guid? DepartmentId,
    string? SubjectType,
    Guid? SubjectId,
    string? DocumentType,
    string FileName,
    string ContentType,
    long Size,
    bool IsLocked,
    DateTime CreatedAt)
{
    public DocumentFileDto(DocumentFile entity, RPlus.Documents.Infrastructure.Encryption.IFieldEncryptor encryptor)
        : this(
            entity.Id,
            entity.FolderId,
            entity.OwnerUserId,
            entity.OrganizationId,
            entity.DepartmentId,
            string.IsNullOrWhiteSpace(entity.SubjectType) ? null : encryptor.Decrypt(entity.SubjectType),
            entity.SubjectId,
            string.IsNullOrWhiteSpace(entity.DocumentType) ? null : encryptor.Decrypt(entity.DocumentType),
            encryptor.Decrypt(entity.FileName),
            encryptor.Decrypt(entity.ContentType),
            entity.Size,
            entity.IsLocked,
            entity.CreatedAt)
    {
    }
}
