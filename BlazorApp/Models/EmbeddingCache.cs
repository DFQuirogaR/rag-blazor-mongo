using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.ComponentModel.DataAnnotations;

namespace BlazorApp.Models;

/// <summary>
/// Represents a cached embedding for a text query.
/// Used for efficient reuse of generated embeddings across sessions.
/// </summary>
public class EmbeddingCache
{
    /// <summary>
    /// Unique identifier for the cache entry.
    /// </summary>
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    /// <summary>
    /// Hash of the query text used as the cache key.
    /// </summary>
    [Required]
    [StringLength(100, ErrorMessage = "QueryHash cannot exceed 100 characters.")]
    public string QueryHash { get; set; } = string.Empty;

    /// <summary>
    /// The original query text (truncated for storage).
    /// </summary>
    [Required]
    [StringLength(2000, ErrorMessage = "QueryText cannot exceed 2000 characters.")]
    public string QueryText { get; set; } = string.Empty;

    /// <summary>
    /// The embedding vector.
    /// </summary>
    public float[] Embedding { get; set; } = Array.Empty<float>();

    /// <summary>
    /// Timestamp when this cache entry was created.
    /// </summary>
    [Required]
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Expiration date for this cache entry.
    /// Entries expire after this date and are cleaned up automatically.
    /// </summary>
    [Required]
    public DateTime ExpirationDate { get; set; }

    /// <summary>
    /// Number of times this cache entry has been accessed.
    /// </summary>
    [Range(0, int.MaxValue, ErrorMessage = "UsageCount must be a non-negative number.")]
    public int UsageCount { get; set; }

    /// <summary>
    /// Timestamp when this cache entry was last used.
    /// </summary>
    public DateTime? LastUsedDate { get; set; }
}

/// <summary>
/// Statistics for the embedding cache.
/// </summary>
public class CacheStatistics
{
    /// <summary>
    /// Total number of cache entries in MongoDB.
    /// </summary>
    public long TotalEntries { get; set; }

    /// <summary>
    /// Number of active (non-expired) cache entries.
    /// </summary>
    public long ActiveEntries { get; set; }

    /// <summary>
    /// Number of expired cache entries.
    /// </summary>
    public long ExpiredEntries { get; set; }

    /// <summary>
    /// Number of cache hits in memory cache.
    /// </summary>
    public int MemoryCacheHits { get; set; }

    /// <summary>
    /// Number of cache misses in memory cache.
    /// </summary>
    public int MemoryCacheMisses { get; set; }

    /// <summary>
    /// Current number of entries in memory cache.
    /// </summary>
    public int MemoryCacheSize { get; set; }

    /// <summary>
    /// Estimated total size of cache in bytes.
    /// </summary>
    public long EstimatedTotalSizeBytes { get; set; }

    /// <summary>
    /// Hit ratio (cache hits / (hits + misses)).
    /// </summary>
    public double HitRatio => MemoryCacheHits + MemoryCacheMisses > 0 
        ? (double)MemoryCacheHits / (MemoryCacheHits + MemoryCacheMisses) 
        : 0;

    /// <summary>
    /// Human-readable estimated total size.
    /// </summary>
    public string EstimatedTotalSize => FormatBytes(EstimatedTotalSizeBytes);

    private static string FormatBytes(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        double len = bytes;
        int order = 0;
        
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }
        
        return $"{len:0.##} {sizes[order]}";
    }

    /// <summary>
    /// Error statistics instance.
    /// </summary>
    public static CacheStatistics Error => new CacheStatistics
    {
        TotalEntries = -1,
        ActiveEntries = -1,
        ExpiredEntries = -1,
        MemoryCacheHits = -1,
        MemoryCacheMisses = -1,
        MemoryCacheSize = -1,
        EstimatedTotalSizeBytes = -1
    };

    /// <summary>
    /// Returns a string representation of the cache statistics.
    /// </summary>
    public override string ToString()
    {
        return $"Total: {TotalEntries}, Activos: {ActiveEntries}, Expirados: {ExpiredEntries}, " +
               $"Memoria: {MemoryCacheSize} entradas, Hit Ratio: {HitRatio:P2}, " +
               $"Tamaño estimado: {EstimatedTotalSize}";
    }
}
