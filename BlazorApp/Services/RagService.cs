using BlazorApp.Models;
using MongoDB.Driver;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Embeddings;
using System.Text;

namespace BlazorApp.Services;

/// <summary>
/// RAG Service with vector search and context-aware responses
/// </summary>
public class RagService
{
    private readonly IMongoDatabase _database;
    private readonly ITextEmbeddingGenerationService _embeddingService;
    private readonly IChatCompletionService _chatService;
    private readonly ILogger<RagService> _logger;

    private IMongoCollection<ChunkModel> ChunksCollection =>
        _database.GetCollection<ChunkModel>("chunks");

    private IMongoCollection<DocumentModel> DocumentsCollection =>
        _database.GetCollection<DocumentModel>("documents");

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

    public async Task<string> QueryAsync(string userQuery, int maxResults = 5)
    {
        try
        {
            _logger.LogInformation("Processing RAG query: {Query}", userQuery);

            // 1. Generate embedding for user query
            var queryEmbedding = await _embeddingService.GenerateEmbeddingAsync(userQuery);
            var queryVector = queryEmbedding.ToArray();

            // 2. Search for relevant chunks
            var relevantChunks = await SearchSimilarChunksAsync(queryVector, maxResults);

            if (relevantChunks.Count == 0)
            {
                _logger.LogWarning("No relevant chunks found for query: {Query}", userQuery);
                return "Lo siento, no encontré información relevante en los documentos procesados para responder a tu pregunta.";
            }

            // 3. Build context from chunks
            var context = await BuildContextAsync(relevantChunks);

            // 4. Generate response using LLM
            var response = await GenerateResponseAsync(userQuery, context);

            // 5. Format response with sources
            var formattedResponse = FormatResponseWithSources(response, relevantChunks);

            _logger.LogInformation("Successfully generated RAG response");
            return formattedResponse;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing RAG query: {Query}", userQuery);
            return $"Error al procesar la consulta: {ex.Message}";
        }
    }

    private async Task<List<ChunkModel>> SearchSimilarChunksAsync(float[] queryVector, int limit)
    {
        try
        {
            // Get all chunks (for now, we'll calculate similarity in memory)
            // In production, use MongoDB Atlas Vector Search or similar
            var allChunks = await ChunksCollection.Find(_ => true).ToListAsync();

            if (allChunks.Count == 0)
            {
                _logger.LogWarning("No chunks found in database");
                return new List<ChunkModel>();
            }

            // Calculate cosine similarity for each chunk
            var chunksWithScores = allChunks
                .Where(chunk => chunk.Embedding != null && chunk.Embedding.Length > 0)
                .Select(chunk => new
                {
                    Chunk = chunk,
                    Score = CalculateCosineSimilarity(queryVector, chunk.Embedding!)
                })
                .OrderByDescending(x => x.Score)
                .Take(limit)
                .ToList();

            _logger.LogInformation("Found {Count} similar chunks", chunksWithScores.Count);

            return chunksWithScores.Select(x => x.Chunk).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching similar chunks");
            return new List<ChunkModel>();
        }
    }

    private float CalculateCosineSimilarity(float[] vectorA, float[] vectorB)
    {
        if (vectorA.Length != vectorB.Length)
            return 0f;

        float dotProduct = 0f;
        float magnitudeA = 0f;
        float magnitudeB = 0f;

        for (int i = 0; i < vectorA.Length; i++)
        {
            dotProduct += vectorA[i] * vectorB[i];
            magnitudeA += vectorA[i] * vectorA[i];
            magnitudeB += vectorB[i] * vectorB[i];
        }

        magnitudeA = (float)Math.Sqrt(magnitudeA);
        magnitudeB = (float)Math.Sqrt(magnitudeB);

        if (magnitudeA == 0f || magnitudeB == 0f)
            return 0f;

        return dotProduct / (magnitudeA * magnitudeB);
    }

    private async Task<string> BuildContextAsync(List<ChunkModel> chunks)
    {
        var contextBuilder = new StringBuilder();
        
        // Get document info for each chunk
        var documentIds = chunks.Select(c => c.DocumentId).Distinct().ToList();
        var documents = await DocumentsCollection
            .Find(d => documentIds.Contains(d.Id!))
            .ToListAsync();
        
        var documentDict = documents.ToDictionary(d => d.Id!, d => d);

        foreach (var chunk in chunks)
        {
            if (documentDict.TryGetValue(chunk.DocumentId, out var document))
            {
                contextBuilder.AppendLine($"[Documento: {document.FileName}]");
            }
            contextBuilder.AppendLine(chunk.ChunkText);
            contextBuilder.AppendLine();
            contextBuilder.AppendLine("---");
            contextBuilder.AppendLine();
        }

        return contextBuilder.ToString();
    }

    private async Task<string> GenerateResponseAsync(string userQuery, string context)
    {
        var systemPrompt = @"Eres un asistente experto que ayuda a responder preguntas basándote en documentos de referencia.

INSTRUCCIONES:
1. Responde ÚNICAMENTE basándote en el contexto proporcionado
2. Si la información no está en el contexto, di claramente que no tienes esa información
3. Estructura tu respuesta con pasos numerados cuando sea apropiado
4. Sé claro, conciso y preciso
5. Si mencionas procedimientos, lista cada paso claramente

CONTEXTO:
" + context;

        var chatHistory = new ChatHistory();
        chatHistory.AddSystemMessage(systemPrompt);
        chatHistory.AddUserMessage(userQuery);

        try
        {
            var response = await _chatService.GetChatMessageContentAsync(chatHistory);
            return response.Content ?? "No se pudo generar una respuesta.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling LLM service");
            return "Error al generar la respuesta del modelo de lenguaje.";
        }
    }

    private string FormatResponseWithSources(string response, List<ChunkModel> chunks)
    {
        var formattedResponse = new StringBuilder();
        formattedResponse.AppendLine(response);
        formattedResponse.AppendLine();
        formattedResponse.AppendLine("---");
        formattedResponse.AppendLine();
        formattedResponse.AppendLine("**Fuentes consultadas:**");

        // Get unique document IDs from chunks
        var documentIds = chunks.Select(c => c.DocumentId).Distinct();
        
        foreach (var docId in documentIds)
        {
            var chunksFromDoc = chunks.Where(c => c.DocumentId == docId).ToList();
            if (chunksFromDoc.Any())
            {
                formattedResponse.AppendLine($"- Documento: {docId} ({chunksFromDoc.Count} fragmentos)");
            }
        }

        return formattedResponse.ToString();
    }

    public async Task<int> GetDocumentCountAsync()
    {
        return (int)await DocumentsCollection.CountDocumentsAsync(_ => true);
    }

    public async Task<int> GetChunkCountAsync()
    {
        return (int)await ChunksCollection.CountDocumentsAsync(_ => true);
    }
}
