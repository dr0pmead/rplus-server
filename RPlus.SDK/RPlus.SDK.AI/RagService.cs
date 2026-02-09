namespace RPlus.SDK.AI;

/// <summary>
/// RAG (Retrieval-Augmented Generation) service.
/// </summary>
public interface IRagService
{
    /// <summary>
    /// Query for relevant documents/memories.
    /// </summary>
    Task<IReadOnlyList<RagResult>> QueryAsync(
        string query,
        RagQueryOptions? options = null,
        CancellationToken ct = default);

    /// <summary>
    /// Index content for retrieval.
    /// </summary>
    Task IndexAsync(
        RagDocument document,
        CancellationToken ct = default);

    /// <summary>
    /// Index multiple documents.
    /// </summary>
    Task IndexBatchAsync(
        IReadOnlyList<RagDocument> documents,
        CancellationToken ct = default);

    /// <summary>
    /// Delete document from index.
    /// </summary>
    Task DeleteAsync(Guid documentId, CancellationToken ct = default);
}

/// <summary>
/// RAG query options.
/// </summary>
public sealed record RagQueryOptions
{
    public int TopK { get; init; } = 5;
    public float MinScore { get; init; } = 0.7f;
    public Guid? UserId { get; init; }
    public string? Domain { get; init; }
    public Dictionary<string, string>? Filters { get; init; }
}

/// <summary>
/// Document for RAG indexing.
/// </summary>
public sealed record RagDocument
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string Content { get; init; }
    public required string Domain { get; init; }
    public Guid? UserId { get; init; }
    public string? Source { get; init; }
    public Dictionary<string, string> Metadata { get; init; } = [];
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// RAG query result.
/// </summary>
public sealed record RagResult(
    Guid DocumentId,
    string Content,
    float Score,
    Dictionary<string, string>? Metadata = null);
