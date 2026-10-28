namespace BlazorApp.Models;

public class EmbeddingCache
{
    public string? Id { get; set; }
    public string QueryHash { get; set; } = string.Empty;
    public string QueryText { get; set; } = string.Empty;
    public float[] Embedding { get; set; } = Array.Empty<float>();
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime ExpirationDate { get; set; }
    public int HitCount { get; set; }
    public int UsageCount { get; set; }
    public DateTime? LastUsedDate { get; set; }
}
