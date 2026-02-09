namespace RPlus.SDK.AI;

/// <summary>
/// Embedding provider for vector generation.
/// </summary>
public interface IEmbeddingProvider
{
    /// <summary>
    /// Provider name (e.g., "ollama", "openai").
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Dimension of generated embeddings.
    /// </summary>
    int Dimensions { get; }

    /// <summary>
    /// Generate embedding for a single text.
    /// </summary>
    Task<float[]> EmbedAsync(string text, CancellationToken ct = default);

    /// <summary>
    /// Generate embeddings for multiple texts (batch).
    /// </summary>
    Task<float[][]> EmbedBatchAsync(
        IReadOnlyList<string> texts,
        CancellationToken ct = default);
}

/// <summary>
/// Embedding result with metadata.
/// </summary>
public sealed record EmbeddingResult(
    float[] Vector,
    int Dimensions,
    int TokenCount);
