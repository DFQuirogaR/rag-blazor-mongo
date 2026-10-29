namespace BlazorApp.Models;

public class ChatMessageModel
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string SessionId { get; set; } = string.Empty;
    public int MessageIndex { get; set; }
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public List<string> Sources { get; set; } = new();
    public MessageMetadata? Metadata { get; set; }
    public long ProcessingTimeMs { get; set; }
    public bool IsProcessing { get; set; }
    public bool HasError { get; set; }
    public string? ErrorMessage { get; set; }

    public static ChatMessageModel CreateUserMessage(string content, string sessionId, int index)
    {
        return new ChatMessageModel
        {
            Role = "user",
            Content = content,
            SessionId = sessionId,
            MessageIndex = index,
            Timestamp = DateTime.UtcNow
        };
    }

    public static ChatMessageModel CreateAssistantMessage(string content, string sessionId, int index, List<string>? sources = null)
    {
        return new ChatMessageModel
        {
            Role = "assistant",
            Content = content,
            SessionId = sessionId,
            MessageIndex = index,
            Timestamp = DateTime.UtcNow,
            Sources = sources ?? new List<string>()
        };
    }

    public static ChatMessageModel CreateErrorMessage(string errorMessage, string sessionId, int index)
    {
        return new ChatMessageModel
        {
            Role = "assistant",
            Content = $"Error: {errorMessage}",
            SessionId = sessionId,
            MessageIndex = index,
            Timestamp = DateTime.UtcNow,
            HasError = true,
            ErrorMessage = errorMessage
        };
    }

    public static ChatMessageModel CreateProcessingMessage(string sessionId, int index)
    {
        return new ChatMessageModel
        {
            Role = "assistant",
            Content = "Procesando...",
            SessionId = sessionId,
            MessageIndex = index,
            Timestamp = DateTime.UtcNow,
            IsProcessing = true
        };
    }
}
