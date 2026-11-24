using BlazorApp.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace BlazorApp.Services;

public sealed class DevelopmentIdentitySeeder
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly AuthenticationOptions _options;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<DevelopmentIdentitySeeder> _logger;

    public DevelopmentIdentitySeeder(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        IOptions<AuthenticationOptions> options,
        IHostEnvironment environment,
        ILogger<DevelopmentIdentitySeeder> logger)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _options = options.Value;
        _environment = environment;
        _logger = logger;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        if (!_environment.IsDevelopment() || !_options.SeedDevelopmentUsers)
        {
            return;
        }

        foreach (var role in new[]
        {
            ApplicationRoles.Administrator,
            ApplicationRoles.DocumentAuditor,
            ApplicationRoles.N1Agent
        })
        {
            if (!await _roleManager.RoleExistsAsync(role))
            {
                var result = await _roleManager.CreateAsync(new IdentityRole(role));
                EnsureSuccess(result, $"crear el rol {role}");
            }
        }

        await SeedUserAsync(_options.Admin, ApplicationRoles.Administrator, cancellationToken);
        await SeedUserAsync(_options.Auditor, ApplicationRoles.DocumentAuditor, cancellationToken);
        await SeedUserAsync(_options.N1, ApplicationRoles.N1Agent, cancellationToken);
    }

    private async Task SeedUserAsync(
        LocalUserSeedOptions options,
        string role,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(options.Email) || string.IsNullOrWhiteSpace(options.Password))
        {
            _logger.LogWarning("No se creó la cuenta de desarrollo para {Role}: faltan correo o contraseña", role);
            return;
        }

        var email = options.Email.Trim().ToLowerInvariant();
        var user = await _userManager.FindByEmailAsync(email);
        if (user == null)
        {
            user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                DisplayName = options.DisplayName
            };

            var createResult = await _userManager.CreateAsync(user, options.Password);
            EnsureSuccess(createResult, $"crear la cuenta {email}");
        }

        if (!await _userManager.IsInRoleAsync(user, role))
        {
            var roleResult = await _userManager.AddToRoleAsync(user, role);
            EnsureSuccess(roleResult, $"asignar el rol {role} a {email}");
        }

        _logger.LogInformation("Cuenta de desarrollo disponible para {Email} con rol {Role}", email, role);
    }

    private static void EnsureSuccess(IdentityResult result, string operation)
    {
        if (!result.Succeeded)
        {
            var errors = string.Join("; ", result.Errors.Select(error => error.Description));
            throw new InvalidOperationException($"No se pudo {operation}: {errors}");
        }
    }
}
