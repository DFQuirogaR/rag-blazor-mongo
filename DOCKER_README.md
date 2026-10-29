# Guía de Despliegue Docker - RAG Blazor App

## Visión General

Este sistema RAG utiliza tres componentes principales:

1. **MongoDB 7.0** - Base de datos vectorial para almacenar documentos y embeddings
2. **Ollama** - Servidor LLM local para generación de respuestas y embeddings
3. **Blazor App** - Aplicación web de chat con interfaz moderna

## Requisitos Previos

- Docker Engine 24.0+
- Docker Compose 2.0+
- 8GB de RAM disponible (recomendado)
- 20GB de espacio en disco

## Archivos de Configuración

### docker-compose.yml

Configuración principal que define todos los servicios:

```yaml
services:
  mongodb:      # Base de datos vectorial
  ollama:       # Servidor LLM
  ollama-init:  # Inicialización de modelos
  blazor-app:   # Aplicación Blazor
  backup:       # Servicio de backup (opcional)
```

### .env.example

Archivo de ejemplo con todas las variables de entorno. Copiar a `.env` y personalizar:

```bash
cp .env.example .env
```

### ollama-init/entrypoint.sh

Script de inicialización que descarga automáticamente los modelos necesarios:
- `llama3.2` - Modelo LLM principal
- `nomic-embed-text` - Modelo para embeddings

## Comandos de Docker Compose

### Iniciar el sistema

```bash
docker-compose up -d
```

### Ver logs

```bash
# Todos los servicios
docker-compose logs -f

# Servicio específico
docker-compose logs -f mongodb
docker-compose logs -f ollama
docker-compose logs -f blazor-app
```

### Detener el sistema

```bash
docker-compose down
```

### Detener y eliminar volúmenes

```bash
docker-compose down -v
```

### Reiniciar un servicio

```bash
docker-compose restart mongodb
docker-compose restart ollama
docker-compose restart blazor-app
```

### Ver estado

```bash
docker-compose ps
docker-compose stats
```

## Salud de los Servicios

### MongoDB

```bash
# Verificar salud
docker-compose exec mongodb mongosh --eval "db.adminCommand('ping')"

# Ver colecciones
docker-compose exec mongodb mongosh RagDatabase --eval "db.getCollectionNames()"
```

### Ollama

```bash
# Verificar salud
curl http://localhost:11434/api/version

# Ver modelos instalados
docker-compose exec ollama ollama list
```

### Blazor App

```bash
# Verificar salud
curl http://localhost:8080/health

# Acceder a la aplicación
# http://localhost:8080
```

## Configuración de Variables de Entorno

### Variables obligatorias

```env
# MongoDB
MONGO_CONNECTION_STRING=mongodb://mongodb:27017/RagDatabase
MONGO_DATABASE=RagDatabase

# Ollama
OLLAMA_ENDPOINT=http://ollama:11434
OLLAMA_MODEL=llama3.2
OLLAMA_EMBEDDING_MODEL=nomic-embed-text

# Documentos
DOCUMENT_SCAN_PATHS=/app/documents
```

### Variables de red

```env
ASPNETCORE_URLS=http://+:8080
```

## Volumenes

### Datos persistentes

```yaml
volumes:
  mongo_data:     # Datos de MongoDB
  ollama_data:    # Modelos de Ollama
  documents_data: # Documentos de usuario
```

### Montajes de lectura

```yaml
# Documentos (host -> contenedor)
- C:\Users\dquirogr\Documents\sharepoint_doc:/app/documents:ro

# Script de inicialización MongoDB
- ./setup-mongodb-optimized.js:/docker-entrypoint-initdb.d/setup.js:ro

# Scripts de Ollama
- ./ollama-init:/opt/ollama-init:ro
```

## Procesamiento de Documentos

### Colocar documentos

Coloca tus documentos Word (.docx) y PDF en la carpeta de documentos:

```
C:\Users\dquirogr\Documents\sharepoint_doc\
├── documentos\
│   ├── proceso1.docx
│   └── proceso2.pdf
├── manuals\
│   └── manual_usuario.pdf
└── policies\
    └── politicas.pdf
```

### Procesamiento automático

El sistema escanea las carpetas configuradas cada 5 minutos por defecto. Los documentos nuevos se procesan automáticamente.

### Procesamiento manual

Puedes procesar documentos manualmente usando la interfaz web o los endpoints de la API.

## Backup y Restore

### Backup

```bash
# Script automatizado
./backup.sh [nombre]

# Ejemplos
./backup.sh                    # backup_20241215_120000
./backup.sh manual-backup      # manual-backup
```

### Restore

```bash
./restore.sh backup_20241215_120000
```

### Backup manual

```bash
# Crear backup
docker-compose exec mongodb mongodump \
    --db RagDatabase \
    --out /backups/backup_manual

# Copiar backup al host
docker-compose cp mongodb:/backups/backup_manual ./backup_manual

# Restaurar
docker-compose cp ./backup_manual mongodb:/backups/
docker-compose exec mongodb mongorestore \
    --db RagDatabase \
    --drop /backups/backup_manual/RagDatabase
```

## Solución de Problemas

### MongoDB no inicia

```bash
# Ver logs
docker-compose logs mongodb

# Verificar permisos
docker-compose exec mongodb ls -la /data/db

# Reiniciar
docker-compose restart mongodb
```

### Ollama lento

```bash
# Verificar GPU
docker-compose exec ollama nvidia-smi

# Ver modelos
docker-compose exec ollama ollama list

# Limpiar cache (si es necesario)
docker-compose down -v
docker-compose up -d
```

### Blazor App no se conecta

```bash
# Verificar red
docker-compose exec blazor-app ping mongodb
docker-compose exec blazor-app ping ollama

# Verificar variables de entorno
docker-compose exec blazor-app env | grep -E "MONGO|OLLAMA"
```

### Rebuild de imágenes

```bash
# Rebuild sin cache
docker-compose build --no-cache

# Rebuild solo un servicio
docker-compose build blazor-app
```

## Optimización

### GPU

Descomentar en docker-compose.yml si tienes GPU NVIDIA:

```yaml
ollama:
  deploy:
    resources:
      reservations:
        devices:
          - driver: nvidia
            count: all
            capabilities: [gpu]
```

### Memoria

Ajustar límites de memoria en docker-compose.yml:

```yaml
mongodb:
  deploy:
    resources:
      limits:
        memory: 4G
```

### Scaling

Para múltiples replicas (producción):

```bash
docker-compose up -d --scale blazor-app=3
```

## Producción

### HTTPS

Configurar reverse proxy (Nginx/Apache):

```nginx
server {
    listen 443 ssl;
    server_name your-domain.com;
    
    ssl_certificate /path/to/cert.pem;
    ssl_certificate_key /path/to/key.pem;
    
    location / {
        proxy_pass http://localhost:8080;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
    }
}
```

### Logs

```bash
# Ver logs en tiempo real
docker-compose logs -f --tail=100

# Exportar logs
docker-compose logs > rag-logs.txt
```

### Actualización

```bash
# Detener
docker-compose down

# Pull nuevas imágenes
docker-compose pull

# Rebuild y restart
docker-compose up -d --build
```

## Monitorización

### Salud de servicios

```bash
docker-compose ps
```

### Métricas

```bash
docker-compose stats
```

### Logs

```bash
docker-compose logs --tail=50 -f
```

## Licencia

Este sistema forma parte del proyecto RAG Blazor MongoDB.