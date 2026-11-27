namespace BlazorApp.Models;

public enum HistoryExportFormat
{
    Json,
    Markdown
}

/// <summary>Portable export contract for a chat history session.</summary>
public sealed class HistoryExport
{
    public string SessionId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? OwnerUserId { get; set; }
    public string? Language { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime LastActiveUtc { get; set; }
    public DateTime ExportedAtUtc { get; set; } = DateTime.UtcNow;
    public List<HistoryExportEntry> Entries { get; set; } = new();
}

/// <summary>Export representation of one correlated query/response entry.</summary>
public sealed class HistoryExportEntry
{
    public string EntryId { get; set; } = string.Empty;
    public string Query { get; set; } = string.Empty;
    public string? Answer { get; set; }
    public string? ResponseLocale { get; set; }
    public DateTime QueryTimestampUtc { get; set; }
    public DateTime? AnswerTimestampUtc { get; set; }
    public string EntryStatus { get; set; } = "pending";
    public List<SourceReference> Sources { get; set; } = new();
    public Dictionary<string, string> ModelMetadata { get; set; } = new();
}

// Short aliases kept for callers that expose chat-specific export contracts.
public sealed class ChatExport
{
    public HistoryExport History { get; set; } = new();
    public HistoryExportFormat Format { get; set; }
}
