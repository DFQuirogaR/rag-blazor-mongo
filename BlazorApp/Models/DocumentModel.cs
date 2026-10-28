using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace BlazorApp.Models;

public class DocumentModel
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }
    
    public string FilePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string FileType { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string ContentHash { get; set; } = string.Empty;
    public DateTime ProcessedAt { get; set; }
    public string ProcessingStatus { get; set; } = "pending";
    public string? ErrorMessage { get; set; }
    
    // Additional metadata
    public int ChunkCount { get; set; }
    public DateTime? LastModifiedDate { get; set; }
    public int? PageCount { get; set; }
    public Dictionary<string, string>? Metadata { get; set; }
}
