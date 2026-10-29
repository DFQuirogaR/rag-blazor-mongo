@echo off
echo ========================================
echo Probando Compilacion Local
echo ========================================
echo.

cd /d "%~dp0\BlazorApp"

echo Limpiando proyecto...
dotnet clean BlazorApp.csproj

echo.
echo Restaurando paquetes...
dotnet restore BlazorApp.csproj

echo.
echo Compilando proyecto BlazorApp (sin tests)...
dotnet build BlazorApp.csproj --no-restore

echo.
if %ERRORLEVEL% EQU 0 (
    echo ========================================
    echo COMPILACION EXITOSA
    echo ========================================
    echo.
    echo El proyecto principal compila correctamente.
    echo Puedes proceder con: rebuild.bat
) else (
    echo ========================================
    echo ERROR EN COMPILACION
    echo ========================================
    echo.
    echo Revisa los errores arriba
)

echo.
pause
