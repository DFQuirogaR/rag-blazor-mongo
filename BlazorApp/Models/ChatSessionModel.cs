namespace BlazorApp.Models;

public class ChatSessionModel
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string? UserId { get; set; }
    public string Title { get; set; } = "Nueva conversación";
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime LastActivityDate { get; set; } = DateTime.UtcNow;
    public int MessageCount { get; set; }
    public string Status { get; set; } = "Active";
    public List<string> Tags { get; set; } = new();
    
    public void GenerateTitle(string query)
    {
        Title = query.Length > 50 ? query.Substring(0, 50) + "..." : query;
    }
    
    public void UpdateActivity()
    {
        LastActivityDate = DateTime.UtcNow;
    }
}
