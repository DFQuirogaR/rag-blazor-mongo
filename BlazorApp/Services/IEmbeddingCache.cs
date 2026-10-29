using BlazorApp.Models;

namespace BlazorApp.Services;

/// <summary>
/// Interface for embedding cache operations
/// Implements RF5.1 (embedding caching) and RNF2.3 (metrics tracking)
/// </summary>
public interface IEmbeddingCache
{
    /// <summary>
    /// Gets cached embedding for a text query
    /// </summary>
    /// <param name="text">The text to look up</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Cached embedding if found, null otherwise</returns>
    Task<float[]?> GetCachedEmbeddingAsync(string text, CancellationToken cancellationToken = default);

    /// <summary>
    /// Caches an embedding for a text query
    /// </summary>
    /// <param name="text">The text to cache</param>
    /// <param name="embedding">The embedding vector</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task CacheEmbeddingAsync(string text, float[] embedding, CancellationToken cancellationToken = default);

    /// <summary>
    /// Invalidates cache entry for a specific text
    /// </summary>
    /// <param name="text">The text to invalidate</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task InvalidateCacheAsync(string text, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a text has a cached embedding
    /// </summary>
    /// <param name="text">The text to check</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if cached, false otherwise</returns>
    Task<bool> ContainsKeyAsync(string text, CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears all cache entries
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    Task ClearAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets cache statistics
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Cache statistics</returns>
    Task<CacheStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Cleans up expired cache entries
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    Task CleanupExpiredEntriesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Invalidates cache for a specific document ( Called by DocumentProcessor )
    /// </summary>
    /// <param name="documentId">The document ID to invalidate</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task InvalidateCacheForDocumentAsync(string documentId, CancellationToken cancellationToken = default);
}
