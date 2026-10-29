using BlazorApp.Models;
using MongoDB.Bson;
using MongoDB.Driver;
using System.Linq.Expressions;
using System.Text;

namespace BlazorApp.Extensions;

/// <summary>
/// Extension methods for MongoDB operations, particularly for vector search.
/// Provides helper methods for cosine similarity calculations and vector operations.
/// </summary>
public static class MongoDbExtensions
{
    /// <summary>
    /// Calculates cosine similarity between two vectors.
    /// </summary>
    /// <param name="vectorA">First vector.</param>
    /// <param name="vectorB">Second vector.</param>
    /// <returns>Cosine similarity score between -1 and 1, where 1 means identical direction.</returns>
    public static float CalculateCosineSimilarity(this float[] vectorA, float[] vectorB)
    {
        if (vectorA == null || vectorB == null)
            return 0f;

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

    /// <summary>
    /// Normalizes a vector to unit length.
    /// </summary>
    /// <param name="vector">Input vector.</param>
    /// <returns>Normalized vector with unit magnitude.</returns>
    public static float[] NormalizeVector(this float[] vector)
    {
        if (vector == null || vector.Length == 0)
            return vector;

        float magnitude = 0f;
        foreach (var value in vector)
        {
            magnitude += value * value;
        }
        magnitude = (float)Math.Sqrt(magnitude);

        if (magnitude == 0f)
            return vector;

        return vector.Select(v => v / magnitude).ToArray();
    }

    /// <summary>
    /// Builds a MongoDB Atlas Vector Search index definition as a BSON document.
    /// The resulting document can be passed to <c>IMongoCollection{T}.SearchIndexes.CreateOneAsync</c>
    /// (driver v2.21+) or to the <c>createSearchIndexes</c> command / Atlas CLI / mongosh scripts.
    /// </summary>
    /// <param name="embeddingField">Name of the field containing the embedding vector.</param>
    /// <param name="dimensions">Number of dimensions in the embedding vector.</param>
    /// <param name="similarity">Similarity metric ("cosine", "euclidean", or "dotProduct").</param>
    /// <returns>BSON document describing the vector search index definition.</returns>
    public static BsonDocument BuildVectorSearchIndexDefinition(
        string embeddingField,
        int dimensions,
        string similarity = "cosine")
    {
        return new BsonDocument
        {
            ["mappings"] = new BsonDocument
            {
                ["dynamic"] = false,
                ["fields"] = new BsonDocument
                {
                    [embeddingField] = new BsonDocument
                    {
                        ["type"] = "knnVector",
                        ["dimensions"] = dimensions,
                        ["similarity"] = similarity
                    }
                }
            }
        };
    }

    /// <summary>
    /// Builds a filter for chunks that have valid embeddings.
    /// </summary>
    /// <param name="builder">Filter definition builder.</param>
    /// <returns>Filter definition for chunks with non-null and non-empty embeddings.</returns>
    public static FilterDefinition<ChunkModel> WithValidEmbeddings(this FilterDefinitionBuilder<ChunkModel> builder)
    {
        return builder.And(
            builder.Ne(c => c.Embedding, null),
            builder.Gt(c => c.Embedding.Length, 0)
        );
    }

    /// <summary>
    /// Builds a filter for chunks from specific documents.
    /// </summary>
    /// <param name="builder">Filter definition builder.</param>
    /// <param name="documentIds">List of document IDs to filter by.</param>
    /// <returns>Filter definition for chunks from specified documents.</returns>
    public static FilterDefinition<ChunkModel> FromDocuments(this FilterDefinitionBuilder<ChunkModel> builder, IEnumerable<string> documentIds)
    {
        return builder.In(c => c.DocumentId, documentIds);
    }

    /// <summary>
    /// Projects chunk model to include only relevant fields for search.
    /// </summary>
    /// <param name="projectionBuilder">Projection definition builder.</param>
    /// <param name="includeScore">Whether to include similarity score in projection.</param>
    /// <returns>Projection definition for optimized search results.</returns>
    public static ProjectionDefinition<ChunkModel> SearchProjection(
        this ProjectionDefinitionBuilder<ChunkModel> projectionBuilder,
        bool includeScore = false)
    {
        if (includeScore)
        {
            return projectionBuilder.Combine(
                projectionBuilder.Include(c => c.Id),
                projectionBuilder.Include(c => c.DocumentId),
                projectionBuilder.Include(c => c.ChunkIndex),
                projectionBuilder.Include(c => c.StartChar),
                projectionBuilder.Include(c => c.EndChar),
                projectionBuilder.Include(c => c.ChunkText),
                projectionBuilder.Include(c => c.PageNumber),
                projectionBuilder.Include(c => c.ContentType),
                projectionBuilder.Include(c => c.ChunkMetadata),
                projectionBuilder.Include(c => c.CreatedAt)
            );
        }

        return projectionBuilder.Combine(
            projectionBuilder.Include(c => c.Id),
            projectionBuilder.Include(c => c.DocumentId),
            projectionBuilder.Include(c => c.ChunkIndex),
            projectionBuilder.Include(c => c.StartChar),
            projectionBuilder.Include(c => c.EndChar),
            projectionBuilder.Include(c => c.ChunkText),
            projectionBuilder.Include(c => c.PageNumber),
            projectionBuilder.Include(c => c.ContentType),
            projectionBuilder.Include(c => c.ChunkMetadata),
            projectionBuilder.Include(c => c.CreatedAt)
        );
    }

    /// <summary>
    /// Creates a compound index for common query patterns.
    /// </summary>
    /// <param name="builder">Index keys definition builder.</param>
    /// <param name="documentIdField">Field for document ID sorting.</param>
    /// <param name="chunkIndexField">Field for chunk index sorting.</param>
    /// <returns>Compound index definition.</returns>
    public static IndexKeysDefinition<ChunkModel> CreateCompoundIndex(
        this IndexKeysDefinitionBuilder<ChunkModel> builder,
        Expression<Func<ChunkModel, string>> documentIdField,
        Expression<Func<ChunkModel, int>> chunkIndexField)
    {
        return builder
            .Ascending(new ExpressionFieldDefinition<ChunkModel, string>(documentIdField))
            .Ascending(new ExpressionFieldDefinition<ChunkModel, int>(chunkIndexField));
    }

    /// <summary>
    /// Applies a text search filter combined with vector search.
    /// </summary>
    /// <param name="builder">Filter definition builder.</param>
    /// <param name="textQuery">Text query to match.</param>
    /// <param name="embedding">Vector embedding for semantic search.</param>
    /// <returns>Combined filter definition for hybrid search.</returns>
    public static FilterDefinition<ChunkModel> HybridSearchFilter(
        this FilterDefinitionBuilder<ChunkModel> builder,
        string textQuery,
        float[]? embedding = null)
    {
        var filters = new List<FilterDefinition<ChunkModel>>();

        if (!string.IsNullOrWhiteSpace(textQuery))
        {
            filters.Add(builder.Regex(c => c.ChunkText, new BsonRegularExpression(textQuery, "i")));
        }

        if (embedding != null && embedding.Length > 0)
        {
            // Vector search would be added here when using MongoDB Atlas Vector Search
            // For now, we'll handle this in-memory
        }

        return filters.Count > 0 ? builder.And(filters) : builder.Empty;
    }

    /// <summary>
    /// Calculates BM25 score for text relevance (simplified implementation).
    /// </summary>
    /// <param name="chunkText">Text content of the chunk.</param>
    /// <param name="queryTerms">Terms from the user query.</param>
    /// <param name="avgDocLength">Average document length in corpus.</param>
    /// <param name="docCount">Total number of documents in corpus.</param>
    /// <returns>BM25 relevance score.</returns>
    public static float CalculateBM25Score(
        this string chunkText,
        IEnumerable<string> queryTerms,
        float avgDocLength = 500f,
        int docCount = 1000)
    {
        if (string.IsNullOrWhiteSpace(chunkText) || !queryTerms.Any())
            return 0f;

        var terms = chunkText.ToLower().Split(new[] { ' ', ',', '.', ';', ':', '-', '_', '(', ')', '[', ']', '{', '}', '\n', '\r' },
            StringSplitOptions.RemoveEmptyEntries);

        var termFrequency = terms.GroupBy(t => t).ToDictionary(g => g.Key, g => g.Count());
        var documentFrequency = terms.Distinct().Count();

        float score = 0f;
        const float k1 = 1.5f;
        const float b = 0.75f;

        foreach (var queryTerm in queryTerms.Where(t => !string.IsNullOrWhiteSpace(t)))
        {
            var term = queryTerm.ToLower();
            var tf = termFrequency.ContainsKey(term) ? termFrequency[term] : 0;
            var df = documentFrequency;

            // BM25 formula
            var idf = (float)Math.Log((docCount - df + 0.5) / (df + 0.5) + 1);
            var freqPart = (tf * (k1 + 1)) / (tf + k1 * (1 - b + b * (terms.Length / avgDocLength)));

            score += idf * freqPart;
        }

        return score;
    }

    /// <summary>
    /// Reranks chunks using a combination of vector similarity and BM25 scoring.
    /// </summary>
    /// <param name="chunks">List of chunks to rerank.</param>
    /// <param name="queryEmbedding">Embedding of the user query.</param>
    /// <param name="userQuery">Original user query text.</param>
    /// <param name="vectorWeight">Weight for vector similarity (0-1).</param>
    /// <param name="bm25Weight">Weight for BM25 scoring (0-1).</param>
    /// <returns>Reranked list of chunks with combined scores.</returns>
    public static List<ChunkWithScore> RerankChunks(
        this List<ChunkModel> chunks,
        float[] queryEmbedding,
        string userQuery,
        float vectorWeight = 0.7f,
        float bm25Weight = 0.3f)
    {
        if (chunks.Count == 0)
            return new List<ChunkWithScore>();

        var queryTerms = userQuery.Split(new[] { ' ', ',', '.', ';', ':', '-', '_', '(', ')', '[', ']', '{', '}', '\n', '\r' },
            StringSplitOptions.RemoveEmptyEntries);

        var avgDocLength = (float)chunks.Average(c => c.ChunkText.Length);
        var docCount = chunks.Count;

        return chunks.Select(chunk => new ChunkWithScore
        {
            Chunk = chunk,
            VectorScore = chunk.Embedding != null ? chunk.Embedding.CalculateCosineSimilarity(queryEmbedding) : 0f,
            BM25Score = chunk.ChunkText.CalculateBM25Score(queryTerms, avgDocLength, docCount)
        })
        .Select(c => new ChunkWithScore
        {
            Chunk = c.Chunk,
            VectorScore = c.VectorScore,
            BM25Score = c.BM25Score,
            CombinedScore = (c.VectorScore * vectorWeight) + (c.BM25Score * bm25Weight)
        })
        .OrderByDescending(c => c.CombinedScore)
        .ToList();
    }

    /// <summary>
    /// Manages context window by selecting chunks within token limits.
    /// </summary>
    /// <param name="chunks">List of chunks to manage.</param>
    /// <param name="maxTokens">Maximum number of tokens allowed.</param>
    /// <param name="tokensPerChunk">Average tokens per chunk (default: 256).</param>
    /// <param name="tokenBuffer">Buffer tokens for prompt overhead (default: 100).</param>
    /// <returns>Subset of chunks within token limit.</returns>
    public static List<ChunkModel> ManageContextWindow(
        this List<ChunkModel> chunks,
        int maxTokens = 3000,
        int tokensPerChunk = 256,
        int tokenBuffer = 100)
    {
        if (chunks.Count == 0)
            return new List<ChunkModel>();

        var maxChunks = (maxTokens - tokenBuffer) / tokensPerChunk;
        return chunks.Take(Math.Min(maxChunks, chunks.Count)).ToList();
    }

    /// <summary>
    /// Formats chunks with source citations.
    /// </summary>
    /// <param name="chunks">List of chunks to format.</param>
    /// <param name="documents">Dictionary of document models by ID.</param>
    /// <param name="includeMetadata">Whether to include chunk metadata in output.</param>
    /// <returns>Formatted string with chunks and source information.</returns>
    public static string FormatWithSources(
        this List<ChunkModel> chunks,
        Dictionary<string, DocumentModel> documents,
        bool includeMetadata = true)
    {
        if (chunks.Count == 0)
            return string.Empty;

        var formatted = new StringBuilder();
        var seenDocuments = new HashSet<string>();

        foreach (var chunk in chunks)
        {
            if (documents.TryGetValue(chunk.DocumentId, out var document))
            {
                var docKey = $"{document.FilePath}_{chunk.PageNumber}";
                
                if (!seenDocuments.Contains(docKey))
                {
                    formatted.AppendLine();
                    formatted.AppendLine($"--- Fuente: {document.FileName} ---");
                    
                    if (chunk.PageNumber.HasValue)
                    {
                        formatted.AppendLine($"Página: {chunk.PageNumber.Value}");
                    }
                    
                    if (includeMetadata && chunk.ChunkMetadata != null)
                    {
                        if (!string.IsNullOrWhiteSpace(chunk.ChunkMetadata.SectionTitle))
                        {
                            formatted.AppendLine($"Sección: {chunk.ChunkMetadata.SectionTitle}");
                        }
                    }
                    
                    seenDocuments.Add(docKey);
                    formatted.AppendLine();
                }
            }

            formatted.AppendLine(chunk.ChunkText);
            formatted.AppendLine();
        }

        return formatted.ToString();
    }
}

/// <summary>
/// Represents a chunk with its relevance scores.
/// </summary>
public class ChunkWithScore
{
    /// <summary>
    /// The chunk model.
    /// </summary>
    public ChunkModel Chunk { get; set; } = new();

    /// <summary>
    /// Vector similarity score.
    /// </summary>
    public float VectorScore { get; set; }

    /// <summary>
    /// BM25 text relevance score.
    /// </summary>
    public float BM25Score { get; set; }

    /// <summary>
    /// Combined weighted score.
    /// </summary>
    public float CombinedScore { get; set; }
}
