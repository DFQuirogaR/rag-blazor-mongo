Write-Host "=== Limpieza y Reconstrucción Completa ===" -ForegroundColor Cyan
Write-Host ""

# Stop and remove containers
Write-Host "1. Deteniendo y eliminando contenedores..." -ForegroundColor Yellow
docker-compose down -v 2>$null

# Remove images
Write-Host "2. Eliminando imágenes..." -ForegroundColor Yellow
docker rmi rag-blazor-mongo-blazorapp 2>$null
docker rmi rag-blazor-mongo-ollama 2>$null

# Clean Docker build cache
Write-Host "3. Limpiando cache de Docker..." -ForegroundColor Yellow
docker builder prune -f

# Build without cache
Write-Host "4. Construyendo sin cache..." -ForegroundColor Green
docker-compose build --no-cache

# Start services
Write-Host "5. Iniciando servicios..." -ForegroundColor Green
docker-compose up -d

# Wait
Write-Host "6. Esperando inicialización..." -ForegroundColor Cyan
Start-Sleep -Seconds 5

# Show status
Write-Host ""
Write-Host "Estado final:" -ForegroundColor Cyan
docker-compose ps

Write-Host ""
Write-Host "=== Aplicación lista ===" -ForegroundColor Green
Write-Host "URL: http://localhost:8080" -ForegroundColor White
Write-Host "Chat: http://localhost:8080/chat" -ForegroundColor White
Write-Host "Documentos: http://localhost:8080/documents" -ForegroundColor White
Write-Host ""
Write-Host "Para ver logs: docker-compose logs -f blazorapp" -ForegroundColor Yellow
