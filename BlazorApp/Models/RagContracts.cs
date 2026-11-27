using System.ComponentModel.DataAnnotations;

namespace BlazorApp.Models;

/// <summary>Typed request sent to the retrieval-augmented generation service.</summary>
public sealed record RagQueryRequest
{
    [Required]
    [StringLength(2000, MinimumLength = 1)]
    public string Query { get; init; } = string.Empty;

    [Range(1, 50)]
    public int MaxResults { get; init; } = 5;
    public string? SessionId { get; init; }

    /// <summary>Identifier used to correlate retrieval, model, and history records.</summary>
    public string CorrelationId { get; init; } = Guid.NewGuid().ToString("N");
}

/// <summary>Result states returned by the RAG pipeline.</summary>
public enum RagResultStatus
{
    Succeeded,
    NoResults,
    Failed,
    Cancelled,
    TimedOut
}

/// <summary>Typed RAG response. Sources are retained separately from answer text.</summary>
public sealed class RagResponse
{
    public RagResultStatus Status { get; init; }
    public string AnswerText { get; init; } = string.Empty;
    public IReadOnlyList<SourceReference> Sources { get; init; } = Array.Empty<SourceReference>();
    public RagModelMetadata ModelMetadata { get; init; } = new();
    public string? FailureCode { get; init; }
    public bool IsValid => Status == RagResultStatus.Succeeded;
}

/// <summary>Metadata describing the model and retrieval context used for a response.</summary>
public sealed class RagModelMetadata
{
    public string? ModelName { get; init; }
    public int ContextChunksUsed { get; init; }
    public DateTime RetrievedAtUtc { get; init; } = DateTime.UtcNow;
    public long ProcessingTimeMs { get; init; }
}
