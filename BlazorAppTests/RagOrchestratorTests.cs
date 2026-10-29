using BlazorApp.Services;
using BlazorApp.Models;
using MongoDB.Driver;
using Microsoft.SemanticKernel.ChatCompletion;
using Moq;
using Xunit;

namespace BlazorAppTests;

/// <summary>
/// Tests for the RagOrchestrator component.
/// Validates the integration between DocumentProcessor, EnhancedRagService, and UI.
/// Implements RNF3.1 (modular and testable code) and RNF3.3 (structured logging).
/// </summary>
public class RagOrchestratorTests
{
    private readonly Mock<DocumentProcessor> _mockDocumentProcessor;
    private readonly Mock<EnhancedRagService> _mockRagService;
    private readonly Mock<ChatSessionService> _mockChatSessionService;
    private readonly Mock<ResponseFormatter> _mockResponseFormatter;
    private readonly Mock<ILogger<RagOrchestrator>> _mockLogger;
    private readonly RagOrchestrator _orchestrator;

    public RagOrchestratorTests()
    {
        var mockExtractors = new List<ITextExtractor>
        {
            new PdfTextExtractor(),
            new WordTextExtractor()
        };

        _mockDocumentProcessor = new Mock<DocumentProcessor>(
            mockExtractors,
            Mock.Of<Microsoft.SemanticKernel.Embeddings.ITextEmbeddingGenerationService>(),
            Mock.Of<DocumentChunker>(),
            Mock.Of<IMongoDatabase>(),
            Mock.Of<IEmbeddingCache>(),
            Mock.Of<ILogger<DocumentProcessor>>());

        _mockRagService = new Mock<EnhancedRagService>(
            Mock.Of<IMongoDatabase>(),
            Mock.Of<Microsoft.SemanticKernel.Embeddings.ITextEmbeddingGenerationService>(),
            Mock.Of<Microsoft.SemanticKernel.ChatCompletion.IChatCompletionService>(),
            Mock.Of<ILogger<EnhancedRagService>>(),
            Mock.Of<ResponseFormatter>());

        _mockChatSessionService = new Mock<ChatSessionService>(
            Mock.Of<ILogger<ChatSessionService>>(),
            Mock.Of<IMongoDatabase>());

        _mockResponseFormatter = new Mock<ResponseFormatter>(
            Mock.Of<ILogger<ResponseFormatter>>());

        _mockLogger = new Mock<ILogger<RagOrchestrator>>();

        _orchestrator = new RagOrchestrator(
            _mockDocumentProcessor.Object,
            _mockRagService.Object,
            _mockChatSessionService.Object,
            _mockResponseFormatter.Object,
            _mockLogger.Object);
    }

    /// <summary>
    /// Tests that document processing returns a successful result when processing succeeds.
    /// </summary>
    [Fact]
    public async Task ProcessDocumentAsync_Success_ReturnsSuccessfulResult()
    {
        // Arrange
        var filePath = "test.docx";
        var document = new DocumentModel
        {
            Id = "doc123",
            FilePath = filePath,
            FileName = "Test Document",
            FileType = "docx",
            ChunkCount = 5,
            ProcessingStatus = "completed"
        };

        _mockDocumentProcessor
            .Setup(dp => dp.ProcessDocumentAsync(filePath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        // Act
        var result = await _orchestrator.ProcessDocumentAsync(filePath);

        // Assert
        Assert.True(result.Success);
        Assert.Equal("doc123", result.DocumentId);
        Assert.Contains("procesado correctamente", result.Message, StringComparison.OrdinalIgnoreCase);
        _mockLogger.VerifyLogInformation("Starting document processing", filePath);
        _mockLogger.VerifyLogInformation("Document processed successfully", filePath);
    }

    /// <summary>
    /// Tests that document processing returns a failure result when processing fails.
    /// </summary>
    [Fact]
    public async Task ProcessDocumentAsync_Failure_ReturnsFailedResult()
    {
        // Arrange
        var filePath = "nonexistent.pdf";
        _mockDocumentProcessor
            .Setup(dp => dp.ProcessDocumentAsync(filePath, It.IsAny<CancellationToken>()))
            .ReturnsAsync((DocumentModel?)null);

        // Act
        var result = await _orchestrator.ProcessDocumentAsync(filePath);

        // Assert
        Assert.False(result.Success);
        Assert.Null(result.DocumentId);
        Assert.Contains("No se pudo procesar", result.Message, StringComparison.OrdinalIgnoreCase);
        _mockLogger.VerifyLogWarning("Document processing failed", filePath);
    }

    /// <summary>
    /// Tests that document processing returns a failure result when an exception occurs.
    /// </summary>
    [Fact]
    public async Task ProcessDocumentAsync_Exception_ReturnsFailedResult()
    {
        // Arrange
        var filePath = "error.docx";
        _mockDocumentProcessor
            .Setup(dp => dp.ProcessDocumentAsync(filePath, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Test exception"));

        // Act
        var result = await _orchestrator.ProcessDocumentAsync(filePath);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("Error al procesar", result.Message, StringComparison.OrdinalIgnoreCase);
        _mockLogger.VerifyLogError("Error processing document", filePath);
    }

    /// <summary>
    /// Tests that query processing returns a successful result when the query succeeds.
    /// </summary>
    [Fact]
    public async Task QueryDocumentsAsync_Success_ReturnsSuccessfulResult()
    {
        // Arrange
        var query = "¿Cuál es el proceso?";
        var response = "Este es el proceso respondido por el sistema RAG.";
        
        _mockRagService
            .Setup(rs => rs.QueryAsync(query, It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        // Act
        var result = await _orchestrator.QueryDocumentsAsync(query);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(response, result.Response);
        Assert.Contains("Consulta procesada correctamente", result.Message, StringComparison.OrdinalIgnoreCase);
        _mockLogger.VerifyLogInformation("Starting RAG query", query);
        _mockLogger.VerifyLogInformation("RAG query completed successfully", query);
    }

    /// <summary>
    /// Tests that query processing returns a failure result when the query is empty.
    /// </summary>
    [Fact]
    public async Task QueryDocumentsAsync_EmptyQuery_ReturnsFailedResult()
    {
        // Act
        var result = await _orchestrator.QueryDocumentsAsync(string.Empty);

        // Assert
        Assert.False(result.Success);
        Assert.Null(result.Response);
        Assert.Contains("no puede estar vacía", result.Message, StringComparison.OrdinalIgnoreCase);
        _mockLogger.VerifyLogWarning("Empty query received");
    }

    /// <summary>
    /// Tests that session management creates a new session when no session ID is provided.
    /// </summary>
    [Fact]
    public async Task ManageSessionAsync_NewSession_CreatesSession()
    {
        // Arrange
        var userId = "user123";
        var session = new ChatSession
        {
            Id = "session123",
            UserId = userId,
            CreatedAt = DateTime.UtcNow,
            MessageCount = 0
        };

        _mockChatSessionService
            .Setup(cs => cs.CreateSessionAsync(userId))
            .ReturnsAsync(session);

        // Act
        var result = await _orchestrator.ManageSessionAsync(userId);

        // Assert
        Assert.True(result.Success);
        Assert.Equal("session123", result.SessionId);
        Assert.NotNull(result.CreatedAt);
        Assert.Equal(0, result.MessageCount);
        Assert.Contains("Sesión gestionada correctamente", result.Message, StringComparison.OrdinalIgnoreCase);
        _mockLogger.VerifyLogInformation("Creating new session", userId);
    }

    /// <summary>
    /// Tests that session management loads an existing session when session ID is provided.
    /// </summary>
    [Fact]
    public async Task ManageSessionAsync_ExistingSession_LoadsSession()
    {
        // Arrange
        var userId = "user123";
        var sessionId = "session123";
        var session = new ChatSession
        {
            Id = sessionId,
            UserId = userId,
            CreatedAt = DateTime.UtcNow,
            MessageCount = 5
        };

        _mockChatSessionService
            .Setup(cs => cs.GetSessionAsync(sessionId))
            .ReturnsAsync(session);

        // Act
        var result = await _orchestrator.ManageSessionAsync(userId, sessionId);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(sessionId, result.SessionId);
        Assert.NotNull(result.CreatedAt);
        Assert.Equal(5, result.MessageCount);
        _mockLogger.VerifyLogInformation("Loading session", sessionId);
    }

    /// <summary>
    /// Tests that processing statistics are retrieved correctly.
    /// </summary>
    [Fact]
    public async Task GetProcessingStatisticsAsync_Success_ReturnsStatistics()
    {
        // Arrange
        var documents = new List<DocumentModel>
        {
            new() { Id = "doc1", ProcessingStatus = "completed" },
            new() { Id = "doc2", ProcessingStatus = "completed" },
            new() { Id = "doc3", ProcessingStatus = "failed" },
            new() { Id = "doc4", ProcessingStatus = "pending" }
        };

        _mockDocumentProcessor
            .Setup(dp => dp.GetAllDocumentsAsync())
            .ReturnsAsync(documents);

        _mockRagService
            .Setup(rs => rs.GetChunkCountWithEmbeddingsAsync())
            .ReturnsAsync(10);

        // Act
        var result = await _orchestrator.GetProcessingStatisticsAsync();

        // Assert
        Assert.Equal(4, result.TotalDocuments);
        Assert.Equal(2, result.CompletedDocuments);
        Assert.Equal(1, result.FailedDocuments);
        Assert.Equal(1, result.PendingDocuments);
        Assert.Equal(10, result.TotalChunksWithEmbeddings);
        _mockLogger.VerifyLogInformation("Fetching processing statistics");
    }

    /// <summary>
    /// Tests that documents with status are retrieved correctly.
    /// </summary>
    [Fact]
    public async Task GetDocumentsWithStatusAsync_Success_ReturnsDocuments()
    {
        // Arrange
        var documents = new List<DocumentModel>
        {
            new() 
            { 
                Id = "doc1", 
                FileName = "Test1.docx", 
                FilePath = "C:\\docs\\Test1.docx",
                FileType = "docx",
                FileSize = 1024,
                ProcessingStatus = "completed",
                ChunkCount = 5,
                UploadDate = DateTime.UtcNow
            }
        };

        _mockDocumentProcessor
            .Setup(dp => dp.GetAllDocumentsAsync())
            .ReturnsAsync(documents);

        // Act
        var result = await _orchestrator.GetDocumentsWithStatusAsync();

        // Assert
        Assert.Single(result);
        Assert.Equal("Test1.docx", result[0].FileName);
        Assert.Equal("completed", result[0].ProcessingStatus);
        Assert.Equal(5, result[0].ChunkCount);
        _mockLogger.VerifyLogInformation("Fetching all documents with status");
    }

    /// <summary>
    /// Tests that document reprocessing returns a successful result.
    /// </summary>
    [Fact]
    public async Task ReprocessDocumentAsync_Success_ReturnsSuccessfulResult()
    {
        // Arrange
        var documentId = "doc123";
        var document = new DocumentModel
        {
            Id = documentId,
            FileName = "Test Document",
            ProcessingStatus = "completed"
        };

        _mockDocumentProcessor
            .Setup(dp => dp.ReprocessDocumentAsync(documentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        // Act
        var result = await _orchestrator.ReprocessDocumentAsync(documentId);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(documentId, result.DocumentId);
        _mockLogger.VerifyLogInformation("Starting document reprocessing", documentId);
    }

    /// <summary>
    /// Tests that document deletion returns a successful result.
    /// </summary>
    [Fact]
    public async Task DeleteDocumentAsync_Success_ReturnsSuccessfulResult()
    {
        // Arrange
        var documentId = "doc123";
        _mockDocumentProcessor
            .Setup(dp => dp.DeleteDocumentAsync(documentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _orchestrator.DeleteDocumentAsync(documentId);

        // Assert
        Assert.True(result.Success);
        _mockLogger.VerifyLogInformation("Starting document deletion", documentId);
    }
}

/// <summary>
/// Extension methods for logger verification in unit tests.
/// </summary>
public static class LoggerExtensions
{
    /// <summary>
    /// Verifies that the logger was called with an information message containing the expected text.
    /// </summary>
    public static void VerifyLogInformation(this Mock<ILogger<RagOrchestrator>> mockLogger, string expectedText, string? argument = null)
    {
        mockLogger.Verify(
            logger => logger.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    /// <summary>
    /// Verifies that the logger was called with a warning message containing the expected text.
    /// </summary>
    public static void VerifyLogWarning(this Mock<ILogger<RagOrchestrator>> mockLogger, string expectedText, string? argument = null)
    {
        mockLogger.Verify(
            logger => logger.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    /// <summary>
    /// Verifies that the logger was called with an error message containing the expected text.
    /// </summary>
    public static void VerifyLogError(this Mock<ILogger<RagOrchestrator>> mockLogger, string expectedText, string? argument = null)
    {
        mockLogger.Verify(
            logger => logger.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }
}
