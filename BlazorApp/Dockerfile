# Multi-stage build para optimización
# ====================================

# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copiar project file y restaurar dependencias (optimización: cache si no cambia)
COPY ["BlazorApp.csproj", "./"]
RUN dotnet restore "BlazorApp.csproj"

# Copiar el resto del código fuente
COPY . .

# Publicar en modo Release
RUN dotnet publish "BlazorApp.csproj" -c Release -o /app/publish --no-restore

# Stage 2: Runtime final
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# Instalar curl para el health check (la imagen base aspnet no lo incluye)
RUN apt-get update && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

# Crear usuario no-root para seguridad
RUN groupadd -r appgroup && useradd -r -g appgroup appuser

# Copiar binaries desde build stage
COPY --from=build /app/publish .

# Crear estructura de directorios y establecer permisos
RUN mkdir -p /app/logs /app/documents /app/models && \
    chown -R appuser:appgroup /app

# Exponer puerto (debe coincidir con ASPNETCORE_URLS)
EXPOSE 8080

# Usuario no-root para ejecutar la aplicación
USER appuser

# Health check para monitorización
HEALTHCHECK --interval=30s --timeout=10s --start-period=40s --retries=3 \
    CMD curl -f http://localhost:8080/health || exit 1

# Punto de entrada
ENTRYPOINT ["dotnet", "BlazorApp.dll"]