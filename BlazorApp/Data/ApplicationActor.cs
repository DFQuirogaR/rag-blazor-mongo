namespace BlazorApp.Data;

public sealed record ApplicationActor(
    string UserId,
    string? Email,
    string? DisplayName,
    IReadOnlyCollection<string> Roles);
