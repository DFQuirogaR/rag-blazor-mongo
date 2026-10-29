@echo off
echo ========================================
echo DIAGNOSTICO DE INICIO
echo ========================================
echo.

echo [1] Estado actual de contenedores:
echo ----------------------------------------
docker-compose ps
echo.

echo [2] Logs de BlazorApp (errores de inicio):
echo ----------------------------------------
docker logs blazor_rag_app
echo.

echo [3] Intentando iniciar manualmente:
echo ----------------------------------------
docker start blazor_rag_app
timeout /t 3 /nobreak >nul
docker logs blazor_rag_app --tail 30
echo.

echo ========================================
pause
