using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.ComponentModel.DataAnnotations;

namespace BlazorApp.Models;

/// <summary>
/// Represents a chunk of text extracted from a document for vector search.
/// Each chunk contains the text content and its corresponding embedding vector.
/// </summary>
public class ChunkModel
{
    /// <summary>
    /// Unique identifier for the chunk.
    /// Uses Guid instead of ObjectId for distributed uniqueness.
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
    /// Zero-based index of this chunk within the document.
    /// Used to maintain chunk order and for chunk reconstruction.
    /// </summary>
    [Required]
    [Range(0, int.MaxValue, ErrorMessage = "ChunkIndex must be a non-negative number.")]
    public int ChunkIndex { get; set; }

    /// <summary>
    /// Starting character position of this chunk in the original document.
    /// </summary>
    [Required]
    [Range(0, int.MaxValue, ErrorMessage = "StartChar must be a non-negative number.")]
    public int StartChar { get; set; }

    /// <summary>
    /// Ending character position of this chunk in the original document.
    /// </summary>
    [Required]
    [Range(0, int.MaxValue, ErrorMessage = "EndChar must be a non-negative number.")]
    public int EndChar { get; set; }

    /// <summary>
    /// Text content of this chunk.
    /// </summary>
    [Required]
    [StringLength(10000, ErrorMessage = "ChunkText cannot exceed 10000 characters.")]
    public string ChunkText { get; set; } = string.Empty;

    /// <summary>
    /// Metadata about this specific chunk.
    /// </summary>
    public ChunkMetadata? ChunkMetadata { get; set; }

    /// <summary>
    /// Embedding vector for this chunk (typically 768 dimensions).
    /// Used for vector similarity search.
    /// </summary>
    public float[]? Embedding { get; set; }

    /// <summary>
    /// Page number where this chunk appears in the original document.
    /// Optional as not all documents have pages (e.g., plain text).
    /// </summary>
    [Range(1, int.MaxValue, ErrorMessage = "PageNumber must be a positive number.")]
    public int? PageNumber { get; set; }

    /// <summary>
    /// Type of content this chunk represents.
    /// </summary>
    [Required]
    [StringLength(20, ErrorMessage = "ContentType cannot exceed 20 characters.")]
    public string ContentType { get; set; } = ChunkType.Paragraph.ToString();

    /// <summary>
    /// Number of words in this chunk.
    /// </summary>
    [Required]
    [Range(0, int.MaxValue, ErrorMessage = "WordCount must be a non-negative number.")]
    public int WordCount { get; set; }

    /// <summary>
    /// Number of characters in this chunk.
    /// </summary>
    [Required]
    [Range(0, int.MaxValue, ErrorMessage = "CharacterCount must be a non-negative number.")]
    public int CharacterCount { get; set; }

    /// <summary>
    /// Timestamp when this chunk was created.
    /// </summary>
    [Required]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Creates a new ChunkModel instance from document content.
    /// </summary>
    /// <param name="documentId">The ID of the parent document.</param>
    /// <param name="chunkIndex">The zero-based index of this chunk.</param>
    /// <param name="content">The text content of this chunk.</param>
    /// <param name="startChar">Starting character position in the original document.</param>
    /// <param name="endChar">Ending character position in the original document.</param>
    /// <param name="metadata">Optional chunk-specific metadata.</param>
    /// <param name="embedding">Optional embedding vector for this chunk.</param>
    /// <param name="pageNumber">Optional page number in the original document.</param>
    /// <param name="chunkType">Optional type of content.</param>
    /// <returns>A new ChunkModel instance.</returns>
    public static ChunkModel Create(
        string documentId,
        int chunkIndex,
        string content,
        int startChar,
        int endChar,
        ChunkMetadata? metadata = null,
        float[]? embedding = null,
        int? pageNumber = null,
        string chunkType = "paragraph")
    {
        return new ChunkModel
        {
            DocumentId = documentId,
            ChunkIndex = chunkIndex,
            StartChar = startChar,
            EndChar = endChar,
            ChunkText = content,
            ChunkMetadata = metadata,
            Embedding = embedding,
            PageNumber = pageNumber,
            ContentType = chunkType.ToLower(),
            WordCount = content.Split(' ').Length,
            CharacterCount = content.Length
        };
    }

    /// <summary>
    /// Enum representing the type of content in this chunk.
    /// </summary>
    public enum ChunkType
    {
        /// <summary>
        /// A section title or heading.
        /// </summary>
        Title,

        /// <summary>
        /// A regular paragraph of text.
        /// </summary>
        Paragraph,

        /// <summary>
        /// A list item or bullet point.
        /// </summary>
        List,

        /// <summary>
        /// A table of data.
        /// </summary>
        Table
    }
}
