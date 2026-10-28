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
    private readonly IMongoDatabase _database;
    private readonly ILogger<DocumentProcessor> _logger;

    private IMongoCollection<DocumentModel> DocumentsCollection =>
        _database.GetCollection<DocumentModel>("documents");

    private IMongoCollection<ChunkModel> ChunksCollection =>
        _database.GetCollection<ChunkModel>("chunks");

    public DocumentProcessor(
        IEnumerable<ITextExtractor> extractors,
        ITextEmbeddingGenerationService embeddingService,
        IMongoDatabase database,
        ILogger<DocumentProcessor> logger)
    {
        _extractors = extractors;
        _embeddingService = embeddingService;
        _database = database;
        _logger = logger;
    }

    public async Task<DocumentModel?> ProcessDocumentAsync(string filePath)
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

            // Calculate hash to detect changes
            var contentHash = CalculateFileHash(filePath);

            // Check if document already processed
            var existingDoc = await DocumentsCollection
                .Find(d => d.FilePath == filePath && d.ContentHash == contentHash)
                .FirstOrDefaultAsync();

            if (existingDoc != null)
            {
                _logger.LogInformation("Document already processed: {FilePath}", filePath);
                return existingDoc;
            }

            // Extract text
            string extractedText = await extractor.ExtractTextAsync(filePath);
            if (string.IsNullOrWhiteSpace(extractedText))
            {
                _logger.LogWarning("No text extracted from: {FilePath}", filePath);
                return null;
            }

            // Create document model
            var document = new DocumentModel
            {
                FilePath = filePath,
                FileName = fileInfo.Name,
                FileType = extension.TrimStart('.'),
                FileSize = fileInfo.Length,
                ContentHash = contentHash,
                ProcessedAt = DateTime.UtcNow,
                ProcessingStatus = "processing"
            };

            // Save document
            await DocumentsCollection.InsertOneAsync(document);
            _logger.LogInformation("Document saved to DB with ID: {DocumentId}", document.Id);

            // Chunk the document
            var chunks = ChunkDocument(extractedText, document.Id!);
            _logger.LogInformation("Created {ChunkCount} chunks for document: {FilePath}", 
                chunks.Count, filePath);

            // Generate embeddings and save chunks
            int processedChunks = 0;
            foreach (var chunk in chunks)
            {
                try
                {
                    var embedding = await _embeddingService.GenerateEmbeddingAsync(chunk.Content);
                    chunk.Embedding = embedding.ToArray();
                    await ChunksCollection.InsertOneAsync(chunk);
                    processedChunks++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error generating embedding for chunk {ChunkIndex}", chunk.ChunkIndex);
                }
            }

            // Update document status
            document.ProcessingStatus = "completed";
            await DocumentsCollection.ReplaceOneAsync(d => d.Id == document.Id, document);

            _logger.LogInformation("Successfully processed document: {FilePath} with {ProcessedChunks} chunks", 
                filePath, processedChunks);

            return document;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing document: {FilePath}", filePath);
            return null;
        }
    }

    private List<ChunkModel> ChunkDocument(string content, string documentId, int chunkSize = 1000, int overlap = 200)
    {
        var chunks = new List<ChunkModel>();
        var paragraphs = content.Split(["\n\n", "\r\n\r\n"], StringSplitOptions.RemoveEmptyEntries);

        var currentChunk = new StringBuilder();
        int chunkIndex = 0;
        int startChar = 0;

        foreach (var paragraph in paragraphs)
        {
            var trimmedParagraph = paragraph.Trim();
            if (string.IsNullOrEmpty(trimmedParagraph))
                continue;

            // If adding this paragraph exceeds chunk size, save current chunk
            if (currentChunk.Length + trimmedParagraph.Length > chunkSize && currentChunk.Length > 0)
            {
                var chunkText = currentChunk.ToString().Trim();
                chunks.Add(new ChunkModel
                {
                    DocumentId = documentId,
                    ChunkIndex = chunkIndex++,
                    StartChar = startChar,
                    EndChar = startChar + chunkText.Length,
                    Content = chunkText,
                    CreatedAt = DateTime.UtcNow
                });

                // Keep overlap
                var words = chunkText.Split(' ');
                var overlapWords = words.Length > 30 ? words.TakeLast(30) : words;
                currentChunk.Clear();
                currentChunk.Append(string.Join(" ", overlapWords));
                currentChunk.Append(" ");

                startChar += chunkText.Length - currentChunk.Length;
            }

            currentChunk.AppendLine(trimmedParagraph);
        }

        // Add remaining content
        if (currentChunk.Length > 0)
        {
            var chunkText = currentChunk.ToString().Trim();
            chunks.Add(new ChunkModel
            {
                DocumentId = documentId,
                ChunkIndex = chunkIndex,
                StartChar = startChar,
                EndChar = startChar + chunkText.Length,
                Content = chunkText,
                CreatedAt = DateTime.UtcNow
            });
        }

        return chunks;
    }

    private string CalculateFileHash(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(stream);
        return Convert.ToHexString(hash);
    }

    public async Task<List<DocumentModel>> GetAllDocumentsAsync()
    {
        return await DocumentsCollection.Find(_ => true).ToListAsync();
    }

    public async Task<bool> DeleteDocumentAsync(string documentId)
    {
        try
        {
            // Delete all chunks
            await ChunksCollection.DeleteManyAsync(c => c.DocumentId == documentId);
            
            // Delete document
            var result = await DocumentsCollection.DeleteOneAsync(d => d.Id == documentId);
            
            return result.DeletedCount > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting document: {DocumentId}", documentId);
            return false;
        }
    }
}
