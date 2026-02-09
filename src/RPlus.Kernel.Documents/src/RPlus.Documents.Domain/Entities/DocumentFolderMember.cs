namespace RPlus.Documents.Domain.Entities;

public sealed class DocumentFolderMember
{
    public Guid Id { get; set; }

    public Guid FolderId { get; set; }

    public Guid UserId { get; set; }

    public bool IsOwner { get; set; }

    public bool CanView { get; set; }

    public bool CanDownload { get; set; }

    public bool CanUpload { get; set; }

    public bool CanEdit { get; set; }

    public bool CanDeleteFiles { get; set; }

    public bool CanDeleteFolder { get; set; }

    public bool CanShareLinks { get; set; }

    public bool CanManageMembers { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
