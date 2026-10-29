@echo off
echo ========================================
echo Verificando estado de Ollama
echo ========================================
echo.

echo Verificando si Ollama responde...
curl -s http://localhost:11434/api/tags

echo.
echo.
echo Si ves una lista de modelos arriba, Ollama esta listo.
echo.
echo Modelos esperados:
echo  - llama3.2
echo  - nomic-embed-text
echo.
echo Si no ves modelos, espera 5-10 minutos mas.
echo Los modelos son grandes y tardan en descargarse.
echo.
pause
