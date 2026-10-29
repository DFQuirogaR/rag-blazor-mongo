@echo off
echo ========================================
echo Limpieza y Reconstruccion RAG Blazor
echo ========================================
echo.

cd /d "%~dp0"

echo [1/5] Deteniendo contenedores...
docker-compose down -v

echo.
echo [2/5] Eliminando imagenes antiguas...
docker rmi rag-blazor-mongo-blazor-app -f 2>nul
docker builder prune -f

echo.
echo [3/5] Construyendo sin cache (esto puede tardar varios minutos)...
docker-compose build --no-cache

echo.
echo [4/5] Iniciando servicios...
docker-compose up -d

echo.
echo [5/5] Esperando inicializacion...
timeout /t 10 /nobreak >nul

echo.
echo ========================================
echo Estado de los servicios:
echo ========================================
docker-compose ps

echo.
echo ========================================
echo APLICACION LISTA
echo ========================================
echo URL Principal: http://localhost:8080
echo Chat RAG:       http://localhost:8080/chat
echo Documentos:     http://localhost:8080/documents
echo.
echo Para ver logs: docker-compose logs -f blazor-app
echo ========================================
pause
