#!/usr/bin/env pwsh

Write-Host "=== Construyendo y ejecutando RAG Blazor App ===" -ForegroundColor Cyan
Write-Host ""

Set-Location $PSScriptRoot

Write-Host "[1/3] Deteniendo contenedores existentes..." -ForegroundColor Yellow
docker-compose down 2>&1 | Out-Null

Write-Host "[2/3] Construyendo imagen Docker..." -ForegroundColor Yellow
docker-compose build 2>&1 | ForEach-Object { Write-Host $_ }

if ($LASTEXITCODE -ne 0) {
    Write-Host ""
    Write-Host "ERROR: La construccion fallo. Revisa los errores arriba." -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "[3/3] Iniciando servicios..." -ForegroundColor Yellow
docker-compose up -d

Write-Host ""
Write-Host "=== Completado ===" -ForegroundColor Green
Write-Host "Aplicacion disponible en: http://localhost:8080" -ForegroundColor Green
Write-Host ""
Write-Host "Para ver logs: docker-compose logs -f blazor-app" -ForegroundColor Cyan
