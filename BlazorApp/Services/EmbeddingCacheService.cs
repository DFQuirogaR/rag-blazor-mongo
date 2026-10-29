using BlazorApp.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using System.Security.Cryptography;
using System.Text;

namespace BlazorApp.Services;

/// <summary>
/// Service for caching embeddings with hybrid memory + MongoDB storage
/// Implements RF5.1 (embedding caching) and RNF2.3 (metrics tracking)
/// </summary>
public class EmbeddingCacheService : IEmbeddingCache
{
    private readonly ILogger<EmbeddingCacheService> _logger;
    private readonly IMongoCollection<EmbeddingCache> _cacheCollection;
    private readonly IMemoryCache _memoryCache;
    private readonly MemoryCacheStats _stats;

    // Configuration
    private readonly TimeSpan _memoryCacheTtl = TimeSpan.FromMinutes(30);
    private readonly TimeSpan _mongoCacheTtl = TimeSpan.FromHours(24);

    public EmbeddingCacheService(
        ILogger<EmbeddingCacheService> logger,
        IMongoDatabase database,
        IMemoryCache memoryCache)
    {
        _logger = logger;
        _cacheCollection = database.GetCollection<EmbeddingCache>("embedding_cache");
        _memoryCache = memoryCache;
        _stats = new MemoryCacheStats();
    }

    /// <summary>
    /// Gets cached embedding for a text query
    /// Implements hybrid caching: checks memory first, then MongoDB
    /// </summary>
    public async Task<float[]?> GetCachedEmbeddingAsync(string text, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                _logger.LogWarning("Empty or whitespace text provided for cache lookup");
                return null;
            }

            var cacheKey = CalculateCacheKey(text);

            // Check memory cache first (faster)
            if (_memoryCache.TryGetValue(cacheKey, out float[]? memoryEmbedding))
            {
                _stats.RecordHit();
                _logger.LogDebug("Embedding hit in memory cache for: {TextHash}", GetTextHash(text));
                return memoryEmbedding;
            }

            // Check MongoDB cache
            var cacheEntry = await _cacheCollection
                .Find(c => c.QueryHash == cacheKey && c.ExpirationDate > DateTime.UtcNow)
                .FirstOrDefaultAsync(cancellationToken);

            if (cacheEntry != null)
            {
                // Store in memory cache for faster subsequent access
                _memoryCache.Set(cacheKey, cacheEntry.Embedding, _memoryCacheTtl);
                _stats.RecordHit();

                // Update usage statistics
                await UpdateCacheUsageAsync(cacheEntry.Id, cancellationToken);

                _logger.LogDebug("Embedding hit in MongoDB cache for: {TextHash}, loaded to memory", GetTextHash(text));
                return cacheEntry.Embedding;
            }

            _stats.RecordMiss();
            _logger.LogTrace("Embedding miss in cache for: {TextHash}", GetTextHash(text));
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving embedding from cache for text");
            return null;
        }
    }

    /// <summary>
    /// Caches an embedding for a text query
    /// Uses hybrid approach: memory first (fast), then MongoDB (persistent)
    /// </summary>
    public async Task CacheEmbeddingAsync(string text, float[] embedding, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                _logger.LogWarning("Empty or whitespace text provided for cache storage");
                return;
            }

            if (embedding == null || embedding.Length == 0)
            {
                _logger.LogWarning("Empty or null embedding provided for cache storage");
                return;
            }

            var cacheKey = CalculateCacheKey(text);

            // Store in memory cache (fast access)
            _memoryCache.Set(cacheKey, embedding, _memoryCacheTtl);

            // Check if already exists in MongoDB
            var existingEntry = await _cacheCollection
                .Find(c => c.QueryHash == cacheKey)
                .FirstOrDefaultAsync(cancellationToken);

            if (existingEntry != null)
            {
                // Update existing entry
                var update = Builders<EmbeddingCache>.Update
                    .Set(c => c.Embedding, embedding)
                    .Set(c => c.ExpirationDate, DateTime.UtcNow.Add(_mongoCacheTtl))
                    .Set(c => c.LastUsedDate, DateTime.UtcNow);

                await _cacheCollection.UpdateOneAsync(
                    c => c.Id == existingEntry.Id,
                    update,
                    cancellationToken: cancellationToken);

                _logger.LogDebug("Updated cached embedding in MongoDB for: {TextHash}", GetTextHash(text));
            }
            else
            {
                // Create new cache entry
                var cacheEntry = new EmbeddingCache
                {
                    QueryHash = cacheKey,
                    QueryText = text.Length > 1000 ? text.Substring(0, 1000) + "..." : text,
                    Embedding = embedding,
                    CreatedDate = DateTime.UtcNow,
                    ExpirationDate = DateTime.UtcNow.Add(_mongoCacheTtl),
                    UsageCount = 1,
                    LastUsedDate = DateTime.UtcNow
                };

                await _cacheCollection.InsertOneAsync(cacheEntry, cancellationToken: cancellationToken);
                _logger.LogDebug("Cached new embedding in MongoDB for: {TextHash}", GetTextHash(text));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error caching embedding for text");
        }
    }

    /// <summary>
    /// Invalidates cache entry for a specific text
    /// Removes from both memory and MongoDB
    /// </summary>
    public async Task InvalidateCacheAsync(string text, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                _logger.LogWarning("Empty or whitespace text provided for cache invalidation");
                return;
            }

            var cacheKey = CalculateCacheKey(text);

            // Remove from memory cache
            _memoryCache.Remove(cacheKey);

            // Remove from MongoDB
            var result = await _cacheCollection.DeleteOneAsync(
                c => c.QueryHash == cacheKey,
                cancellationToken);

            if (result.DeletedCount > 0)
            {
                _logger.LogDebug("Invalidated cache entry for: {TextHash}", GetTextHash(text));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error invalidating cache for text");
        }
    }

    /// <summary>
    /// Checks if a text has a cached embedding
    /// </summary>
    public async Task<bool> ContainsKeyAsync(string text, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            var cacheKey = CalculateCacheKey(text);

            // Check memory first
            if (_memoryCache.TryGetValue(cacheKey, out _))
            {
                return true;
            }

            // Check MongoDB
            var count = await _cacheCollection.CountDocumentsAsync(
                c => c.QueryHash == cacheKey && c.ExpirationDate > DateTime.UtcNow,
                cancellationToken: cancellationToken);

            return count > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking cache key");
            return false;
        }
    }

    /// <summary>
    /// Clears all cache entries
    /// </summary>
    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Clear memory cache (Compact(1.0) evicts ~100% of entries; IMemoryCache has no enumeration API)
            if (_memoryCache is MemoryCache concreteMemoryCache)
            {
                concreteMemoryCache.Compact(1.0);
            }

            // Clear MongoDB cache
            var result = await _cacheCollection.DeleteManyAsync(_ => true, cancellationToken);
            _logger.LogInformation("Cleared {Count} cache entries from MongoDB", result.DeletedCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing cache");
        }
    }

    /// <summary>
    /// Gets cache statistics
    /// </summary>
    public async Task<CacheStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var memoryStats = _stats.GetStatistics();

            // Get MongoDB statistics
            var totalEntries = await _cacheCollection.CountDocumentsAsync(_ => true, cancellationToken: cancellationToken);
            var activeEntries = await _cacheCollection.CountDocumentsAsync(
                c => c.ExpirationDate > DateTime.UtcNow,
                cancellationToken: cancellationToken);
            
            // Get memory cache count
            var memoryCacheCount = GetMemoryCacheCount();

            var expiredEntries = totalEntries - activeEntries;

            // Estimate cache size
            var sizeSample = await _cacheCollection
                .Find(_ => true)
                .Limit(100)
                .ToListAsync(cancellationToken);

            var avgEntrySize = sizeSample.Any() 
                ? sizeSample.Average(e => EstimateSize(e)) 
                : 0;

            return new CacheStatistics
            {
                TotalEntries = totalEntries,
                ActiveEntries = activeEntries,
                ExpiredEntries = expiredEntries,
                MemoryCacheSize = memoryCacheCount,
                MemoryCacheHits = memoryStats.Hits,
                MemoryCacheMisses = memoryStats.Misses,
                EstimatedTotalSizeBytes = (long)(avgEntrySize * totalEntries)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting cache statistics");
            return CacheStatistics.Error;
        }
    }

    /// <summary>
    /// Cleans up expired cache entries
    /// </summary>
    public async Task CleanupExpiredEntriesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Remove expired entries from MongoDB
            var result = await _cacheCollection.DeleteManyAsync(
                c => c.ExpirationDate <= DateTime.UtcNow,
                cancellationToken);

            if (result.DeletedCount > 0)
            {
                _logger.LogInformation("Cleaned up {Count} expired cache entries", result.DeletedCount);
            }

            // Memory cache handles expiration automatically via IMemoryCache
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cleaning up expired cache entries");
        }
    }

    /// <summary>
    /// Invalidates cache for a specific document
    /// Called by DocumentProcessor when documents change
    /// </summary>
    public async Task InvalidateCacheForDocumentAsync(string documentId, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(documentId))
            {
                _logger.LogWarning("Empty or whitespace document ID provided for cache invalidation");
                return;
            }

            // Invalidate cache entries associated with this document
            // This requires tracking which queries used chunks from which documents
            // For now, we'll invalidate based on any cache entry that references this document
            // In a more advanced implementation, we'd track this explicitly
            
            var deleted = await _cacheCollection.DeleteManyAsync(
                c => c.QueryText.Contains(documentId),
                cancellationToken);

            if (deleted.DeletedCount > 0)
            {
                _logger.LogInformation("Invalidated {Count} cache entries for document {DocumentId}", 
                    deleted.DeletedCount, documentId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error invalidating cache for document {DocumentId}", documentId);
        }
    }

    #region Helper Methods

    /// <summary>
    /// Calculates a unique cache key for a text query
    /// </summary>
    private string CalculateCacheKey(string text)
    {
        using var md5 = MD5.Create();
        var hashBytes = md5.ComputeHash(Encoding.UTF8.GetBytes(text));
        return Convert.ToBase64String(hashBytes);
    }

    /// <summary>
    /// Gets a short hash of text for logging
    /// </summary>
    private string GetTextHash(string text)
    {
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(text));
        return Convert.ToHexString(hashBytes).Substring(0, 8);
    }

    /// <summary>
    /// Gets the current count of items in memory cache
    /// Uses reflection to access internal cache count
    /// </summary>
    private int GetMemoryCacheCount()
    {
        try
        {
            var fields = _memoryCache.GetType().GetFields(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            foreach (var field in fields)
            {
                if (field.Name == "_traceSource" || field.Name == "_trackGCCollect")
                    continue;
                
                if (field.FieldType.Name.Contains("ConcurrentDictionary"))
                {
                    var value = field.GetValue(_memoryCache);
                    if (value is System.Collections.Concurrent.ConcurrentDictionary<string, object> dict)
                    {
                        return dict.Count;
                    }
                }
            }
        }
        catch
        {
            // If we can't get the count, return -1
        }
        
        return -1;
    }

    /// <summary>
    /// Estimates the size of a cache entry in bytes
    /// </summary>
    private double EstimateSize(EmbeddingCache entry)
    {
        var textSize = Encoding.UTF8.GetByteCount(entry.QueryText ?? string.Empty);
        var embeddingSize = entry.Embedding?.Length ?? 0 * sizeof(float);
        var metadataSize = 100; // Approximate metadata size
        
        return textSize + embeddingSize + metadataSize;
    }

    /// <summary>
    /// Updates cache usage statistics in MongoDB
    /// </summary>
    private async Task UpdateCacheUsageAsync(string cacheId, CancellationToken cancellationToken)
    {
        try
        {
            var update = Builders<EmbeddingCache>.Update
                .Inc(c => c.UsageCount, 1)
                .Set(c => c.LastUsedDate, DateTime.UtcNow);

            await _cacheCollection.UpdateOneAsync(
                c => c.Id == cacheId,
                update,
                cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error updating cache usage statistics");
        }
    }

    #endregion

    #region Memory Cache Statistics

    /// <summary>
    /// Thread-safe statistics tracker for memory cache
    /// </summary>
    private class MemoryCacheStats
    {
        private int _hits = 0;
        private int _misses = 0;
        private readonly object _lock = new();

        public void RecordHit()
        {
            lock (_lock)
            {
                _hits++;
            }
        }

        public void RecordMiss()
        {
            lock (_lock)
            {
                _misses++;
            }
        }

        public (int Hits, int Misses, double HitRatio) GetStatistics()
        {
            lock (_lock)
            {
                var total = _hits + _misses;
                var hitRatio = total > 0 ? (double)_hits / total : 0.0;
                return (_hits, _misses, hitRatio);
            }
        }

        public int TotalHits => _hits;
        public int TotalMisses => _misses;
        public double HitRatio
        {
            get
            {
                lock (_lock)
                {
                    var total = _hits + _misses;
                    return total > 0 ? (double)_hits / total : 0.0;
                }
            }
        }
    }

    #endregion
}
