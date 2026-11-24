using BlazorApp.Data;
using Microsoft.AspNetCore.Components.Authorization;

namespace BlazorApp.Services;

public sealed class CurrentUserContext
{
    private readonly AuthenticationStateProvider _authenticationStateProvider;

    public CurrentUserContext(AuthenticationStateProvider authenticationStateProvider)
    {
        _authenticationStateProvider = authenticationStateProvider;
    }

    public async Task<ApplicationActor?> GetActorAsync()
    {
        var state = await _authenticationStateProvider.GetAuthenticationStateAsync();
        var principal = state.User;
        if (principal.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        var userId = principal.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(userId))
        {
            return null;
        }

        return new ApplicationActor(
            userId,
            principal.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value
                ?? principal.Identity.Name,
            principal.FindFirst("DisplayName")?.Value
                ?? principal.Identity.Name,
            principal.FindAll(System.Security.Claims.ClaimTypes.Role)
                .Select(claim => claim.Value)
                .Distinct(StringComparer.Ordinal)
                .ToArray());
    }
}
