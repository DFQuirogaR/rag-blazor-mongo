@echo off
title MongoDB RAG Setup Script
color 0A

echo ========================================
echo   MongoDB RAG Configuration Setup
echo ========================================
echo.

REM Check if mongosh is installed
echo [1/4] Checking for mongosh...
mongosh --version >nul 2>&1
if errorlevel 1 (
    echo ERROR: mongosh not found!
    echo Please install MongoDB Shell:
    echo   - Windows: choco install mongosh
    echo   - Or download from: https://www.mongodb.com/try/download/shell
    pause
    exit /b 1
)
echo       mongosh is installed.

REM Check if MongoDB is running
echo.
echo [2/4] Checking MongoDB connection...
mongosh --eval "db.adminCommand('ping')" >nul 2>&1
if errorlevel 1 (
    echo ERROR: Cannot connect to MongoDB!
    echo Please ensure MongoDB is running:
    echo   - Check docker: docker ps
    echo   - Start with: docker-compose up -d mongodb
    pause
    exit /b 1
)
echo       MongoDB connection successful.

REM Run the setup script
echo.
echo [3/4] Running MongoDB setup script...
mongosh --file setup-mongodb-optimized.js

if errorlevel 1 (
    echo.
    echo ========================================
    echo   ERROR DURING SETUP
    echo ========================================
    echo.
    echo The setup script encountered errors.
    echo.
    echo Common issues:
    echo   1. MongoDB version < 7.0 (needed for vector search)
    echo   2. Connection issues to MongoDB
    echo   3. Insufficient permissions
    echo.
    echo Check the error messages above for details.
    echo.
    pause
    exit /b 1
)

echo.
echo [4/4] Verifying setup...
mongosh --file verify-mongodb-setup.js

echo.
echo ========================================
echo   SETUP COMPLETED SUCCESSFULLY
echo ========================================
echo.
echo Next steps:
echo   1. Start the Blazor application: docker-compose up blazor-app
echo   2. Or run locally: dotnet run --project BlazorApp
echo.
echo Documentation:
echo   - MongoDB Shell: https://www.mongodb.com/docs/mongodb-shell/
echo   - Vector Search: https://www.mongodb.com/docs/atlas/atlas-vector-search/
echo.
pause
