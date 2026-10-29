Write-Host "========================================"
Write-Host "Verificando errores del sistema"
Write-Host "========================================"
Write-Host ""

Write-Host "[1] Estado de Ollama:" -ForegroundColor Cyan
try {
    $response = Invoke-WebRequest -Uri "http://localhost:11434/api/tags" -UseBasicParsing -TimeoutSec 5
    Write-Host "✅ Ollama responde correctamente" -ForegroundColor Green
    $response.Content | ConvertFrom-Json | ForEach-Object { 
        Write-Host "  Modelos disponibles:" -ForegroundColor Yellow
        $_.models | ForEach-Object { Write-Host "    - $($_.name)" }
    }
} catch {
    Write-Host "❌ Ollama NO responde: $($_.Exception.Message)" -ForegroundColor Red
}
Write-Host ""

Write-Host "[2] Logs de BlazorApp (últimos errores):" -ForegroundColor Cyan
docker logs blazor_rag_app --tail 50 | Select-String -Pattern "error|exception|fail" -CaseSensitive:$false
Write-Host ""

Write-Host "[3] Verificando modelos en Ollama:" -ForegroundColor Cyan
docker exec ollama_rag ollama list
Write-Host ""

Write-Host "[4] Prueba rápida de embedding:" -ForegroundColor Cyan
Write-Host "Generando embedding de prueba con Ollama..."
try {
    $body = @{
        model = "nomic-embed-text"
        prompt = "test"
    } | ConvertTo-Json
    
    $result = Invoke-WebRequest -Uri "http://localhost:11434/api/embeddings" -Method Post -Body $body -ContentType "application/json" -UseBasicParsing -TimeoutSec 10
    $embedding = ($result.Content | ConvertFrom-Json).embedding
    Write-Host "✅ Embedding generado correctamente ($($embedding.Length) dimensiones)" -ForegroundColor Green
} catch {
    Write-Host "❌ Error generando embedding: $($_.Exception.Message)" -ForegroundColor Red
}
Write-Host ""

Write-Host "========================================"
