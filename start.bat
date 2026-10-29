@echo off
cd /d %~dp0
echo Iniciando servicios...
docker compose up --build
