using BlazorApp.Extensions;
using BlazorApp.Models;
using MongoDB.Driver;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Embeddings;
using System.Text;

namespace BlazorApp.Services;

/// <summary>
/// Enhanced RAG service with vector search, reranking, and context window management.
/// Implements RF3.1-RF3.5 requirements for semantic search functionality.
/// </summary>
public class EnhancedRagService
{
    private readonly IMongoDatabase _database;
    private readonly ITextEmbeddingGenerationService _embeddingService;
    private readonly IChatCompletionService _chatService;
    private readonly ILogger<EnhancedRagService> _logger;
    private readonly ResponseFormatter _responseFormatter;

    private IMongoCollection<ChunkModel> ChunksCollection =>
        _database.GetCollection<ChunkModel>("chunks");

    private IMongoCollection<DocumentModel> DocumentsCollection =>
        _database.GetCollection<DocumentModel>("documents");

    // Configuration
    private readonly int _defaultMaxResults = 10;
    private readonly int _maxContextTokens = 3000;
    private readonly float _vectorWeight = 0.7f;
    private readonly float _bm25Weight = 0.3f;
    private readonly int _tokensPerChunk = 256;
    private readonly int _tokenBuffer = 100;

    public EnhancedRagService(
        IMongoDatabase database,
        ITextEmbeddingGenerationService embeddingService,
        IChatCompletionService chatService,
        ILogger<EnhancedRagService> logger,
        ResponseFormatter responseFormatter)
    {
        _database = database;
        _embeddingService = embeddingService;
        _chatService = chatService;
        _logger = logger;
        _responseFormatter = responseFormatter;
    }

    /// <summary>
    /// Queries documents using enhanced RAG with vector search, reranking, and context management.
    /// </summary>
    /// <param name="userQuery">User's question or query.</param>
    /// <param name="maxResults">Maximum number of results to return (default: 10).</param>
    /// <param name="rerank">Whether to rerank results using hybrid scoring (default: true).</param>
    /// <param name="contextTokenLimit">Maximum tokens in context window (default: 3000).</param>
    /// <returns>Formatted response with sources and citations.</returns>
    public async Task<string> QueryAsync(
        string userQuery,
        int maxResults = 10,
        bool rerank = true,
        int contextTokenLimit = 3000)
    {
        try
        {
            _logger.LogInformation("Processing enhanced RAG query: {Query}", userQuery);

            // 1. Generate embedding for user query
            var queryEmbedding = await _embeddingService.GenerateEmbeddingAsync(userQuery);
            var queryVector = queryEmbedding.ToArray();

            // 2. Search for relevant chunks
            var relevantChunks = await SearchVectorialAsync(queryVector, maxResults, rerank, userQuery);

            if (relevantChunks.Count == 0)
            {
                _logger.LogWarning("No relevant chunks found for query: {Query}", userQuery);
                return CreateEmptyResponse(userQuery);
            }

            // 3. Manage context window
            var contextChunks = relevantChunks
                .Select(r => r.Chunk)
                .ToList()
                .ManageContextWindow(
                    contextTokenLimit,
                    _tokensPerChunk,
                    _tokenBuffer);

            // 4. Build context from chunks
            var context = await BuildContextAsync(contextChunks);

            // 5. Generate response using LLM
            var response = await GenerateResponseAsync(userQuery, context);

            // 6. Format response with sources using ResponseFormatter
            var formattedResponse = _responseFormatter.FormatResponse(
                response,
                new ResponseFormatOptions
                {
                    Sources = relevantChunks
                        .Select(r => r.Chunk.DocumentId.ToString())
                        .ToList(),
                    ShowBorder = true,
                    RoundedCorners = true,
                    AddShadow = true,
                    ShowFooter = true,
                    ProcessingTimeMs = 0, // Can be added later for metrics
                    ContextChunksUsed = contextChunks.Count
                });

            _logger.LogInformation("Successfully generated enhanced RAG response with {ChunkCount} chunks", relevantChunks.Count);
            return formattedResponse;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing enhanced RAG query: {Query}", userQuery);
            return CreateErrorResponse(ex);
        }
    }

    /// <summary>
    /// Performs vector similarity search with optional reranking.
    /// Implements RF3.2 (vector search with cosine similarity) and RF3.4 (reranking).
    /// </summary>
    /// <param name="queryVector">Query embedding vector.</param>
    /// <param name="limit">Maximum number of results.</param>
    /// <param name="rerank">Whether to apply reranking with BM25.</param>
    /// <param name="userQuery">Original user query for reranking.</param>
    /// <param name="metadataFilter">Optional metadata filter.</param>
    /// <returns>List of relevant chunks with scores.</returns>
    private async Task<List<ChunkWithScore>> SearchVectorialAsync(
        float[] queryVector,
        int limit,
        bool rerank = true,
        string? userQuery = null,
        FilterDefinition<ChunkModel>? metadataFilter = null)
    {
        try
        {
            _logger.LogDebug("Searching for {Limit} similar chunks", limit);

            // Get all chunks with valid embeddings
            var allChunks = await GetChunksWithValidEmbeddingsAsync(metadataFilter);

            if (allChunks.Count == 0)
            {
                _logger.LogWarning("No chunks with valid embeddings found");
                return new List<ChunkWithScore>();
            }

            List<ChunkWithScore> chunksWithScores;

            // Apply reranking if enabled
            if (rerank && !string.IsNullOrWhiteSpace(userQuery))
            {
                _logger.LogDebug("Applying reranking with BM25");
                chunksWithScores = allChunks
                    .RerankChunks(queryVector, userQuery, _vectorWeight, _bm25Weight)
                    .Take(limit)
                    .ToList();
            }
            else
            {
                // Just take top results by vector similarity without reranking
                chunksWithScores = allChunks
                    .Select(chunk => new ChunkWithScore
                    {
                        Chunk = chunk,
                        VectorScore = queryVector.CalculateCosineSimilarity(chunk.Embedding!)
                    })
                    .OrderByDescending(x => x.VectorScore)
                    .Take(limit)
                    .ToList();
            }

            _logger.LogDebug("Found {Count} chunks with vector similarity > 0", 
                chunksWithScores.Count(x => x.VectorScore > 0));

            _logger.LogInformation("Returning {Count} reranked chunks", chunksWithScores.Count);
            return chunksWithScores;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in vector search");
            return new List<ChunkWithScore>();
        }
    }

    /// <summary>
    /// Gets all chunks with valid embeddings from the database.
    /// </summary>
    /// <param name="filter">Optional additional filter.</param>
    /// <returns>List of chunks with valid embeddings.</returns>
    private async Task<List<ChunkModel>> GetChunksWithValidEmbeddingsAsync(FilterDefinition<ChunkModel>? filter = null)
    {
        try
        {
            var builder = Builders<ChunkModel>.Filter;
            var embeddingsFilter = builder.Exists(c => c.Embedding, true);
            embeddingsFilter = builder.And(
                embeddingsFilter,
                builder.Gt(c => c.Embedding.Length, 0)
            );

            if (filter != null)
            {
                embeddingsFilter = builder.And(embeddingsFilter, filter);
            }

            return await ChunksCollection
                .Find(embeddingsFilter)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving chunks with valid embeddings");
            return new List<ChunkModel>();
        }
    }

    /// <summary>
    /// Builds a context string from chunks, including document information.
    /// Implements RF3.5 (context window management).
    /// </summary>
    /// <param name="chunks">List of chunks to build context from.</param>
    /// <returns>Formatted context string.</returns>
    private async Task<string> BuildContextAsync(List<ChunkModel> chunks)
    {
        var contextBuilder = new StringBuilder();
        
        // Get document info for each chunk
        var documentIds = chunks.Select(c => c.DocumentId).Distinct().ToList();
        var documents = await DocumentsCollection
            .Find(d => documentIds.Contains(d.Id!))
            .ToListAsync();
        
        var documentDict = documents.ToDictionary(d => d.Id!, d => d);

        contextBuilder.AppendLine("=== INFORMACIÓN DE REFERENCIA ===");
        contextBuilder.AppendLine();

        foreach (var chunk in chunks)
        {
            if (documentDict.TryGetValue(chunk.DocumentId, out var document))
            {
                contextBuilder.AppendLine($"[Documento: {document.FileName}]");
                
                if (chunk.PageNumber.HasValue)
                {
                    contextBuilder.AppendLine($"Página: {chunk.PageNumber.Value}");
                }
                
                if (chunk.ChunkMetadata?.SectionTitle != null)
                {
                    contextBuilder.AppendLine($"Sección: {chunk.ChunkMetadata.SectionTitle}");
                }
            }
            
            contextBuilder.AppendLine($"[Fragmento {chunk.ChunkIndex}]");
            contextBuilder.AppendLine(chunk.ChunkText);
            contextBuilder.AppendLine();
            contextBuilder.AppendLine("---");
            contextBuilder.AppendLine();
        }

        return contextBuilder.ToString();
    }

    /// <summary>
    /// Generates a response using LLM with the given context.
    /// </summary>
    /// <param name="userQuery">Original user query.</param>
    /// <param name="context">Context information from chunks.</param>
    /// <returns>Generated response from LLM.</returns>
    private async Task<string> GenerateResponseAsync(string userQuery, string context)
    {
        var systemPrompt = @"Eres un asistente experto que ayuda a responder preguntas basándote en documentos de referencia del datacenter.

INSTRUCCIONES:
1. Responde ÚNICAMENTE basándote en el contexto proporcionado
2. Si la información no está en el contexto, di claramente que no tienes esa información
3. Estructura tu respuesta con pasos numerados cuando sea apropiado
4. Sé claro, conciso y preciso
5. Menciona explícitamente las fuentes de donde obtienes la información
6. Si se pide un proceso, lista cada paso claramente con su número

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

    /// <summary>
    /// Formats response with source citations and chunk details.
    /// </summary>
    /// <param name="response">LLM-generated response.</param>
    /// <param name="allChunks">All relevant chunks found.</param>
    /// <param name="contextChunks">Chunks used in context window.</param>
    /// <returns>Formatted response with sources and citations.</returns>
    private string FormatResponseWithSources(
        string response,
        List<ChunkWithScore> allChunks,
        List<ChunkModel> contextChunks)
    {
        var formattedResponse = new StringBuilder();
        formattedResponse.AppendLine(response);
        formattedResponse.AppendLine();
        formattedResponse.AppendLine();
        formattedResponse.AppendLine("---");
        formattedResponse.AppendLine();
        formattedResponse.AppendLine("**Fuentes consultadas:**");

        // Group chunks by document
        var chunksByDoc = allChunks
            .GroupBy(c => c.Chunk.DocumentId)
            .Select(g => new
            {
                DocumentId = g.Key,
                Chunks = g.ToList(),
                Score = g.Average(x => x.CombinedScore > 0 ? x.CombinedScore : x.VectorScore)
            })
            .OrderByDescending(x => x.Score)
            .ToList();

        foreach (var docGroup in chunksByDoc)
        {
            formattedResponse.AppendLine();
            
            // Get document name
            var document = docGroup.Chunks.FirstOrDefault()?.Chunk;
            if (document != null)
            {
                // Fetch document details
                var docModel = DocumentsCollection
                    .Find(d => d.Id == document.DocumentId)
                    .FirstOrDefault();
                
                if (docModel != null)
                {
                    formattedResponse.AppendLine($"📄 **{docModel.FileName}**");
                    formattedResponse.AppendLine($"   - **Similitud:** {docGroup.Score:F2}");
                    formattedResponse.AppendLine($"   - **Fragmentos:** {docGroup.Chunks.Count}");
                    
                    // Show page numbers if available
                    var pages = docGroup.Chunks
                        .Where(c => c.Chunk.PageNumber.HasValue)
                        .Select(c => c.Chunk.PageNumber.Value)
                        .Distinct()
                        .OrderBy(p => p)
                        .ToList();
                    
                    if (pages.Count > 0)
                    {
                        formattedResponse.AppendLine($"   - **Páginas:** {string.Join(", ", pages)}");
                    }
                }
            }
            
            formattedResponse.AppendLine();
        }

        return formattedResponse.ToString();
    }

    /// <summary>
    /// Creates a response when no relevant chunks are found.
    /// </summary>
    /// <param name="userQuery">Original user query.</param>
    /// <returns>Response indicating no relevant information found.</returns>
    private string CreateEmptyResponse(string userQuery)
    {
        return $"Lo siento, no encontré información relevante en los documentos procesados para responder a: \"{userQuery}\".\n\n" +
               "Puedes intentar:\n" +
               "- Reformular tu pregunta con términos diferentes\n" +
               "- Verificar que los documentos relevantes hayan sido procesados\n" +
               "- Consultar con más detalles sobre el tema";
    }

    /// <summary>
    /// Creates an error response.
    /// </summary>
    /// <param name="ex">Exception that occurred.</param>
    /// <returns>Formatted error response.</returns>
    private string CreateErrorResponse(Exception ex)
    {
        _logger.LogError(ex, "Error in EnhancedRagService");
        return $"Ocurrió un error al procesar tu consulta: {ex.Message}\n\n" +
               "Por favor, intenta nuevamente o revisa los logs del sistema para más detalles.";
    }

    /// <summary>
    /// Gets the count of chunks with vector embeddings.
    /// </summary>
    /// <returns>Number of chunks with embeddings.</returns>
    public async Task<int> GetChunkCountWithEmbeddingsAsync()
    {
        var builder = Builders<ChunkModel>.Filter;
        var filter = builder.Exists(c => c.Embedding, true);
        filter = builder.And(filter, builder.Gt(c => c.Embedding.Length, 0));
        
        return (int)await ChunksCollection.CountDocumentsAsync(filter);
    }

    /// <summary>
    /// Searches for chunks with metadata filtering.
    /// </summary>
    /// <param name="query">User query text.</param>
    /// <param name="documentIds">Optional document IDs to filter by.</param>
    /// <param name="pageNumber">Optional page number to filter by.</param>
    /// <param name="maxResults">Maximum number of results.</param>
    /// <param name="rerank">Whether to rerank results.</param>
    /// <returns>Formatted response with filtered search results.</returns>
    public async Task<string> QueryWithFiltersAsync(
        string query,
        IEnumerable<string>? documentIds = null,
        int? pageNumber = null,
        int maxResults = 10,
        bool rerank = true)
    {
        try
        {
            var builder = Builders<ChunkModel>.Filter;
            var filter = builder.Empty;

            // Apply document filter
            if (documentIds != null && documentIds.Any())
            {
                filter = builder.And(filter, builder.In(c => c.DocumentId, documentIds));
            }

            // Apply page number filter
            if (pageNumber.HasValue)
            {
                filter = builder.And(filter, builder.Eq(c => c.PageNumber, pageNumber.Value));
            }

            // Generate embedding
            var queryEmbedding = await _embeddingService.GenerateEmbeddingAsync(query);
            var queryVector = queryEmbedding.ToArray();

            // Search with metadata filter
            var results = await SearchVectorialAsync(
                queryVector,
                maxResults,
                rerank,
                query,
                filter);

            if (results.Count == 0)
            {
                return CreateEmptyResponse(query);
            }

            // Build context and generate response
            var context = await BuildContextAsync(results.Select(r => r.Chunk).ToList());
            var response = await GenerateResponseAsync(query, context);
            var formattedResponse = FormatResponseWithSources(response, results, results.Select(r => r.Chunk).ToList());

            return formattedResponse;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in filtered query");
            return CreateErrorResponse(ex);
        }
    }
}
