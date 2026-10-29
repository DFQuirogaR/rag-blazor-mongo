using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.ComponentModel.DataAnnotations;

namespace BlazorApp.Models;

/// <summary>
/// Represents the result of generating an embedding for a chunk.
/// Stores the embedding vector along with metadata about the embedding process.
/// </summary>
public class EmbeddingResult
{
    /// <summary>
    /// Unique identifier for this embedding result.
    /// </summary>
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = ObjectId.GenerateNewId().ToString();

    /// <summary>
    /// Reference to the parent DocumentModel.
    /// </summary>
    [Required]
    [BsonRepresentation(BsonType.ObjectId)]
    public string DocumentId { get; set; } = ObjectId.GenerateNewId().ToString();

    /// <summary>
    /// Index of the chunk this embedding was generated for.
    /// </summary>
    [Required]
    [Range(0, int.MaxValue, ErrorMessage = "ChunkIndex must be a non-negative number.")]
    public int ChunkIndex { get; set; }

    /// <summary>
    /// The embedding vector as an array of floats.
    /// Typically 768 dimensions for models like all-MiniLM-L6-v2.
    /// </summary>
    [Required]
    public float[] Vector { get; set; } = Array.Empty<float>();

    /// <summary>
    /// Number of dimensions in the embedding vector.
    /// Used to validate that embeddings have the expected dimensionality.
    /// </summary>
    [Required]
    [Range(1, int.MaxValue, ErrorMessage = "DimensionCount must be a positive number.")]
    public int DimensionCount { get; set; }

    /// <summary>
    /// Name or identifier of the embedding model used.
    /// e.g., "all-MiniLM-L6-v2", "text-embedding-3-small"
    /// </summary>
    [Required]
    [StringLength(100, ErrorMessage = "EmbeddingModel cannot exceed 100 characters.")]
    public string EmbeddingModel { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp when the embedding was generated.
    /// </summary>
    [Required]
    public DateTime EmbeddingTimestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Optional processing time in milliseconds for embedding generation.
    /// </summary>
    [Range(0, int.MaxValue, ErrorMessage = "ProcessingTimeMs must be a non-negative number.")]
    public int? ProcessingTimeMs { get; set; }

    /// <summary>
    /// Optional token count used to generate this embedding.
    /// </summary>
    [Range(0, int.MaxValue, ErrorMessage = "TokenCount must be a non-negative number.")]
    public int? TokenCount { get; set; }

    /// <summary>
    /// Creates a new EmbeddingResult instance.
    /// </summary>
    /// <param name="documentId">The ID of the parent document.</param>
    /// <param name="chunkIndex">The index of the chunk this embedding is for.</param>
    /// <param name="vector">The embedding vector.</param>
    /// <param name="embeddingModel">Name of the embedding model used.</param>
    /// <param name="processingTimeMs">Optional processing time in milliseconds.</param>
    /// <param name="tokenCount">Optional token count used.</param>
    /// <returns>A new EmbeddingResult instance.</returns>
    public static EmbeddingResult Create(
        string documentId,
        int chunkIndex,
        float[] vector,
        string embeddingModel,
        int? processingTimeMs = null,
        int? tokenCount = null)
    {
        return new EmbeddingResult
        {
            DocumentId = documentId,
            ChunkIndex = chunkIndex,
            Vector = vector,
            DimensionCount = vector.Length,
            EmbeddingModel = embeddingModel,
            ProcessingTimeMs = processingTimeMs,
            TokenCount = tokenCount
        };
    }

    /// <summary>
    /// Validates that the embedding vector has the expected dimensionality.
    /// </summary>
    /// <param name="expectedDimensions">The expected number of dimensions.</param>
    /// <returns>True if the vector dimensions match; otherwise, false.</returns>
    public bool ValidateDimensionCount(int expectedDimensions)
    {
        return DimensionCount == expectedDimensions && Vector.Length == expectedDimensions;
    }

    /// <summary>
    /// Serializes the embedding vector to a JSON string.
    /// Useful for debugging or export purposes.
    /// </summary>
    /// <returns>A JSON string representation of the embedding vector.</returns>
    public string SerializeEmbedding()
    {
        return System.Text.Json.JsonSerializer.Serialize(Vector);
    }

    /// <summary>
    /// Deserializes a JSON string back into a float array.
    /// </summary>
    /// <param name="json">The JSON string to deserialize.</param>
    /// <returns>A float array representing the embedding vector.</returns>
    public static float[] DeserializeEmbedding(string json)
    {
        return System.Text.Json.JsonSerializer.Deserialize<float[]>(json) ?? Array.Empty<float>();
    }
}
