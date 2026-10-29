Write-Host "========================================"
Write-Host "Inspeccionando MongoDB - Base RAG"
Write-Host "========================================"
Write-Host ""

Write-Host "Conectando a MongoDB..." -ForegroundColor Yellow
Write-Host ""

# Contar documentos
$docCount = docker exec mongodb_rag mongosh --quiet --eval "use RagDatabase; db.documents.countDocuments()"
Write-Host "Total documentos: $docCount" -ForegroundColor Cyan

# Contar chunks
$chunkCount = docker exec mongodb_rag mongosh --quiet --eval "use RagDatabase; db.chunks.countDocuments()"
Write-Host "Total chunks: $chunkCount" -ForegroundColor Cyan
Write-Host ""

if ($docCount -gt 0) {
    Write-Host "=== MUESTRA DE DOCUMENTOS ===" -ForegroundColor Green
    docker exec mongodb_rag mongosh --quiet --eval "use RagDatabase; db.documents.find().limit(3).forEach(d => print(d.FileName + ' - ' + d.ProcessingStatus))"
    Write-Host ""
}

if ($chunkCount -gt 0) {
    Write-Host "=== CHUNKS CON EMBEDDINGS ===" -ForegroundColor Green
    docker exec mongodb_rag mongosh --quiet --eval "use RagDatabase; db.chunks.find().limit(2).forEach(c => print('Chunk ' + c.ChunkIndex + ': ' + (c.Embedding ? c.Embedding.length + ' dims' : 'SIN embedding')))"
    Write-Host ""
} else {
    Write-Host "⚠️ NO HAY CHUNKS - El procesamiento falló" -ForegroundColor Red
    Write-Host "Verifica que Ollama esté funcionando y vuelve a escanear" -ForegroundColor Yellow
    Write-Host ""
}

Write-Host "========================================"
