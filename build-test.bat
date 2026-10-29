@echo off
cd BlazorApp
dotnet build BlazorApp.csproj --no-restore -v q
echo Build completed
pause
