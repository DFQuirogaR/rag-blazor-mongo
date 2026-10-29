using BlazorApp.Models;
using Microsoft.SemanticKernel.ChatCompletion;

namespace BlazorApp.Services;

/// <summary>
/// Orchestrator that coordinates DocumentProcessor, RagService, and chat UI components.
/// Implements unified error handling, consistent logging, and end-to-end flow management.
/// Handles RNF3.1 (modular and testable code) and RNF3.3 (structured logging) requirements.
/// </summary>
public class RagOrchestrator
{
    private readonly DocumentProcessor _documentProcessor;
    private readonly EnhancedRagService _ragService;
    private readonly ChatSessionService _chatSessionService;
    private readonly ResponseFormatter _responseFormatter;
    private readonly ILogger<RagOrchestrator> _logger;

    /// <summary>
    /// Initializes the orchestrator with all required services.
    /// </summary>
    /// <param name="documentProcessor">Service for document ingestion and processing.</param>
    /// <param name="ragService">Enhanced RAG service for query processing.</param>
    /// <param name="chatSessionService">Service for managing chat sessions.</param>
    /// <param name="responseFormatter">Service for formatting responses.</param>
    /// <param name="logger">Logger instance for consistent logging.</param>
    public RagOrchestrator(
        DocumentProcessor documentProcessor,
        EnhancedRagService ragService,
        ChatSessionService chatSessionService,
        ResponseFormatter responseFormatter,
        ILogger<RagOrchestrator> logger)
    {
        _documentProcessor = documentProcessor;
        _ragService = ragService;
        _chatSessionService = chatSessionService;
        _responseFormatter = responseFormatter;
        _logger = logger;
    }

    /// <summary>
    /// Processes a document from file path.
    /// </summary>
    /// <param name="filePath">Path to the document file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Processing result with success status and message.</returns>
    public async Task<DocumentProcessingResult> ProcessDocumentAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Starting document processing: {FilePath}", filePath);

            var document = await _documentProcessor.ProcessDocumentAsync(filePath, cancellationToken);

            if (document == null)
            {
                _logger.LogWarning("Document processing failed: {FilePath}", filePath);
                return new DocumentProcessingResult
                {
                    Success = false,
                    Message = $"No se pudo procesar el documento: {filePath}. Verifique que el archivo exista y tenga un formato soportado (Word o PDF)."
                };
            }

            if (document.ProcessingStatus == "completed")
            {
                _logger.LogInformation("Document processed successfully: {FilePath} (ID: {DocumentId})", filePath, document.Id);
                return new DocumentProcessingResult
                {
                    Success = true,
                    DocumentId = document.Id,
                    Message = $"Documento procesado correctamente: {document.FileName}. {document.ChunkCount} fragmentos generados."
                };
            }

            _logger.LogWarning("Document processing completed with errors: {FilePath}", filePath);
            return new DocumentProcessingResult
            {
                Success = false,
                DocumentId = document.Id,
                Message = $"El documento se procesó con errores. Revisa los logs para más detalles."
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing document: {FilePath}", filePath);
            return new DocumentProcessingResult
            {
                Success = false,
                Message = $"Error al procesar el documento: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Queries documents using the RAG pipeline.
    /// </summary>
    /// <param name="userQuery">User's question or query.</param>
    /// <param name="sessionId">Optional session ID for context.</param>
    /// <param name="maxResults">Maximum number of results to retrieve.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Query result with formatted response and metadata.</returns>
    public async Task<QueryResult> QueryDocumentsAsync(
        string userQuery,
        string? sessionId = null,
        int maxResults = 10,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Starting RAG query: {Query}", userQuery);

            if (string.IsNullOrWhiteSpace(userQuery))
            {
                _logger.LogWarning("Empty query received");
                return new QueryResult
                {
                    Success = false,
                    Message = "La consulta no puede estar vacía."
                };
            }

            // Generate query embedding and search for relevant chunks
            var queryResult = await _ragService.QueryAsync(userQuery, maxResults, rerank: true);

            // Log query metrics
            _logger.LogInformation("RAG query completed successfully: {Query} ({ResultLength} chars)", 
                userQuery, queryResult.Length);

            return new QueryResult
            {
                Success = true,
                Response = queryResult,
                Message = "Consulta procesada correctamente."
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing RAG query: {Query}", userQuery);
            return new QueryResult
            {
                Success = false,
                Message = $"Error al procesar la consulta: {ex.Message}. Por favor, intenta reformular tu pregunta."
            };
        }
    }

    /// <summary>
    /// Manages chat session state and interactions.
    /// </summary>
    /// <param name="userId">User identifier.</param>
    /// <param name="sessionId">Optional session ID. If null, creates a new session.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Session result with session details.</returns>
    public async Task<SessionResult> ManageSessionAsync(
        string userId,
        string? sessionId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                _logger.LogWarning("Invalid user ID provided");
                return new SessionResult
                {
                    Success = false,
                    Message = "El identificador de usuario no es válido."
                };
            }

            ChatSession? session;

            if (string.IsNullOrWhiteSpace(sessionId))
            {
                // Create new session
                _logger.LogInformation("Creating new session for user: {UserId}", userId);
                session = await _chatSessionService.CreateSessionAsync(userId: userId);
            }
            else
            {
                // Load existing session
                _logger.LogInformation("Loading session: {SessionId}", sessionId);
                session = await _chatSessionService.GetSessionAsync(sessionId);
            }

            if (session == null)
            {
                _logger.LogWarning("Session not found or could not be created");
                return new SessionResult
                {
                    Success = false,
                    Message = "No se pudo acceder a la sesión de chat."
                };
            }

            return new SessionResult
            {
                Success = true,
                SessionId = session.Id,
                Message = "Sesión gestionada correctamente.",
                CreatedAt = session.CreatedDate,
                MessageCount = session.Messages.Count
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error managing chat session");
            return new SessionResult
            {
                Success = false,
                Message = $"Error al gestionar la sesión: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Gets processing statistics for monitoring.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Processing statistics.</returns>
    public async Task<ProcessingStatistics> GetProcessingStatisticsAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Fetching processing statistics");

            var documents = await _documentProcessor.GetAllDocumentsAsync();
            var chunkCount = await _ragService.GetChunkCountWithEmbeddingsAsync();

            var completedDocs = documents.Count(d => d.ProcessingStatus == "completed");
            var failedDocs = documents.Count(d => d.ProcessingStatus == "failed");
            var pendingDocs = documents.Count(d => d.ProcessingStatus == "pending");

            _logger.LogInformation("Statistics - Total documents: {Total}, Completed: {Completed}, Failed: {Failed}, Pending: {Pending}",
                documents.Count, completedDocs, failedDocs, pendingDocs);

            return new ProcessingStatistics
            {
                TotalDocuments = documents.Count,
                CompletedDocuments = completedDocs,
                FailedDocuments = failedDocs,
                PendingDocuments = pendingDocs,
                TotalChunksWithEmbeddings = chunkCount,
                RetrievedAt = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching processing statistics");
            throw;
        }
    }

    /// <summary>
    /// Gets all documents with their processing status.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of documents with status.</returns>
    public async Task<List<DocumentStatusSummary>> GetDocumentsWithStatusAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Fetching all documents with status");

            var documents = await _documentProcessor.GetAllDocumentsAsync();

            var summaries = documents.Select(doc => new DocumentStatusSummary
            {
                DocumentId = doc.Id,
                FileName = doc.FileName,
                FilePath = doc.FilePath,
                FileType = doc.FileType,
                FileSize = doc.FileSize,
                ProcessingStatus = doc.ProcessingStatus,
                ChunkCount = doc.ChunkCount,
                UploadedAt = doc.UploadDate,
                ProcessedAt = doc.ProcessedAt
            }).ToList();

            _logger.LogInformation("Retrieved {Count} documents", summaries.Count);
            return summaries;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching documents with status");
            throw;
        }
    }

    /// <summary>
    /// Reprocesses a document (useful after updates or fixes).
    /// </summary>
    /// <param name="documentId">ID of the document to reprocess.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Reprocessing result.</returns>
    public async Task<DocumentProcessingResult> ReprocessDocumentAsync(
        string documentId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Starting document reprocessing: {DocumentId}", documentId);

            var result = await _documentProcessor.ReprocessDocumentAsync(documentId, cancellationToken);

            if (result == null)
            {
                _logger.LogWarning("Document not found for reprocessing: {DocumentId}", documentId);
                return new DocumentProcessingResult
                {
                    Success = false,
                    Message = $"No se encontró el documento con ID: {documentId}."
                };
            }

            if (result.ProcessingStatus == "completed")
            {
                _logger.LogInformation("Document reprocessed successfully: {DocumentId}", documentId);
                return new DocumentProcessingResult
                {
                    Success = true,
                    DocumentId = result.Id,
                    Message = $"Documento reprocesado correctamente: {result.FileName}."
                };
            }

            _logger.LogWarning("Document reprocessing completed with errors: {DocumentId}", documentId);
            return new DocumentProcessingResult
            {
                Success = false,
                DocumentId = result.Id,
                Message = $"El documento se reprocesó con errores."
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reprocessing document: {DocumentId}", documentId);
            return new DocumentProcessingResult
            {
                Success = false,
                Message = $"Error al reprocesar el documento: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Deletes a document and all its associated chunks.
    /// </summary>
    /// <param name="documentId">ID of the document to delete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Delete result.</returns>
    public async Task<DeleteResult> DeleteDocumentAsync(
        string documentId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Starting document deletion: {DocumentId}", documentId);

            var result = await _documentProcessor.DeleteDocumentAsync(documentId, cancellationToken);

            if (result)
            {
                _logger.LogInformation("Document deleted successfully: {DocumentId}", documentId);
                return new DeleteResult
                {
                    Success = true,
                    Message = $"Documento eliminado correctamente."
                };
            }

            _logger.LogWarning("Document not found or not deleted: {DocumentId}", documentId);
            return new DeleteResult
            {
                Success = false,
                Message = $"No se pudo eliminar el documento. Verifica que el ID sea correcto."
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting document: {DocumentId}", documentId);
            return new DeleteResult
            {
                Success = false,
                Message = $"Error al eliminar el documento: {ex.Message}"
            };
        }
    }
}

/// <summary>
/// Result of document processing operation.
/// </summary>
public class DocumentProcessingResult
{
    /// <summary>
    /// Indicates if processing was successful.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// The document ID if processing created or updated a document.
    /// </summary>
    public string? DocumentId { get; set; }

    /// <summary>
    /// Human-readable message describing the result.
    /// </summary>
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// Result of RAG query operation.
/// </summary>
public class QueryResult
{
    /// <summary>
    /// Indicates if query was processed successfully.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// The formatted response from the RAG system.
    /// </summary>
    public string? Response { get; set; }

    /// <summary>
    /// Human-readable message describing the result.
    /// </summary>
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// Result of chat session management operation.
/// </summary>
public class SessionResult
{
    /// <summary>
    /// Indicates if session management was successful.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// The session ID if successful.
    /// </summary>
    public string? SessionId { get; set; }

    /// <summary>
    /// When the session was created.
    /// </summary>
    public DateTime? CreatedAt { get; set; }

    /// <summary>
    /// Number of messages in the session.
    /// </summary>
    public int MessageCount { get; set; }

    /// <summary>
    /// Human-readable message describing the result.
    /// </summary>
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// Summary of document status for display.
/// </summary>
public class DocumentStatusSummary
{
    /// <summary>
    /// Document ID.
    /// </summary>
    public string? DocumentId { get; set; }

    /// <summary>
    /// Document file name.
    /// </summary>
    public string? FileName { get; set; }

    /// <summary>
    /// Document file path.
    /// </summary>
    public string? FilePath { get; set; }

    /// <summary>
    /// Document file type (e.g., "docx", "pdf").
    /// </summary>
    public string? FileType { get; set; }

    /// <summary>
    /// Document file size in bytes.
    /// </summary>
    public long? FileSize { get; set; }

    /// <summary>
    /// Current processing status.
    /// </summary>
    public string? ProcessingStatus { get; set; }

    /// <summary>
    /// Number of chunks generated for this document.
    /// </summary>
    public int ChunkCount { get; set; }

    /// <summary>
    /// When the document was uploaded.
    /// </summary>
    public DateTime? UploadedAt { get; set; }

    /// <summary>
    /// When the document was processed.
    /// </summary>
    public DateTime? ProcessedAt { get; set; }
}

/// <summary>
/// Processing statistics for monitoring.
/// </summary>
public class ProcessingStatistics
{
    /// <summary>
    /// Total number of documents.
    /// </summary>
    public int TotalDocuments { get; set; }

    /// <summary>
    /// Number of successfully processed documents.
    /// </summary>
    public int CompletedDocuments { get; set; }

    /// <summary>
    /// Number of documents with processing errors.
    /// </summary>
    public int FailedDocuments { get; set; }

    /// <summary>
    /// Number of documents pending processing.
    /// </summary>
    public int PendingDocuments { get; set; }

    /// <summary>
    /// Total number of chunks with embeddings.
    /// </summary>
    public int TotalChunksWithEmbeddings { get; set; }

    /// <summary>
    /// When statistics were retrieved.
    /// </summary>
    public DateTime RetrievedAt { get; set; }
}

/// <summary>
/// Result of document deletion operation.
/// </summary>
public class DeleteResult
{
    /// <summary>
    /// Indicates if deletion was successful.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Human-readable message describing the result.
    /// </summary>
    public string Message { get; set; } = string.Empty;
}
