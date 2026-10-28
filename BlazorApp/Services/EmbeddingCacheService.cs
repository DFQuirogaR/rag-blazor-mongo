using BlazorApp.Models;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using System.Security.Cryptography;
using System.Text;

namespace BlazorApp.Services;

/// <summary>
/// Interfaz para cache de embeddings
/// </summary>
public interface IEmbeddingCache
{
    /// <summary>
    /// Obtiene embedding desde cache si existe
    /// </summary>
    Task<float[]?> GetCachedEmbeddingAsync(string text, CancellationToken cancellationToken = default);

    /// <summary>
    /// Guarda embedding en cache
    /// </summary>
    Task CacheEmbeddingAsync(string text, float[] embedding, CancellationToken cancellationToken = default);

    /// <summary>
    /// Invalida cache para un texto específico
    /// </summary>
    Task InvalidateCacheAsync(string text, CancellationToken cancellationToken = default);

    /// <summary>
    /// Limpia cache expirado
    /// </summary>
    Task CleanupExpiredCacheAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtiene estadísticas del cache
    /// </summary>
    Task<CacheStatistics> GetCacheStatisticsAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Servicio de cache de embeddings con MongoDB
/// </summary>
public class EmbeddingCacheService : IEmbeddingCache
{
    private readonly ILogger<EmbeddingCacheService> _logger;
    private readonly IMongoCollection<EmbeddingCache> _cacheCollection;
    private readonly MemoryCache _memoryCache;

    public EmbeddingCacheService(
        ILogger<EmbeddingCacheService> logger,
        IMongoDatabase database)
    {
        _logger = logger;
        _cacheCollection = database.GetCollection<EmbeddingCache>("embedding_cache");
        _memoryCache = new MemoryCache();
    }

    public async Task<float[]?> GetCachedEmbeddingAsync(string text, CancellationToken cancellationToken = default)
    {
        try
        {
            var textHash = CalculateHash(text);

            // Primero buscar en cache en memoria
            var memoryCacheResult = _memoryCache.Get(textHash);
            if (memoryCacheResult != null)
            {
                _logger.LogDebug("Embedding obtenido de cache en memoria para hash: {Hash}", textHash);
                return memoryCacheResult;
            }

            // Buscar en MongoDB
            var cacheEntry = await _cacheCollection
                .Find(c => c.QueryHash == textHash && c.ExpirationDate > DateTime.UtcNow)
                .FirstOrDefaultAsync(cancellationToken);

            if (cacheEntry != null)
            {
                // Actualizar estadísticas de uso
                await UpdateCacheUsageAsync(cacheEntry.Id, cancellationToken);

                // Almacenar en cache en memoria
                _memoryCache.Set(textHash, cacheEntry.Embedding, TimeSpan.FromMinutes(5));

                _logger.LogDebug("Embedding obtenido de MongoDB cache para texto con hash: {Hash}", textHash);
                return cacheEntry.Embedding;
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener embedding desde cache para texto");
            return null;
        }
    }

    public async Task CacheEmbeddingAsync(string text, float[] embedding, CancellationToken cancellationToken = default)
    {
        try
        {
            var textHash = CalculateHash(text);
            
            // Primero almacenar en cache en memoria
            _memoryCache.Set(textHash, embedding, TimeSpan.FromMinutes(5));

            // Verificar si ya existe en MongoDB
            var existingEntry = await _cacheCollection
                .Find(c => c.QueryHash == textHash)
                .FirstOrDefaultAsync(cancellationToken);

            if (existingEntry != null)
            {
                // Actualizar entrada existente
                var update = Builders<EmbeddingCache>.Update
                    .Set(c => c.Embedding, embedding)
                    .Set(c => c.ExpirationDate, DateTime.UtcNow.AddHours(24))
                    .Inc(c => c.UsageCount, 1)
                    .Set(c => c.LastUsedDate, DateTime.UtcNow);

                await _cacheCollection.UpdateOneAsync(
                    c => c.Id == existingEntry.Id,
                    update,
                    cancellationToken: cancellationToken);

                _logger.LogDebug("Embedding actualizado en cache para hash: {Hash}", textHash);
            }
            else
            {
                // Crear nueva entrada
                var cacheEntry = new EmbeddingCache
                {
                    QueryHash = textHash,
                    QueryText = text.Length > 1000 ? text.Substring(0, 1000) + "..." : text,
                    Embedding = embedding,
                    CreatedDate = DateTime.UtcNow,
                    ExpirationDate = DateTime.UtcNow.AddHours(24), // 24 horas de cache
                    UsageCount = 1,
                    LastUsedDate = DateTime.UtcNow
                };

                await _cacheCollection.InsertOneAsync(cacheEntry, cancellationToken: cancellationToken);
                _logger.LogDebug("Nuevo embedding guardado en cache para hash: {Hash}", textHash);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al guardar embedding en cache");
        }
    }

    public async Task InvalidateCacheAsync(string text, CancellationToken cancellationToken = default)
    {
        try
        {
            var textHash = CalculateHash(text);

            // Invalidar en cache en memoria
            _memoryCache.Remove(textHash);

            // Invalidar en MongoDB
            var deleteResult = await _cacheCollection.DeleteOneAsync(
                c => c.QueryHash == textHash,
                cancellationToken);

            if (deleteResult.DeletedCount > 0)
            {
                _logger.LogDebug("Cache invalidado para hash: {Hash}", textHash);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al invalidar cache para texto");
        }
    }

    public async Task CleanupExpiredCacheAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // MongoDB elimina automáticamente documentos con TTL, pero también podemos hacer limpieza manual
            var deleteResult = await _cacheCollection.DeleteManyAsync(
                c => c.ExpirationDate <= DateTime.UtcNow,
                cancellationToken);

            if (deleteResult.DeletedCount > 0)
            {
                _logger.LogInformation("Limpieza de cache expirado: {DeletedCount} entradas eliminadas", 
                    deleteResult.DeletedCount);
            }

            // Limpiar cache en memoria
            _memoryCache.Cleanup();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al limpiar cache expirado");
        }
    }

    public async Task<CacheStatistics> GetCacheStatisticsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var totalEntries = await _cacheCollection.CountDocumentsAsync(_ => true, cancellationToken: cancellationToken);
            var activeEntries = await _cacheCollection.CountDocumentsAsync(
                c => c.ExpirationDate > DateTime.UtcNow, 
                cancellationToken: cancellationToken);
            
            var expiredEntries = totalEntries - activeEntries;
            
            var memoryCacheStats = _memoryCache.GetStatistics();

            // Calcular tamaño total aproximado
            var sizeSample = await _cacheCollection
                .Find(_ => true)
                .Limit(100)
                .ToListAsync(cancellationToken);

            var avgEntrySize = sizeSample.Any() 
                ? sizeSample.Average(e => EstimateSize(e)) 
                : 0;

            var estimatedTotalSize = avgEntrySize * totalEntries;

            return new CacheStatistics
            {
                TotalEntries = totalEntries,
                ActiveEntries = activeEntries,
                ExpiredEntries = expiredEntries,
                MemoryCacheHits = memoryCacheStats.Hits,
                MemoryCacheMisses = memoryCacheStats.Misses,
                MemoryCacheSize = memoryCacheStats.Size,
                EstimatedTotalSizeBytes = (long)estimatedTotalSize
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener estadísticas del cache");
            return CacheStatistics.Error;
        }
    }

    /// <summary>
    /// Actualiza estadísticas de uso de una entrada de cache
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
            _logger.LogWarning(ex, "Error al actualizar estadísticas de uso del cache");
        }
    }

    /// <summary>
    /// Calcula hash MD5 de un texto
    /// </summary>
    private string CalculateHash(string text)
    {
        using var md5 = MD5.Create();
        var hashBytes = md5.ComputeHash(Encoding.UTF8.GetBytes(text));
        return Convert.ToBase64String(hashBytes);
    }

    /// <summary>
    /// Estima el tamaño de una entrada de cache
    /// </summary>
    private double EstimateSize(EmbeddingCache entry)
    {
        var textSize = Encoding.UTF8.GetByteCount(entry.QueryText);
        var embeddingSize = entry.Embedding.Length * sizeof(float);
        var metadataSize = 100; // Tamaño aproximado de metadatos
        
        return textSize + embeddingSize + metadataSize;
    }

    /// <summary>
    /// Cache en memoria para acceso rápido
    /// </summary>
    private class MemoryCache
    {
        private readonly Dictionary<string, (float[] embedding, DateTime expiration)> _cache = new();
        private readonly object _lock = new();
        private int _hits = 0;
        private int _misses = 0;

        public float[]? Get(string key)
        {
            lock (_lock)
            {
                if (_cache.TryGetValue(key, out var entry))
                {
                    if (entry.expiration > DateTime.UtcNow)
                    {
                        _hits++;
                        return entry.embedding;
                    }
                    else
                    {
                        _cache.Remove(key);
                    }
                }
                _misses++;
                return null;
            }
        }

        public void Set(string key, float[] embedding, TimeSpan ttl)
        {
            lock (_lock)
            {
                _cache[key] = (embedding, DateTime.UtcNow.Add(ttl));
            }
        }

        public void Remove(string key)
        {
            lock (_lock)
            {
                _cache.Remove(key);
            }
        }

        public void Cleanup()
        {
            lock (_lock)
            {
                var now = DateTime.UtcNow;
                var expiredKeys = _cache.Where(kv => kv.Value.expiration <= now).Select(kv => kv.Key).ToList();
                
                foreach (var key in expiredKeys)
                {
                    _cache.Remove(key);
                }

                // Limitar tamaño del cache
                const int maxSize = 1000;
                if (_cache.Count > maxSize)
                {
                    var keysToRemove = _cache
                        .OrderBy(kv => kv.Value.expiration)
                        .Take(_cache.Count - maxSize)
                        .Select(kv => kv.Key)
                        .ToList();

                    foreach (var key in keysToRemove)
                    {
                        _cache.Remove(key);
                    }
                }
            }
        }

        public MemoryCacheStats GetStatistics()
        {
            lock (_lock)
            {
                return new MemoryCacheStats
                {
                    Size = _cache.Count,
                    Hits = _hits,
                    Misses = _misses,
                    HitRatio = _hits + _misses > 0 ? (double)_hits / (_hits + _misses) : 0
                };
            }
        }
    }

    /// <summary>
    /// Estadísticas del cache en memoria
    /// </summary>
    private class MemoryCacheStats
    {
        public int Size { get; set; }
        public int Hits { get; set; }
        public int Misses { get; set; }
        public double HitRatio { get; set; }
    }
}

/// <summary>
/// Estadísticas del cache
/// </summary>
public class CacheStatistics
{
    public long TotalEntries { get; set; }
    public long ActiveEntries { get; set; }
    public long ExpiredEntries { get; set; }
    public int MemoryCacheHits { get; set; }
    public int MemoryCacheMisses { get; set; }
    public int MemoryCacheSize { get; set; }
    public long EstimatedTotalSizeBytes { get; set; }

    public double HitRatio => MemoryCacheHits + MemoryCacheMisses > 0 
        ? (double)MemoryCacheHits / (MemoryCacheHits + MemoryCacheMisses) 
        : 0;

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

    public override string ToString()
    {
        return $"Total: {TotalEntries}, Activos: {ActiveEntries}, Expirados: {ExpiredEntries}, " +
               $"Memoria: {MemoryCacheSize} entradas, Hit Ratio: {HitRatio:P2}, " +
               $"Tamaño estimado: {EstimatedTotalSize}";
    }
}