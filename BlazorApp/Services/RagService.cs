using BlazorApp.Models;
using MongoDB.Driver;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Embeddings;
using System.Diagnostics;
using System.Text;

namespace BlazorApp.Services;

/// <summary>RAG service with a typed request/response contract.</summary>
public class RagService
{
    private readonly IMongoDatabase _database;
    private readonly ITextEmbeddingGenerationService _embeddingService;
    private readonly IChatCompletionService _chatService;
    private readonly ILogger<RagService> _logger;
    private readonly string? _modelName;

    private IMongoCollection<ChunkModel> ChunksCollection => _database.GetCollection<ChunkModel>("chunks");
    private IMongoCollection<DocumentModel> DocumentsCollection => _database.GetCollection<DocumentModel>("documents");

    public RagService(
        IMongoDatabase database,
        ITextEmbeddingGenerationService embeddingService,
        IChatCompletionService chatService,
        ILogger<RagService> logger)
    {
        _database = database;
        _embeddingService = embeddingService;
        _chatService = chatService;
        _logger = logger;
    }

    /// <summary>Executes retrieval and generation without embedding sources in Markdown.</summary>
    public async Task<RagResponse> QueryAsync(RagQueryRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var query = request.Query.Trim();
        if (query.Length is < 1 or > 2000)
        {
            return new RagResponse { Status = RagResultStatus.Failed, FailureCode = "invalid-query" };
        }

        var stopwatch = Stopwatch.StartNew();
        var retrievedAt = DateTime.UtcNow;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var queryEmbedding = await _embeddingService.GenerateEmbeddingAsync(query);
            cancellationToken.ThrowIfCancellationRequested();
            var candidates = await SearchSimilarChunksAsync(queryEmbedding.ToArray(), request.MaxResults, cancellationToken);
            var sources = await BuildSourcesAsync(candidates, retrievedAt, cancellationToken);
            var metadata = new RagModelMetadata
            {
                ModelName = _modelName,
                ContextChunksUsed = candidates.Count,
                RetrievedAtUtc = retrievedAt,
                ProcessingTimeMs = stopwatch.ElapsedMilliseconds
            };

            if (candidates.Count == 0)
            {
                return new RagResponse
                {
                    Status = RagResultStatus.NoResults,
                    AnswerText = "Lo siento, no encontré información relevante en los documentos procesados para responder a tu pregunta.",
                    Sources = sources,
                    ModelMetadata = metadata
                };
            }

            var context = await BuildContextAsync(candidates, cancellationToken);
            var answer = await GenerateResponseAsync(query, context, cancellationToken);
            return new RagResponse
            {
                Status = RagResultStatus.Succeeded,
                AnswerText = answer,
                Sources = sources,
                ModelMetadata = new RagModelMetadata
                {
                    ModelName = metadata.ModelName,
                    ContextChunksUsed = metadata.ContextChunksUsed,
                    RetrievedAtUtc = metadata.RetrievedAtUtc,
                    ProcessingTimeMs = stopwatch.ElapsedMilliseconds
                }
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return new RagResponse { Status = RagResultStatus.Cancelled, FailureCode = "cancelled" };
        }
        catch (TimeoutException)
        {
            return new RagResponse { Status = RagResultStatus.TimedOut, FailureCode = "timeout" };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing RAG query with correlation {CorrelationId}", request.CorrelationId);
            return new RagResponse { Status = RagResultStatus.Failed, FailureCode = "rag-failure" };
        }
    }

    /// <summary>
    /// Temporary string adapter for consumers that have not migrated to RagQueryRequest/RagResponse.
    /// New code should use the typed overload.
    /// </summary>
    public async Task<string> QueryAsync(string userQuery, int maxResults = 5, CancellationToken cancellationToken = default)
    {
        var response = await QueryAsync(new RagQueryRequest
        {
            Query = userQuery,
            MaxResults = maxResults
        }, cancellationToken);

        if (response.Status is RagResultStatus.Failed or RagResultStatus.Cancelled or RagResultStatus.TimedOut)
        {
            return response.FailureCode switch
            {
                "cancelled" => "La consulta fue cancelada.",
                "timeout" => "La consulta agotó el tiempo de espera.",
                "invalid-query" => "La consulta no es válida.",
                _ => "No se pudo completar la consulta."
            };
        }

        return response.AnswerText;
    }

    private async Task<List<RetrievedChunk>> SearchSimilarChunksAsync(float[] queryVector, int limit, CancellationToken cancellationToken)
    {
        var allChunks = await ChunksCollection.Find(_ => true).ToListAsync(cancellationToken);
        return allChunks
            .Where(chunk => chunk.Embedding is { Length: > 0 })
            .Select(chunk => new RetrievedChunk(chunk, CalculateCosineSimilarity(queryVector, chunk.Embedding!)))
            .OrderByDescending(result => result.Relevance)
            .Take(Math.Clamp(limit, 1, 50))
            .ToList();
    }

    private async Task<List<SourceReference>> BuildSourcesAsync(
        IReadOnlyList<RetrievedChunk> retrieved,
        DateTime retrievedAtUtc,
        CancellationToken cancellationToken)
    {
        var documentIds = retrieved.Select(item => item.Chunk.DocumentId).Distinct().ToList();
        var documents = await DocumentsCollection.Find(documentIds.Count == 0
            ? Builders<DocumentModel>.Filter.Empty
            : Builders<DocumentModel>.Filter.In(document => document.Id, documentIds)).ToListAsync(cancellationToken);
        var names = documents.ToDictionary(document => document.Id!, document =>
            string.IsNullOrWhiteSpace(document.DocumentMetadata?.Title)
                ? document.FileName
                : document.DocumentMetadata.Title!.Trim());

        return retrieved.Select(item => new SourceReference
        {
            DocumentId = item.Chunk.DocumentId,
            StoredDocumentName = names.TryGetValue(item.Chunk.DocumentId, out var name) ? name : item.Chunk.DocumentId,
            Page = item.Chunk.PageNumber,
            Section = item.Chunk.ChunkMetadata?.SectionTitle,
            Location = $"chunk:{item.Chunk.ChunkIndex};chars:{item.Chunk.StartChar}-{item.Chunk.EndChar}",
            Quote = item.Chunk.ChunkText,
            RetrievedAtUtc = retrievedAtUtc,
            Relevance = item.Relevance
        }).ToList();
    }

    private async Task<string> BuildContextAsync(IReadOnlyList<RetrievedChunk> chunks, CancellationToken cancellationToken)
    {
        var documents = await DocumentsCollection.Find(_ => true).ToListAsync(cancellationToken);
        var names = documents.ToDictionary(document => document.Id!, document => document.FileName);
        var context = new StringBuilder();
        foreach (var result in chunks)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (names.TryGetValue(result.Chunk.DocumentId, out var name))
            {
                context.AppendLine($"[Documento: {name}]");
            }
            context.AppendLine(result.Chunk.ChunkText);
            context.AppendLine("---");
        }
        return context.ToString();
    }

    private async Task<string> GenerateResponseAsync(string query, string context, CancellationToken cancellationToken)
    {
        var history = new ChatHistory();
        history.AddSystemMessage("Responde únicamente basándote en el contexto proporcionado. Si falta información, indícalo claramente.\n\nCONTEXTO:\n" + context);
        history.AddUserMessage(query);
        var response = await _chatService.GetChatMessageContentAsync(history, cancellationToken: cancellationToken);
        return response.Content ?? "No se pudo generar una respuesta.";
    }

    private static float CalculateCosineSimilarity(float[] vectorA, float[] vectorB)
    {
        if (vectorA.Length != vectorB.Length) return 0f;
        var dot = 0f;
        var magnitudeA = 0f;
        var magnitudeB = 0f;
        for (var i = 0; i < vectorA.Length; i++)
        {
            dot += vectorA[i] * vectorB[i];
            magnitudeA += vectorA[i] * vectorA[i];
            magnitudeB += vectorB[i] * vectorB[i];
        }
        if (magnitudeA == 0 || magnitudeB == 0) return 0f;
        return dot / (float)(Math.Sqrt(magnitudeA) * Math.Sqrt(magnitudeB));
    }

    public async Task<int> GetDocumentCountAsync() => (int)await DocumentsCollection.CountDocumentsAsync(_ => true);
    public async Task<int> GetChunkCountAsync() => (int)await ChunksCollection.CountDocumentsAsync(_ => true);

    private sealed record RetrievedChunk(ChunkModel Chunk, double Relevance);
}
