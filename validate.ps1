Write-Host "Validando estructura del proyecto..." -ForegroundColor Cyan

$errors = @()

# Archivos críticos
$criticalFiles = @(
    "BlazorApp\BlazorApp.csproj",
    "BlazorApp\Program.cs",
    "BlazorApp\Dockerfile",
    "BlazorApp\Components\App.razor",
    "BlazorApp\Components\Routes.razor",
    "BlazorApp\Components\_Imports.razor",
    "BlazorApp\Components\Pages\Home.razor",
    "BlazorApp\Components\Pages\Chat.razor",
    "BlazorApp\Components\Layout\MainLayout.razor",
    "BlazorApp\Components\Layout\NavMenu.razor",
    "BlazorApp\wwwroot\app.css",
    "docker-compose.yml"
)

foreach ($file in $criticalFiles) {
    if (!(Test-Path $file)) {
        $errors += "FALTA: $file"
        Write-Host "✗ $file" -ForegroundColor Red
    } else {
        Write-Host "✓ $file" -ForegroundColor Green
    }
}

if ($errors.Count -gt 0) {
    Write-Host "`nSe encontraron $($errors.Count) archivos faltantes:" -ForegroundColor Red
    $errors | ForEach-Object { Write-Host "  $_" }
    exit 1
} else {
    Write-Host "`n✓ Todos los archivos críticos están presentes" -ForegroundColor Green
    Write-Host "`nPuedes ejecutar: docker compose build" -ForegroundColor Cyan
}
