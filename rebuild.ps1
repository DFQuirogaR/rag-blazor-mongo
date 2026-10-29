# Stop any running containers
Write-Host "Stopping containers..." -ForegroundColor Yellow
docker-compose down 2>$null

# Remove old images to force rebuild
Write-Host "Removing old images..." -ForegroundColor Yellow
docker rmi rag-blazor-mongo-blazorapp 2>$null

# Build and start
Write-Host "Building and starting containers..." -ForegroundColor Green
docker-compose up --build -d

# Wait a moment
Start-Sleep -Seconds 5

# Show status
Write-Host "`nContainer status:" -ForegroundColor Cyan
docker ps

Write-Host "`nApplication should be available at: http://localhost:8080" -ForegroundColor Green
Write-Host "Chat interface: http://localhost:8080/chat" -ForegroundColor Green
