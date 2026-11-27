# Multi-stage build para optimización
# ====================================

# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Optional build metadata is embedded at publish time, not supplied as runtime environment state.
ARG BUILD_VERSION=0.1.0
ARG BUILD_VERSION_SUFFIX=local
ARG BUILD_COMMIT=unknown
ARG CI_BUILD_NUMBER=local
ARG BUILD_PHASE=development

# Copiar project file y restaurar dependencias (optimización: cache si no cambia)
COPY ["BlazorApp.csproj", "./"]
RUN dotnet restore "BlazorApp.csproj"

# Copiar el resto del código fuente
COPY . .

# Publicar en modo Release, incorporando metadata opcional en el ensamblado
RUN dotnet publish "BlazorApp.csproj" -c Release -o /app/publish --no-restore \
    -p:VersionPrefix="${BUILD_VERSION}" \
    -p:VersionSuffix="${BUILD_VERSION_SUFFIX}" \
    -p:BuildCommit="${BUILD_COMMIT}" \
    -p:BuildNumber="${CI_BUILD_NUMBER}" \
    -p:BuildPhase="${BUILD_PHASE}"

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
RUN mkdir -p /app/logs /app/documents /app/models /app/data && \
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