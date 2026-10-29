using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.ComponentModel.DataAnnotations;

namespace BlazorApp.Models;

/// <summary>
/// Represents a document stored in MongoDB for the RAG system.
/// Contains metadata and tracks the processing status.
/// </summary>
public class DocumentModel
{
    /// <summary>
    /// Unique identifier for the document.
    /// Uses Guid instead of ObjectId for distributed uniqueness.
    /// </summary>
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = ObjectId.GenerateNewId().ToString();

    /// <summary>
    /// Full path to the document file.
    /// </summary>
    /// <example>C:\datacenter\procedures\onboarding.docx</example>
    [Required]
    [StringLength(500, ErrorMessage = "FilePath cannot exceed 500 characters.")]
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// Name of the document file including extension.
    /// </summary>
    /// <example>onboarding.docx</example>
    [Required]
    [StringLength(255, ErrorMessage = "FileName cannot exceed 255 characters.")]
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// Document processing status enumeration represented as string.
    /// Values: Pending, Processing, Completed, Failed
    /// </summary>
    [Required]
    [StringLength(20, ErrorMessage = "ProcessingStatus cannot exceed 20 characters.")]
    public string ProcessingStatus { get; set; } = DocumentStatus.Pending.ToString();

    /// <summary>
    /// SHA256 hash of the document content for deduplication.
    /// </summary>
    [Required]
    [StringLength(64, ErrorMessage = "FileHash must be 64 characters (SHA256).")]
    public string FileHash { get; set; } = string.Empty;

    /// <summary>
    /// Size of the document in bytes.
    /// </summary>
    [Required]
    [Range(0, long.MaxValue, ErrorMessage = "FileSize must be a positive number.")]
    public long FileSize { get; set; }

    /// <summary>
    /// File extension/type (e.g., .docx, .pdf).
    /// </summary>
    [Required]
    [StringLength(10, ErrorMessage = "FileType cannot exceed 10 characters.")]
    public string FileType { get; set; } = string.Empty;

    /// <summary>
    /// Upload timestamp in UTC.
    /// </summary>
    [Required]
    public DateTime UploadDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// SHA256 hash of the document content for deduplication.
    /// </summary>
    [Required]
    [StringLength(64, ErrorMessage = "ContentHash must be 64 characters (SHA256).")]
    public string ContentHash { get; set; } = string.Empty;



    /// <summary>
    /// Optional document metadata such as title, author, and creation date.
    /// </summary>
    public DocumentMetadata? DocumentMetadata { get; set; }

    /// <summary>
    /// List of tags for document categorization and search.
    /// </summary>
    public List<string> Tags { get; set; } = new();

    /// <summary>
    /// Number of chunks created from this document.
    /// </summary>
    public int ChunkCount { get; set; }

    /// <summary>
    /// Optional error message if document processing failed.
    /// </summary>
    [StringLength(1000, ErrorMessage = "ErrorMessage cannot exceed 1000 characters.")]
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Timestamp when processing completed or failed.
    /// </summary>
    public DateTime? ProcessedAt { get; set; }

    /// <summary>
    /// Creates a new DocumentModel instance with the provided information.
    /// </summary>
    /// <param name="filePath">Full path to the document file.</param>
    /// <param name="fileName">Name of the document file.</param>
    /// <param name="fileType">File extension/type.</param>
    /// <param name="fileSize">Size of the document in bytes.</param>
    /// <param name="fileHash">SHA256 hash of the document content.</param>
    /// <param name="metadata">Optional document metadata.</param>
    /// <param name="tags">Optional list of tags.</param>
    /// <returns>A new DocumentModel instance.</returns>
    public static DocumentModel Create(
        string filePath,
        string fileName,
        string fileType,
        long fileSize,
        string fileHash,
        DocumentMetadata? metadata = null,
        List<string>? tags = null)
    {
        return new DocumentModel
        {
            FilePath = filePath,
            FileName = fileName,
            FileType = fileType,
            FileSize = fileSize,
            FileHash = fileHash,
            DocumentMetadata = metadata,
            Tags = tags ?? new List<string>()
        };
    }

    /// <summary>
    /// Enum representing the possible states of document processing.
    /// </summary>
    public enum DocumentStatus
    {
        /// <summary>
        /// Document is waiting to be processed.
        /// </summary>
        Pending,

        /// <summary>
        /// Document is currently being processed.
        /// </summary>
        Processing,

        /// <summary>
        /// Document has been successfully processed.
        /// </summary>
        Completed,

        /// <summary>
        /// Document processing failed.
        /// </summary>
        Failed
    }
}

/// <summary>
/// Contains metadata about a document such as title, author, and dates.
/// </summary>
public class DocumentMetadata
{
    /// <summary>
    /// Title of the document.
    /// </summary>
    [StringLength(500, ErrorMessage = "Title cannot exceed 500 characters.")]
    public string? Title { get; set; }

    /// <summary>
    /// Author or creator of the document.
    /// </summary>
    [StringLength(255, ErrorMessage = "Author cannot exceed 255 characters.")]
    public string? Author { get; set; }

    /// <summary>
    /// Date when the document was created.
    /// </summary>
    public DateTime? CreationDate { get; set; }

    /// <summary>
    /// Date when the document was last modified.
    /// </summary>
    public DateTime? LastModifiedDate { get; set; }

    /// <summary>
    /// Number of pages in the document (if applicable).
    /// </summary>
    [Range(0, int.MaxValue, ErrorMessage = "PageCount must be a positive number.")]
    public int? PageCount { get; set; }

    /// <summary>
    /// Optional dictionary for custom document metadata.
    /// </summary>
    public Dictionary<string, string>? CustomFields { get; set; }
}
