using Microsoft.AspNetCore.Identity;

namespace BlazorApp.Data;

public class ApplicationUser : IdentityUser
{
    public string? DisplayName { get; set; }
}
