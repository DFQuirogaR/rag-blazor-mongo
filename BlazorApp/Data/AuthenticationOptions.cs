namespace BlazorApp.Data;

public sealed class AuthenticationOptions
{
    public bool SeedDevelopmentUsers { get; set; }
    public LocalUserSeedOptions Admin { get; set; } = new();
    public LocalUserSeedOptions Auditor { get; set; } = new();
    public LocalUserSeedOptions N1 { get; set; } = new();
}

public sealed class LocalUserSeedOptions
{
    public string? Email { get; set; }
    public string? Password { get; set; }
    public string? DisplayName { get; set; }
}
