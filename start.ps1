Write-Host "=== Iniciando RAG Blazor App ===" -ForegroundColor Cyan
Write-Host ""

# Stop existing containers
Write-Host "Deteniendo contenedores existentes..." -ForegroundColor Yellow
docker-compose down

# Build and start
Write-Host "Compilando y levantando servicios..." -ForegroundColor Green
docker-compose up --build -d

# Wait a moment
Start-Sleep -Seconds 3

# Show status
Write-Host ""
Write-Host "Estado de contenedores:" -ForegroundColor Cyan
docker-compose ps

Write-Host ""
Write-Host "=== Aplicación lista ===" -ForegroundColor Green
Write-Host "URL: http://localhost:8080" -ForegroundColor White
Write-Host "Chat: http://localhost:8080/chat" -ForegroundColor White
Write-Host "Documentos: http://localhost:8080/documents" -ForegroundColor White
Write-Host ""
Write-Host "Para ver logs: docker-compose logs -f blazorapp" -ForegroundColor Yellow
