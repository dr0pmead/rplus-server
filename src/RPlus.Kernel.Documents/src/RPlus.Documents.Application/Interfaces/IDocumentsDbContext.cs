using Microsoft.EntityFrameworkCore;
using RPlus.Documents.Domain.Entities;

namespace RPlus.Documents.Application.Interfaces;

public interface IDocumentsDbContext
{
    DbSet<DocumentFile> DocumentFiles { get; }
    DbSet<DocumentShare> DocumentShares { get; }
    DbSet<DocumentFolder> DocumentFolders { get; }
    DbSet<DocumentFolderMember> DocumentFolderMembers { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
