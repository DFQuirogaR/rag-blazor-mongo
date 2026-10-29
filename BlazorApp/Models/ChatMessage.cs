using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.ComponentModel.DataAnnotations;

namespace BlazorApp.Models;

/// <summary>
/// Model for individual chat messages.
/// Each message belongs to a session and has a role (user/assistant) and content.
/// </summary>
public class ChatMessage
{
    /// <summary>
    /// Unique identifier for the message.
    /// Uses Guid instead of ObjectId for distributed uniqueness.
    /// </summary>
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = ObjectId.GenerateNewId().ToString();

    /// <summary>
    /// Reference to the parent ChatSession.
    /// </summary>
    [Required]
    [BsonRepresentation(BsonType.ObjectId)]
    public string SessionId { get; set; } = ObjectId.GenerateNewId().ToString();

    /// <summary>
    /// Role of the message sender (user or assistant).
    /// </summary>
    [Required]
    [StringLength(20, ErrorMessage = "Role cannot exceed 20 characters.")]
    public string Role { get; set; } = string.Empty;

    /// <summary>
    /// Content of the message.
    /// </summary>
    [Required]
    [StringLength(100000, ErrorMessage = "Content cannot exceed 100000 characters.")]
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp when the message was created.
    /// </summary>
    [Required]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Source documents referenced in this message (for RAG context).
    /// </summary>
    public List<string>? SourceDocuments { get; set; }

    /// <summary>
    /// Optional metadata about the message.
    /// </summary>
    public MessageMetadata? Metadata { get; set; }

    /// <summary>
    /// Creates a new user message instance.
    /// </summary>
    /// <param name="content">The message content.</param>
    /// <param name="sessionId">The parent session ID.</param>
    /// <param name="sourceDocuments">Optional list of source document IDs.</param>
    /// <returns>A new ChatMessage instance representing a user message.</returns>
    public static ChatMessage CreateUserMessage(string content, string sessionId, List<string>? sourceDocuments = null)
    {
        return new ChatMessage
        {
            Role = "user",
            Content = content,
            SessionId = sessionId,
            Timestamp = DateTime.UtcNow,
            SourceDocuments = sourceDocuments
        };
    }

    /// <summary>
    /// Creates a new assistant message instance.
    /// </summary>
    /// <param name="content">The message content.</param>
    /// <param name="sessionId">The parent session ID.</param>
    /// <param name="sourceDocuments">Optional list of source document IDs.</param>
    /// <param name="metadata">Optional metadata about the message.</param>
    /// <returns>A new ChatMessage instance representing an assistant message.</returns>
    public static ChatMessage CreateAssistantMessage(string content, string sessionId, List<string>? sourceDocuments = null, MessageMetadata? metadata = null)
    {
        return new ChatMessage
        {
            Role = "assistant",
            Content = content,
            SessionId = sessionId,
            Timestamp = DateTime.UtcNow,
            SourceDocuments = sourceDocuments,
            Metadata = metadata
        };
    }

    /// <summary>
    /// Creates a new system/error message instance.
    /// </summary>
    /// <param name="content">The message content.</param>
    /// <param name="sessionId">The parent session ID.</param>
    /// <returns>A new ChatMessage instance representing a system message.</returns>
    public static ChatMessage CreateSystemMessage(string content, string sessionId)
    {
        return new ChatMessage
        {
            Role = "system",
            Content = content,
            SessionId = sessionId,
            Timestamp = DateTime.UtcNow
        };
    }
}

/// <summary>
/// Metadata for a chat message.
/// </summary>
public class MessageMetadata
{
    /// <summary>
    /// Name of the LLM model used to generate the response.
    /// </summary>
    public string? ModelName { get; set; }

    /// <summary>
    /// Confidence score of the response (0-1).
    /// </summary>
    public double? ConfidenceScore { get; set; }

    /// <summary>
    /// Number of context chunks used for this message.
    /// </summary>
    public int? ContextChunksUsed { get; set; }

    /// <summary>
    /// Average similarity score of context chunks.
    /// </summary>
    public double? AverageContextSimilarity { get; set; }

    /// <summary>
    /// List of source document IDs referenced.
    /// </summary>
    public List<string>? SourceDocuments { get; set; }

    /// <summary>
    /// Number of tokens used for this message.
    /// </summary>
    public int? TokensUsed { get; set; }

    /// <summary>
    /// Optional custom metadata.
    /// </summary>
    public Dictionary<string, string>? CustomMetadata { get; set; }
}
