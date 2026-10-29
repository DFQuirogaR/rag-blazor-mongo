using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.ComponentModel.DataAnnotations;

namespace BlazorApp.Models;

/// <summary>
/// Model for chat sessions.
/// Each session represents a conversation with multiple messages.
/// </summary>
public class ChatSession
{
    /// <summary>
    /// Unique identifier for the session.
    /// Uses Guid instead of ObjectId for distributed uniqueness.
    /// </summary>
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = ObjectId.GenerateNewId().ToString();

    /// <summary>
    /// Title of the conversation session.
    /// </summary>
    [Required]
    [StringLength(200, ErrorMessage = "Title cannot exceed 200 characters.")]
    public string Title { get; set; } = "Nueva conversación";

    /// <summary>
    /// List of messages in this session.
    /// </summary>
    public List<ChatMessage> Messages { get; set; } = new();

    /// <summary>
    /// Timestamp when the session was created.
    /// </summary>
    [Required]
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Timestamp of the last activity in this session.
    /// </summary>
    [Required]
    public DateTime LastActive { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Optional user ID who owns this session.
    /// </summary>
    [BsonRepresentation(BsonType.ObjectId)]
    public string? UserId { get; set; }

    /// <summary>
    /// Optional metadata about the session.
    /// </summary>
    public SessionMetadata? Metadata { get; set; }

    /// <summary>
    /// Creates a new chat session instance.
    /// </summary>
    /// <param name="title">The session title.</param>
    /// <param name="userId">Optional user ID.</param>
    /// <param name="tags">Optional list of tags for categorization.</param>
    /// <returns>A new ChatSession instance.</returns>
    public static ChatSession Create(string title = "Nueva conversación", string? userId = null, List<string>? tags = null)
    {
        return new ChatSession
        {
            Title = title,
            UserId = userId,
            CreatedDate = DateTime.UtcNow,
            LastActive = DateTime.UtcNow,
            Metadata = new SessionMetadata
            {
                Tags = tags ?? new List<string>()
            }
        };
    }

    /// <summary>
    /// Updates the session's last active timestamp.
    /// </summary>
    public void UpdateLastActive()
    {
        LastActive = DateTime.UtcNow;
    }

    /// <summary>
    /// Adds a message to this session.
    /// </summary>
    /// <param name="message">The message to add.</param>
    public void AddMessage(ChatMessage message)
    {
        if (message == null)
            throw new ArgumentNullException(nameof(message));

        message.SessionId = Id;
        Messages.Add(message);
        UpdateLastActive();
    }

    /// <summary>
    /// Generates a title from the first user message content.
    /// </summary>
    /// <param name="content">The content to generate title from.</param>
    /// <param name="maxLength">Maximum length of the title.</param>
    public void GenerateTitleFromContent(string content, int maxLength = 50)
    {
        if (string.IsNullOrWhiteSpace(content))
            return;

        Title = content.Length > maxLength 
            ? content.Substring(0, maxLength) + "..." 
            : content;
    }

    /// <summary>
    /// Gets the conversation history as a formatted string.
    /// </summary>
    /// <param name="maxMessages">Maximum number of messages to include.</param>
    /// <returns>A formatted string containing the conversation history.</returns>
    public string GetConversationHistory(int maxMessages = 10)
    {
        var recentMessages = Messages
            .OrderByDescending(m => m.Timestamp)
            .Take(maxMessages)
            .Reverse()
            .ToList();

        var history = new System.Text.StringBuilder();
        foreach (var message in recentMessages)
        {
            history.AppendLine($"[{message.Role.ToUpper()}] {message.Timestamp:yyyy-MM-dd HH:mm:ss}");
            history.AppendLine(message.Content);
            history.AppendLine("---");
        }

        return history.ToString();
    }

    /// <summary>
    /// Exports this session to a JSON string.
    /// </summary>
    /// <returns>A JSON string representation of this session.</returns>
    public string ToJson()
    {
        return System.Text.Json.JsonSerializer.Serialize(this, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
        });
    }
}

/// <summary>
/// Metadata for a chat session.
/// </summary>
public class SessionMetadata
{
    /// <summary>
    /// List of tags for categorizing sessions.
    /// </summary>
    public List<string> Tags { get; set; } = new();

    /// <summary>
    /// Optional language preference for the session.
    /// </summary>
    public string? Language { get; set; }

    /// <summary>
    /// Optional custom properties.
    /// </summary>
    public Dictionary<string, string>? CustomProperties { get; set; }
}
