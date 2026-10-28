using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace BlazorApp.Models;

public class ChunkModel
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }
    
    public string DocumentId { get; set; } = string.Empty;
    public int ChunkIndex { get; set; }
    public int StartChar { get; set; }
    public int EndChar { get; set; }
    public string Content { get; set; } = string.Empty;
    public float[]? Embedding { get; set; }
    public DateTime CreatedAt { get; set; }
    
    // Optional metadata
    public int? PageNumber { get; set; }
    public ChunkMetadata? Metadata { get; set; }
}
