@echo off
echo ========================================
echo DIAGNOSTICO COMPLETO DEL SISTEMA RAG
echo ========================================
echo.

echo [1/5] Estado de contenedores...
echo ----------------------------------------
docker ps --filter "name=blazor" --filter "name=mongodb" --filter "name=ollama" --format "table {{.Names}}\t{{.Status}}\t{{.Ports}}"
echo.

echo [2/5] Verificando Ollama...
echo ----------------------------------------
echo Probando conexion HTTP a Ollama:
curl -s http://localhost:11434/api/tags
echo.
echo.

echo [3/5] Modelos instalados en Ollama...
echo ----------------------------------------
docker exec ollama_rag ollama list
echo.

echo [4/5] Logs recientes de Ollama...
echo ----------------------------------------
docker logs ollama_rag --tail 20
echo.

echo [5/5] Logs recientes de BlazorApp...
echo ----------------------------------------
docker logs blazor_rag_app --tail 30
echo.

echo ========================================
echo DIAGNOSTICO COMPLETO
echo ========================================
echo.
echo Si Ollama no responde o no tiene modelos:
echo   1. Espera 5-10 minutos mas
echo   2. O ejecuta manualmente:
echo      docker exec -it ollama_rag ollama pull llama3.2
echo      docker exec -it ollama_rag ollama pull nomic-embed-text
echo.
pause
