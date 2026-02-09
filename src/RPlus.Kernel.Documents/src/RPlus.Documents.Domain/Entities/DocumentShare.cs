namespace RPlus.Documents.Domain.Entities;

public sealed class DocumentShare
{
    public Guid Id { get; set; }

    public Guid DocumentId { get; set; }

    public Guid? GrantedToUserId { get; set; }

    public DateTime ExpiresAt { get; set; }

    public int? MaxDownloads { get; set; }

    public int DownloadCount { get; set; }

    public Guid? CreatedByUserId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
