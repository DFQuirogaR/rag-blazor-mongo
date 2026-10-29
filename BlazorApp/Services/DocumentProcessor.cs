using BlazorApp.Models;
using MongoDB.Driver;
using Microsoft.SemanticKernel.Embeddings;
using System.Security.Cryptography;
using System.Text;

namespace BlazorApp.Services;

/// <summary>
/// Processes documents: extraction, chunking, embedding, and storage
/// </summary>
public class DocumentProcessor
{
    private readonly IEnumerable<ITextExtractor> _extractors;
    private readonly ITextEmbeddingGenerationService _embeddingService;
    private readonly DocumentChunker _chunker;
    private readonly IMongoDatabase _database;
    private readonly IEmbeddingCache _embeddingCache;
    private readonly ILogger<DocumentProcessor> _logger;

    private IMongoCollection<DocumentModel> DocumentsCollection =>
        _database.GetCollection<DocumentModel>("documents");

    private IMongoCollection<ChunkModel> ChunksCollection =>
        _database.GetCollection<ChunkModel>("chunks");

    public DocumentProcessor(
        IEnumerable<ITextExtractor> extractors,
        ITextEmbeddingGenerationService embeddingService,
        DocumentChunker chunker,
        IMongoDatabase database,
        IEmbeddingCache embeddingCache,
        ILogger<DocumentProcessor> logger)
    {
        _extractors = extractors;
        _embeddingService = embeddingService;
        _chunker = chunker;
        _database = database;
        _embeddingCache = embeddingCache;
        _logger = logger;
    }

    /// <summary>
    /// Processes a document from extraction to storage
    /// </summary>
    /// <param name="filePath">Path to the document file</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The processed document model, or null if processing failed</returns>
    public async Task<DocumentModel?> ProcessDocumentAsync(string filePath, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                _logger.LogError("File not found: {FilePath}", filePath);
                return null;
            }

            var fileInfo = new FileInfo(filePath);
            var extension = fileInfo.Extension;

            // Find appropriate extractor
            var extractor = _extractors.FirstOrDefault(e => e.SupportsFileType(extension));
            if (extractor == null)
            {
                _logger.LogWarning("No extractor found for file type: {Extension}", extension);
                return null;
            }

            _logger.LogInformation("Processing document: {FilePath}", filePath);

            // Calculate hash to detect duplicates and changes
            var contentHash = CalculateFileHash(filePath);

            // Load the existing record by path. ProcessingStatus is serialized as
            // the enum name ("Completed"), not the lowercase legacy value.
            var existingDocument = await DocumentsCollection
                .Find(d => d.FilePath == filePath)
                .FirstOrDefaultAsync(cancellationToken);

            if (existingDocument != null &&
                existingDocument.ContentHash == contentHash &&
                string.Equals(
                    existingDocument.ProcessingStatus,
                    DocumentModel.DocumentStatus.Completed.ToString(),
                    StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation("Document already processed with same content: {FilePath}", filePath);
                return existingDocument;
            }

            // Extract text
            string extractedText = await extractor.ExtractTextAsync(filePath);
            if (string.IsNullOrWhiteSpace(extractedText))
            {
                _logger.LogWarning("No text extracted from: {FilePath}", filePath);
                return null;
            }

            // Reuse the existing document ID when retrying a failed or changed file.
            // This prevents duplicate FilePath records and lets retries replace chunks.
            if (existingDocument != null)
            {
                _logger.LogInformation("Re-processing existing document: {FilePath}", filePath);
                await _embeddingCache.InvalidateCacheForDocumentAsync(existingDocument.Id!, cancellationToken);
                await ChunksCollection.DeleteManyAsync(c => c.DocumentId == existingDocument.Id, cancellationToken);
            }

            var document = existingDocument ?? DocumentModel.Create(
                filePath: filePath,
                fileName: fileInfo.Name,
                fileType: extension.TrimStart('.'),
                fileSize: fileInfo.Length,
                fileHash: contentHash
            );

            document.FilePath = filePath;
            document.FileName = fileInfo.Name;
            document.FileType = extension.TrimStart('.');
            document.FileSize = fileInfo.Length;
            document.ContentHash = contentHash;
            document.ProcessingStatus = DocumentModel.DocumentStatus.Processing.ToString();

            // Persist the document before creating chunks so its generated ObjectId is
            // available to ChunkModel.DocumentId. The old upsert-based write inserted
            // only the update fields, which violated the MongoDB validator and also
            // generated an ID different from document.Id.
            if (existingDocument != null)
            {
                await DocumentsCollection.ReplaceOneAsync(
                    d => d.Id == document.Id,
                    document,
                    cancellationToken: cancellationToken);
            }
            else
            {
                await DocumentsCollection.InsertOneAsync(document, cancellationToken);
            }

            _logger.LogInformation("Document saved to DB with ID: {DocumentId}", document.Id);

            // Use DocumentChunker for intelligent chunking
            var chunks = _chunker.ChunkDocument(extractedText, document.Id!, chunkSize: 1000, overlapSize: 200);
            _logger.LogInformation("Created {ChunkCount} chunks for document: {FilePath}", 
                chunks.Count, filePath);

            // Generate embeddings for each chunk and save
            int processedChunks = 0;
            int failedChunks = 0;
            
            foreach (var chunk in chunks)
            {
                try
                {
                    // Generate embedding for the chunk content
                    var embedding = await _embeddingService.GenerateEmbeddingAsync(chunk.ChunkText);
                    chunk.Embedding = embedding.ToArray();
                    chunk.CreatedAt = DateTime.UtcNow;
                    
                    await ChunksCollection.InsertOneAsync(chunk);
                    processedChunks++;
                    
                    _logger.LogDebug("Generated embedding for chunk {ChunkIndex}", chunk.ChunkIndex);
                }
                catch (Exception ex)
                {
                    failedChunks++;
                    _logger.LogError(ex, "Error generating embedding for chunk {ChunkIndex}", chunk.ChunkIndex);
                }
            }

            // Update document status
            document.ProcessingStatus = failedChunks == 0 
                ? DocumentModel.DocumentStatus.Completed.ToString() 
                : DocumentModel.DocumentStatus.Failed.ToString();
            
            document.ProcessedAt = DateTime.UtcNow;
            document.ChunkCount = chunks.Count;

            await DocumentsCollection.ReplaceOneAsync(d => d.Id == document.Id, document);

            if (failedChunks > 0)
            {
                _logger.LogError("Processed document: {FilePath} with {ProcessedChunks} successful chunks, {FailedChunks} failed", 
                    filePath, processedChunks, failedChunks);
            }
            else
            {
                _logger.LogInformation("Successfully processed document: {FilePath} with {ProcessedChunks} chunks", 
                    filePath, processedChunks);
            }

            return document;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing document: {FilePath}", filePath);
            return null;
        }
    }

    /// <summary>
    /// Calculates SHA256 hash of a file for deduplication
    /// </summary>
    private string CalculateFileHash(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(stream);
        return Convert.ToHexString(hash);
    }

    /// <summary>
    /// Gets all documents in the database
    /// </summary>
    public async Task<List<DocumentModel>> GetAllDocumentsAsync()
    {
        return await DocumentsCollection.Find(_ => true).ToListAsync();
    }

    /// <summary>
    /// Gets a document by its ID
    /// </summary>
    public async Task<DocumentModel?> GetDocumentByIdAsync(string documentId)
    {
        return await DocumentsCollection.Find(d => d.Id == documentId).FirstOrDefaultAsync();
    }

    /// <summary>
    /// Gets chunks for a specific document
    /// </summary>
    public async Task<List<ChunkModel>> GetDocumentChunksAsync(string documentId)
    {
        return await ChunksCollection.Find(c => c.DocumentId == documentId)
            .SortBy(c => c.ChunkIndex)
            .ToListAsync();
    }

    /// <summary>
    /// Deletes a document and all its chunks
    /// </summary>
    public async Task<bool> DeleteDocumentAsync(string documentId, CancellationToken cancellationToken = default)
    {
        try
        {
            // Invalidate cache for the document
            await _embeddingCache.InvalidateCacheForDocumentAsync(documentId, cancellationToken);
            
            // Delete all chunks
            await ChunksCollection.DeleteManyAsync(c => c.DocumentId == documentId);
            
            // Delete document
            var result = await DocumentsCollection.DeleteOneAsync(d => d.Id == documentId);
            
            if (result.DeletedCount > 0)
            {
                _logger.LogInformation("Deleted document {DocumentId} and invalidated cache", documentId);
            }
            
            return result.DeletedCount > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting document: {DocumentId}", documentId);
            return false;
        }
    }

    /// <summary>
    /// Re-processes a document (useful after updates)
    /// </summary>
    public async Task<DocumentModel?> ReprocessDocumentAsync(string documentId, CancellationToken cancellationToken = default)
    {
        var document = await GetDocumentByIdAsync(documentId);
        if (document == null)
        {
            _logger.LogWarning("Document not found for reprocessing: {DocumentId}", documentId);
            return null;
        }

        // Invalidate cache for the document
        await _embeddingCache.InvalidateCacheForDocumentAsync(documentId, cancellationToken);

        // Delete existing chunks
        await ChunksCollection.DeleteManyAsync(c => c.DocumentId == documentId);

        // Re-process the document
        return await ProcessDocumentAsync(document.FilePath);
    }

    /// <summary>
    /// Gets documents with a specific processing status
    /// </summary>
    public async Task<List<DocumentModel>> GetDocumentsByStatusAsync(string status)
    {
        return await DocumentsCollection.Find(d => d.ProcessingStatus == status).ToListAsync();
    }
}
