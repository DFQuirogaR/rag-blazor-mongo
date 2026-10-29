@echo off
echo ========================================
echo Inspeccionando MongoDB - Base RAG
echo ========================================
echo.

echo Conectando a MongoDB...
echo.

docker exec -it mongodb_rag mongosh --eval "
use RagDatabase

print('=== DOCUMENTOS PROCESADOS ===');
db.documents.countDocuments().then(count => print('Total documentos: ' + count));
print('');

print('=== MUESTRA DE DOCUMENTOS ===');
db.documents.find().limit(3).forEach(doc => {
    print('- ' + doc.FileName + ' (' + doc.FileType + ')');
    print('  Estado: ' + doc.ProcessingStatus);
    print('  Procesado: ' + doc.ProcessedAt);
    if(doc.ErrorMessage) print('  Error: ' + doc.ErrorMessage);
    print('');
});

print('=== CHUNKS GENERADOS ===');
db.chunks.countDocuments().then(count => print('Total chunks: ' + count));
print('');

print('=== MUESTRA DE CHUNKS ===');
db.chunks.find().limit(2).forEach(chunk => {
    print('- Chunk #' + chunk.ChunkIndex + ' del doc: ' + chunk.DocumentId);
    print('  Contenido (primeros 100 chars): ' + chunk.Content.substring(0, 100) + '...');
    print('  Tiene embedding: ' + (chunk.Embedding ? 'SI ('+chunk.Embedding.length+' dimensiones)' : 'NO'));
    print('');
});
"

echo.
echo ========================================
pause
