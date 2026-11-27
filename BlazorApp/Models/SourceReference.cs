using MongoDB.Bson.Serialization.Attributes;

namespace BlazorApp.Models;

/// <summary>Structured historical reference to a document returned by RAG.</summary>
public sealed class SourceReference
{
    [BsonElement("DocumentId")]
    public string DocumentId { get; set; } = string.Empty;

    [BsonElement("StoredDocumentName")]
    public string StoredDocumentName { get; set; } = string.Empty;

    public int? Page { get; set; }
    public string? Section { get; set; }
    public string? Location { get; set; }
    public string? Quote { get; set; }
    public DateTime? RetrievedAtUtc { get; set; }
    public double? Relevance { get; set; }

    /// <summary>Whether the referenced document is available now; historical data is retained when false.</summary>
    public bool? IsCurrentlyAvailable { get; set; }
}
