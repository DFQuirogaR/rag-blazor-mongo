namespace BlazorApp.Models;

public class ChunkMetadata
{
    public string? ContentType { get; set; }
    public int? HeadingLevel { get; set; }
    public bool IsListItem { get; set; }
    public int? ListItemNumber { get; set; }
    public bool HasCode { get; set; }
    public string? Language { get; set; }
    public List<string> NamedEntities { get; set; } = new();
    public List<string> Topics { get; set; } = new();
}
