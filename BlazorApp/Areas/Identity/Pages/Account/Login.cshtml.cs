using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using BlazorApp.Data;

namespace BlazorApp.Areas.Identity.Pages.Account;

[AllowAnonymous]
public class LoginModel : PageModel
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly ILogger<LoginModel> _logger;
    private readonly IConfiguration _configuration;

    public LoginModel(
        SignInManager<ApplicationUser> signInManager,
        ILogger<LoginModel> logger,
        IConfiguration configuration)
    {
        _signInManager = signInManager;
        _logger = logger;
        _configuration = configuration;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    public void OnGet()
    {
        ViewData["BrandName"] = _configuration["Brand:Name"] ?? "Nombre de la aplicación";
        ViewData["BrandSubtitle"] = _configuration["Brand:Subtitle"] ?? "Portal de conocimiento";
    }

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        ReturnUrl ??= returnUrl;
        ViewData["BrandName"] = _configuration["Brand:Name"] ?? "Nombre de la aplicación";
        ViewData["BrandSubtitle"] = _configuration["Brand:Subtitle"] ?? "Portal de conocimiento";

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var email = Input.Email.Trim().ToLowerInvariant();
        var result = await _signInManager.PasswordSignInAsync(
            email,
            Input.Password,
            Input.RememberMe,
            lockoutOnFailure: true);

        if (result.Succeeded)
        {
            _logger.LogInformation("Usuario local autenticado: {Email}", email);
            return LocalRedirect(GetSafeReturnUrl());
        }

        if (result.IsLockedOut)
        {
            ModelState.AddModelError(string.Empty, "La cuenta está bloqueada temporalmente.");
        }
        else
        {
            ModelState.AddModelError(string.Empty, "El correo o la contraseña no son válidos.");
        }

        return Page();
    }

    private string GetSafeReturnUrl()
    {
        return !string.IsNullOrWhiteSpace(ReturnUrl) && Url.IsLocalUrl(ReturnUrl)
            ? ReturnUrl
            : "/";
    }

    public sealed class InputModel
    {
        [Required(ErrorMessage = "El correo electrónico es obligatorio.")]
        [EmailAddress(ErrorMessage = "Introduce un correo electrónico válido.")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "La contraseña es obligatoria.")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [Display(Name = "Recordarme")]
        public bool RememberMe { get; set; }
    }
}
