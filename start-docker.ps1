#!/usr/bin/env pwsh

# ==========================================
# Script de inicio con Docker Compose
# ==========================================
# Inicia todos los servicios del sistema RAG
# ==========================================

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Inicio del sistema RAG con Docker Compose" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Verificar Docker
Write-Host "Verificando Docker..." -ForegroundColor Yellow
if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
    Write-Host "ERROR: Docker no está instalado o no está en el PATH" -ForegroundColor Red
    exit 1
}

# Verificar Docker Compose
Write-Host "Verificando Docker Compose..." -ForegroundColor Yellow
docker compose version | Out-Null
if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: Docker Compose no está disponible" -ForegroundColor Red
    exit 1
}

Write-Host "✓ Docker y Docker Compose están disponibles" -ForegroundColor Green
Write-Host ""

# Verificar archivo docker-compose.yml
if (-not (Test-Path "docker-compose.yml")) {
    Write-Host "ERROR: No se encontró docker-compose.yml" -ForegroundColor Red
    exit 1
}

# Preguntar si se deben reconstruir las imágenes
$rebuild = Read-Host "¿Reconstruir imágenes? (y/n) [n]"
$buildArgs = ""
if ($rebuild -eq "y" -or $rebuild -eq "Y") {
    $buildArgs = "--build"
}

# Crear red si no existe
Write-Host "Verificando red Docker..." -ForegroundColor Yellow
docker network ls | Select-String "rag-network" | Out-Null
if ($LASTEXITCODE -ne 0) {
    Write-Host "Creando red rag-network..." -ForegroundColor Yellow
    docker network create rag-network | Out-Null
}

# Iniciar servicios
Write-Host ""
Write-Host "Iniciando servicios..." -ForegroundColor Yellow
Write-Host "Este proceso puede tomar varios minutos..." -ForegroundColor Yellow
Write-Host ""

docker compose up -d $buildArgs

if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR al iniciar servicios" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host "Sistema RAG iniciado correctamente" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""

# Esperar a que todos los servicios estén listos
Write-Host "Esperando a que todos los servicios estén listos..." -ForegroundColor Yellow
Write-Host ""

# Verificar MongoDB
Write-Host "Verificando MongoDB..." -ForegroundColor Yellow
for ($i = 0; $i -lt 30; $i++) {
    $result = docker compose exec -T mongodb mongosh --eval "db.adminCommand('ping')" 2>&1
    if ($LASTEXITCODE -eq 0) {
        Write-Host "✓ MongoDB está listo" -ForegroundColor Green
        break
    }
    if ($i -eq 29) {
        Write-Host "✗ MongoDB no está listo después de 150 segundos" -ForegroundColor Red
    }
    Start-Sleep -Seconds 5
}

# Verificar Ollama
Write-Host "Verificando Ollama..." -ForegroundColor Yellow
for ($i = 0; $i -lt 30; $i++) {
    $result = Invoke-WebRequest -Uri "http://localhost:11434/api/version" -UseBasicParsing -ErrorAction SilentlyContinue
    if ($result.StatusCode -eq 200) {
        Write-Host "✓ Ollama está listo" -ForegroundColor Green
        break
    }
    if ($i -eq 29) {
        Write-Host "✗ Ollama no está listo después de 150 segundos" -ForegroundColor Red
    }
    Start-Sleep -Seconds 5
}

# Verificar Blazor App
Write-Host "Verificando Blazor App..." -ForegroundColor Yellow
for ($i = 0; $i -lt 30; $i++) {
    $result = Invoke-WebRequest -Uri "http://localhost:8080/health" -UseBasicParsing -ErrorAction SilentlyContinue
    if ($result.StatusCode -eq 200) {
        Write-Host "✓ Blazor App está listo" -ForegroundColor Green
        break
    }
    if ($i -eq 29) {
        Write-Host "✗ Blazor App no está listo después de 150 segundos" -ForegroundColor Red
    }
    Start-Sleep -Seconds 5
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host "Todos los servicios están listos" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""

# Mostrar URLs
Write-Host "URLs de acceso:" -ForegroundColor Cyan
Write-Host "  Blazor App:  http://localhost:8080" -ForegroundColor White
Write-Host "  MongoDB:     mongodb://localhost:27017" -ForegroundColor White
Write-Host "  Ollama:      http://localhost:11434" -ForegroundColor White
Write-Host ""

# Mostrar estado
Write-Host "Estado de los contenedores:" -ForegroundColor Cyan
docker compose ps

Write-Host ""
Write-Host "Para ver los logs: docker-compose logs -f" -ForegroundColor Yellow
Write-Host "Para detener:      docker-compose down" -ForegroundColor Yellow
Write-Host ""

exit 0