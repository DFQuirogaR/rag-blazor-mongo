@echo off
echo ========================================
echo Estado RAG Blazor Application
echo ========================================
echo.

cd /d "%~dp0"

echo Contenedores en ejecucion:
echo ----------------------------------------
docker ps --filter "name=blazor" --filter "name=mongodb" --filter "name=ollama"

echo.
echo ========================================
echo Logs recientes de Blazor:
echo ========================================
docker logs blazor_rag_app --tail 50

echo.
echo ========================================
pause
