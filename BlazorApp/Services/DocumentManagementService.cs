using BlazorApp.Models;
using MongoDB.Driver;
using System.Runtime.CompilerServices;

namespace BlazorApp.Services;

/// <summary>
/// Servicio de gestión de documentos procesados.
/// Proporciona operaciones CRUD completas para documentos y chunks.
/// </summary>
public class DocumentManagementService
{
    private readonly DocumentProcessor _documentProcessor;
    private readonly IMongoCollection<DocumentModel> _documentsCollection;
    private readonly IMongoCollection<ChunkModel> _chunksCollection;
    private readonly ILogger<DocumentManagementService> _logger;

    public DocumentManagementService(
        DocumentProcessor documentProcessor,
        IMongoDatabase database,
        ILogger<DocumentManagementService> logger)
    {
        _documentProcessor = documentProcessor;
        _documentsCollection = database.GetCollection<DocumentModel>("documents");
        _chunksCollection = database.GetCollection<ChunkModel>("chunks");
        _logger = logger;
    }

    /// <summary>
    /// Obtiene la lista de documentos con soporte para paginación y filtrado.
    /// </summary>
    /// <param name="page">Número de página (0-indexed)</param>
    /// <param name="pageSize">Tamaño de página</param>
    /// <param name="status">Filtro por estado de procesamiento (opcional)</param>
    /// <param name="searchTerm">Término de búsqueda en nombre de archivo o tags (opcional)</param>
    /// <returns>Pagina de documentos con metadatos de paginación</returns>
    public async Task<PagedResult<DocumentModel>> GetDocumentsAsync(
        int page = 0,
        int pageSize = 20,
        string? status = null,
        string? searchTerm = null)
    {
        try
        {
            // Construir filtro base
            var filterBuilder = Builders<DocumentModel>.Filter;
            var filters = new List<FilterDefinition<DocumentModel>>();

            // Filtro por estado
            if (!string.IsNullOrWhiteSpace(status))
            {
                var normalizedStatus = status.Trim().ToLowerInvariant() switch
                {
                    "pending" => DocumentModel.DocumentStatus.Pending.ToString(),
                    "processing" => DocumentModel.DocumentStatus.Processing.ToString(),
                    "completed" => DocumentModel.DocumentStatus.Completed.ToString(),
                    "failed" or "error" => DocumentModel.DocumentStatus.Failed.ToString(),
                    _ => status.Trim()
                };

                // Existing data contains both PascalCase and lowercase status values.
                // Use a case-insensitive exact match so the UI filter works for both.
                var statusPattern = $"^{System.Text.RegularExpressions.Regex.Escape(normalizedStatus)}$";
                filters.Add(filterBuilder.Regex(
                    d => d.ProcessingStatus,
                    new MongoDB.Bson.BsonRegularExpression(statusPattern, "i")));
            }

            // Filtro por búsqueda
            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var searchLower = searchTerm.Trim().ToLower();
                
                // Buscar en nombre de archivo o tags
                var nameFilter = filterBuilder.Regex(d => d.FileName, new MongoDB.Bson.BsonRegularExpression(searchLower, "i"));
                var tagFilter = filterBuilder.Regex(d => d.Tags, new MongoDB.Bson.BsonRegularExpression(searchLower, "i"));
                
                filters.Add(filterBuilder.Or(nameFilter, tagFilter));
            }

            var combinedFilter = filters.Count > 0 
                ? filterBuilder.And(filters) 
                : filterBuilder.Exists("_id", true);

            // Contar total de documentos
            var totalCount = await _documentsCollection.CountDocumentsAsync(combinedFilter);

            // Obtir documentos con paginación
            var documents = await _documentsCollection
                .Find(combinedFilter)
                .SortByDescending(d => d.UploadDate)
                .Skip(page * pageSize)
                .Limit(pageSize)
                .ToListAsync();

            _logger.LogInformation("Retrieved {Count} documents (Page: {Page}, PageSize: {PageSize}, Total: {Total})", 
                documents.Count, page, pageSize, totalCount);

            return new PagedResult<DocumentModel>
            {
                Items = documents,
                Page = page,
                PageSize = pageSize,
                TotalCount = (int)totalCount,
                TotalPages = (int)Math.Ceiling((double)totalCount / pageSize)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving documents");
            return new PagedResult<DocumentModel>
            {
                Items = new List<DocumentModel>(),
                Page = page,
                PageSize = pageSize,
                TotalCount = 0,
                TotalPages = 0
            };
        }
    }

    /// <summary>
    /// Obtiene un documento por su ID.
    /// </summary>
    /// <param name="documentId">ID del documento</param>
    /// <returns>El documento encontrado o null si no existe</returns>
    public async Task<DocumentModel?> GetDocumentByIdAsync(string documentId)
    {
        try
        {
            var document = await _documentsCollection.Find(d => d.Id == documentId).FirstOrDefaultAsync();
            
            if (document == null)
            {
                _logger.LogWarning("Document not found: {DocumentId}", documentId);
            }
            
            return document;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving document: {DocumentId}", documentId);
            return null;
        }
    }

    /// <summary>
    /// Obtiene los chunks de un documento específico.
    /// </summary>
    /// <param name="documentId">ID del documento</param>
    /// <param name="chunkIndex">Índice opcional para obtener un chunk específico</param>
    /// <returns>Lista de chunks o chunk específico</returns>
    public async Task<List<ChunkModel>> GetDocumentChunksAsync(string documentId, int? chunkIndex = null)
    {
        try
        {
            var filterBuilder = Builders<ChunkModel>.Filter;
            var filters = new List<FilterDefinition<ChunkModel>> { filterBuilder.Eq(c => c.DocumentId, documentId) };

            if (chunkIndex.HasValue)
            {
                filters.Add(filterBuilder.Eq(c => c.ChunkIndex, chunkIndex.Value));
            }

            var filter = filterBuilder.And(filters);

            var chunks = await _chunksCollection
                .Find(filter)
                .SortBy(c => c.ChunkIndex)
                .ToListAsync();

            _logger.LogInformation("Retrieved {Count} chunks for document: {DocumentId}", chunks.Count, documentId);

            return chunks;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving chunks for document: {DocumentId}", documentId);
            return new List<ChunkModel>();
        }
    }

    /// <summary>
    /// Re-procesa un documento (elimina chunks existentes y vuelve a procesar).
    /// </summary>
    /// <param name="documentId">ID del documento a re-procesar</param>
    /// <returns>El documento procesado o null si falla</returns>
    public async Task<DocumentModel?> ReprocessDocumentAsync(string documentId)
    {
        try
        {
            var document = await GetDocumentByIdAsync(documentId);
            if (document == null)
            {
                _logger.LogWarning("Document not found for reprocessing: {DocumentId}", documentId);
                return null;
            }

            _logger.LogInformation("Starting reprocess of document: {DocumentId} ({FileName})", 
                documentId, document.FileName);

            // Marcar como procesando
            await _documentsCollection.UpdateOneAsync(
                d => d.Id == documentId,
                Builders<DocumentModel>.Update
                    .Set(d => d.ProcessingStatus, DocumentModel.DocumentStatus.Processing.ToString())
                    .Unset(d => d.ErrorMessage));

            // Eliminar chunks existentes
            await _chunksCollection.DeleteManyAsync(c => c.DocumentId == documentId);
            _logger.LogDebug("Deleted existing chunks for document: {DocumentId}", documentId);

            // Re-procesar usando DocumentProcessor
            var result = await _documentProcessor.ProcessDocumentAsync(document.FilePath);

            if (result != null)
            {
                _logger.LogInformation("Document reprocessed successfully: {DocumentId}", documentId);
            }
            else
            {
                _logger.LogError("Document reprocessing failed: {DocumentId}", documentId);
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reprocessing document: {DocumentId}", documentId);
            
            // Actualizar estado a error
            await _documentsCollection.UpdateOneAsync(
                d => d.Id == documentId,
                Builders<DocumentModel>.Update
                    .Set(d => d.ProcessingStatus, DocumentModel.DocumentStatus.Failed.ToString())
                    .Set(d => d.ErrorMessage, ex.Message));

            return null;
        }
    }

    /// <summary>
    /// Elimina un documento y todos sus chunks.
    /// </summary>
    /// <param name="documentId">ID del documento a eliminar</param>
    /// <returns>true si se eliminó correctamente, false en caso contrario</returns>
    public async Task<bool> DeleteDocumentAsync(string documentId)
    {
        try
        {
            var document = await GetDocumentByIdAsync(documentId);
            if (document == null)
            {
                _logger.LogWarning("Document not found for deletion: {DocumentId}", documentId);
                return false;
            }

            _logger.LogInformation("Deleting document: {DocumentId} ({FileName})", 
                documentId, document.FileName);

            // Eliminar todos los chunks
            var chunksResult = await _chunksCollection.DeleteManyAsync(c => c.DocumentId == documentId);
            _logger.LogDebug("Deleted {Count} chunks for document: {DocumentId}", chunksResult.DeletedCount, documentId);

            // Eliminar el documento
            var docResult = await _documentsCollection.DeleteOneAsync(d => d.Id == documentId);

            if (docResult.DeletedCount > 0)
            {
                _logger.LogInformation("Document deleted successfully: {DocumentId}", documentId);
                return true;
            }
            else
            {
                _logger.LogWarning("Document deletion failed: {DocumentId}", documentId);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting document: {DocumentId}", documentId);
            return false;
        }
    }

    /// <summary>
    /// Obtiene estadísticas de procesamiento de documentos.
    /// </summary>
    /// <returns>Objeto con estadísticas de documentos</returns>
    public async Task<DocumentStatistics> GetStatisticsAsync()
    {
        try
        {
            // Contar documentos por estado
            var statusCounts = await _documentsCollection
                .Aggregate()
                .Group(d => d.ProcessingStatus, 
                    g => new { Status = g.Key, Count = g.Count() })
                .ToListAsync();

            // Obtener total de documentos
            var totalDocuments = await _documentsCollection.CountDocumentsAsync(_ => true);

            // Obtener total de chunks
            var totalChunks = await _chunksCollection.CountDocumentsAsync(_ => true);

            // Obtener total de documentos procesados con éxito
            var completedDocuments = await _documentsCollection
                .CountDocumentsAsync(d => d.ProcessingStatus == DocumentModel.DocumentStatus.Completed.ToString());

            // Obtener total de archivos procesados
            var totalSize = await _documentsCollection
                .Aggregate()
                .Group(d => 1, g => new { TotalSize = g.Sum(d => d.FileSize) })
                .FirstOrDefaultAsync();

            // Obtener documentos más recientes
            var recentDocuments = await _documentsCollection
                .Find(_ => true)
                .SortByDescending(d => d.UploadDate)
                .Limit(10)
                .ToListAsync();

            // Obtener documentos con más chunks
            var documentsWithMostChunks = await _chunksCollection
                .Aggregate()
                .Group(c => c.DocumentId, 
                    g => new { DocumentId = g.Key, ChunkCount = g.Count() })
                .SortByDescending(x => x.ChunkCount)
                .Limit(5)
                .ToListAsync();

            // Mapear documentos con sus nombres
            var documentIdNames = await _documentsCollection
                .Find(d => documentsWithMostChunks.Select(x => x.DocumentId).Contains(d.Id))
                .ToListAsync()
                .ConfigureAwait(false);

            var docNameMap = documentIdNames.ToDictionary(d => d.Id, d => d.FileName);

            var statistics = new DocumentStatistics
            {
                TotalDocuments = (int)totalDocuments,
                TotalChunks = (int)totalChunks,
                TotalSizeBytes = totalSize?.TotalSize ?? 0,
                CompletedDocuments = (int)completedDocuments,
                ProcessingDocuments = statusCounts
                    .Where(s => s.Status == DocumentModel.DocumentStatus.Processing.ToString())
                    .Select(s => s.Count)
                    .FirstOrDefault(),
                FailedDocuments = statusCounts
                    .Where(s => s.Status == DocumentModel.DocumentStatus.Failed.ToString())
                    .Select(s => s.Count)
                    .FirstOrDefault(),
                StatusCounts = statusCounts.ToDictionary(s => s.Status, s => (int)s.Count),
                RecentDocuments = recentDocuments,
                DocumentsWithMostChunks = documentsWithMostChunks
                    .Select(x => new DocumentChunkCount
                    {
                        DocumentId = x.DocumentId,
                        ChunkCount = x.ChunkCount,
                        FileName = docNameMap.TryGetValue(x.DocumentId, out var name) ? name : "Desconocido"
                    })
                    .ToList()
            };

            _logger.LogInformation("Statistics retrieved: {TotalDocs} docs, {TotalChunks} chunks, {Completed} completed",
                statistics.TotalDocuments, statistics.TotalChunks, statistics.CompletedDocuments);

            return statistics;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving document statistics");
            return new DocumentStatistics();
        }
    }

    /// <summary>
    /// Obtiene documentos con un estado específico.
    /// </summary>
    /// <param name="status">Estado a filtrar</param>
    /// <returns>Lista de documentos con el estado especificado</returns>
    public async Task<List<DocumentModel>> GetDocumentsByStatusAsync(string status)
    {
        try
        {
            var documents = await _documentsCollection
                .Find(d => d.ProcessingStatus == status)
                .SortByDescending(d => d.UploadDate)
                .ToListAsync();

            return documents;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving documents by status: {Status}", status);
            return new List<DocumentModel>();
        }
    }

    /// <summary>
    /// Actualiza las tags de un documento.
    /// </summary>
    /// <param name="documentId">ID del documento</param>
    /// <param name="tags">Nueva lista de tags</param>
    /// <returns>true si se actualizó correctamente</returns>
    public async Task<bool> UpdateDocumentTagsAsync(string documentId, List<string> tags)
    {
        try
        {
            var result = await _documentsCollection.UpdateOneAsync(
                d => d.Id == documentId,
                Builders<DocumentModel>.Update
                    .Set(d => d.Tags, tags));

            return result.ModifiedCount > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating tags for document: {DocumentId}", documentId);
            return false;
        }
    }

    /// <summary>
    /// Busca documentos por Metadata (tags, nombre de archivo, etc.)
    /// </summary>
    /// <param name="tags">Tags a buscar (documentos que contengan ALGUNA de las tags)</param>
    /// <param name="fileName">Nombre de archivo a buscar (parcial)</param>
    /// <param name="page">Número de página</param>
    /// <param name="pageSize">Tamaño de página</param>
    /// <returns>Pagina de documentos que coinciden con los criterios</returns>
    public async Task<PagedResult<DocumentModel>> SearchDocumentsAsync(
        List<string>? tags = null,
        string? fileName = null,
        int page = 0,
        int pageSize = 20)
    {
        try
        {
            var filterBuilder = Builders<DocumentModel>.Filter;
            var filters = new List<FilterDefinition<DocumentModel>>();

            // Filtro por tags (OR entre tags)
            if (tags != null && tags.Count > 0)
            {
                var tagFilters = tags.Select(tag => 
                    filterBuilder.Regex(d => d.Tags, new MongoDB.Bson.BsonRegularExpression(tag, "i"))).ToList();
                filters.Add(filterBuilder.Or(tagFilters));
            }

            // Filtro por nombre de archivo
            if (!string.IsNullOrWhiteSpace(fileName))
            {
                var fileNameLower = fileName.Trim().ToLower();
                filters.Add(filterBuilder.Regex(d => d.FileName, new MongoDB.Bson.BsonRegularExpression(fileNameLower, "i")));
            }

            var combinedFilter = filters.Count > 0
                ? filterBuilder.And(filters)
                : filterBuilder.Exists("_id", true);

            // Contar total
            var totalCount = await _documentsCollection.CountDocumentsAsync(combinedFilter);

            // Obtir documentos
            var documents = await _documentsCollection
                .Find(combinedFilter)
                .SortByDescending(d => d.UploadDate)
                .Skip(page * pageSize)
                .Limit(pageSize)
                .ToListAsync();

            return new PagedResult<DocumentModel>
            {
                Items = documents,
                Page = page,
                PageSize = pageSize,
                TotalCount = (int)totalCount,
                TotalPages = (int)Math.Ceiling((double)totalCount / pageSize)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching documents");
            return new PagedResult<DocumentModel>
            {
                Items = new List<DocumentModel>(),
                Page = page,
                PageSize = pageSize,
                TotalCount = 0,
                TotalPages = 0
            };
        }
    }
}

/// <summary>
/// Resultado de una operación paginada.
/// </summary>
/// <typeparam name="T">Tipo de elementos en la página</typeparam>
public class PagedResult<T>
{
    /// <summary>
    /// Lista de elementos en esta página.
    /// </summary>
    public List<T> Items { get; set; } = new();

    /// <summary>
    /// Número de página actual (0-indexed).
    /// </summary>
    public int Page { get; set; }

    /// <summary>
    /// Tamaño de cada página.
    /// </summary>
    public int PageSize { get; set; }

    /// <summary>
    /// Total de elementos en todos los pages.
    /// </summary>
    public int TotalCount { get; set; }

    /// <summary>
    /// Total de páginas disponibles.
    /// </summary>
    public int TotalPages { get; set; }

    /// <summary>
    /// Indica si hay una página anterior.
    /// </summary>
    public bool HasPreviousPage => Page > 0;

    /// <summary>
    /// Indica si hay una página siguiente.
    /// </summary>
    public bool HasNextPage => Page < TotalPages - 1;
}

/// <summary>
/// Estadísticas de procesamiento de documentos.
/// </summary>
public class DocumentStatistics
{
    /// <summary>
    /// Total de documentos en el sistema.
    /// </summary>
    public int TotalDocuments { get; set; }

    /// <summary>
    /// Total de chunks generados.
    /// </summary>
    public int TotalChunks { get; set; }

    /// <summary>
    /// Tamaño total de archivos en bytes.
    /// </summary>
    public long TotalSizeBytes { get; set; }

    /// <summary>
    /// Total de documentos procesados con éxito.
    /// </summary>
    public int CompletedDocuments { get; set; }

    /// <summary>
    /// Total de documentos en proceso.
    /// </summary>
    public int ProcessingDocuments { get; set; }

    /// <summary>
    /// Total de documentos con error.
    /// </summary>
    public int FailedDocuments { get; set; }

    /// <summary>
    /// Conteo de documentos por estado.
    /// </summary>
    public Dictionary<string, int> StatusCounts { get; set; } = new();

    /// <summary>
    /// Lista de documentos más recientes (últimos 10).
    /// </summary>
    public List<DocumentModel> RecentDocuments { get; set; } = new();

    /// <summary>
    /// Lista de documentos con más chunks.
    /// </summary>
    public List<DocumentChunkCount> DocumentsWithMostChunks { get; set; } = new();
}

/// <summary>
/// Conteo de chunks por documento.
/// </summary>
public class DocumentChunkCount
{
    /// <summary>
    /// ID del documento.
    /// </summary>
    public string DocumentId { get; set; } = string.Empty;

    /// <summary>
    /// Cantidad de chunks.
    /// </summary>
    public int ChunkCount { get; set; }

    /// <summary>
    /// Nombre del archivo.
    /// </summary>
    public string FileName { get; set; } = string.Empty;
}
