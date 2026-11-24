# RAG Blazor Mongo

Aplicación Blazor Server para consultar documentación `.docx` y `.pdf` mediante Retrieval-Augmented Generation (RAG). El sistema extrae el contenido, lo divide en fragmentos, genera embeddings con Ollama y guarda documentos y fragmentos en MongoDB.

> Esta guía describe el estado comprobable del repositorio. Algunas piezas preparan capacidades futuras, pero no forman parte del flujo activo de `/chat`; se indican explícitamente para evitar confundir la arquitectura prevista con la implementación actual.

## Contenido

- [Requisitos](#requisitos)
- [Inicio rápido con Docker](#inicio-rápido-con-docker)
- [Desarrollo local](#desarrollo-local)
- [Configuración](#configuración)
- [Uso y rutas](#uso-y-rutas)
- [Procesamiento y búsqueda](#procesamiento-y-búsqueda)
- [Operación y despliegue](#operación-y-despliegue)
- [Solución de problemas](#solución-de-problemas)
- [Documentación técnica](#documentación-técnica)

## Requisitos

### Docker

- Docker Engine 24 o posterior.
- Docker Compose v2.
- Al menos 8 GB de RAM disponibles para MongoDB, Ollama y el modelo de lenguaje.
- Al menos 20 GB de espacio libre para imágenes, modelos, datos y backups.

### Ejecución local sin Docker

- .NET SDK 10, porque `BlazorApp/BlazorApp.csproj` apunta a `net10.0`.
- MongoDB 7 o compatible.
- Ollama con los modelos `llama3.2` y `nomic-embed-text`.

## Inicio rápido con Docker

1. Copia el archivo de variables de ejemplo:

   ```powershell
   Copy-Item .env.example .env
   ```

2. Si los documentos están fuera del repositorio, edita `.env` y establece `DOCUMENTS_PATH` a una ruta del host. La carpeta se monta como `/app/documents` dentro de la aplicación.

3. Coloca archivos `.docx` o `.pdf` en `documents/` o en la ruta configurada.

4. Construye e inicia el stack principal:

   ```powershell
   docker compose up -d --build
   ```

   El compose principal inicia MongoDB, Ollama, el contenedor que descarga los modelos, la aplicación Blazor y el servicio de backup.

5. Comprueba el estado y espera a que Ollama termine de descargar los modelos:

   ```powershell
   docker compose ps
   docker compose logs -f ollama-init
   ```

6. Comprueba la aplicación:

   ```powershell
   Invoke-WebRequest http://localhost:8080/health
   ```

   Después abre [http://localhost:8080/chat](http://localhost:8080/chat).

El script `setup-mongodb-canonical.js` se monta como inicializador de MongoDB. Los scripts de inicialización de MongoDB solo se ejecutan automáticamente cuando se crea el volumen por primera vez. Para revisar o volver a ejecutar la configuración, consulta [MONGODB_SETUP.md](MONGODB_SETUP.md).

## Desarrollo local

Puedes ejecutar MongoDB y Ollama con Docker y la aplicación con el SDK de .NET. Desde PowerShell:

```powershell
docker compose up -d mongodb ollama
$env:ConnectionStrings__MongoDb = "mongodb://localhost:27017/RagDatabase"
$env:Ollama__Endpoint = "http://localhost:11434"
$env:ASPNETCORE_URLS = "http://localhost:8080"
dotnet run --project BlazorApp/BlazorApp.csproj
```

Si Ollama no tiene los modelos, instálalos antes de consultar:

```powershell
ollama pull llama3.2
ollama pull nomic-embed-text
```

La configuración predeterminada de `appsettings.json` usa los nombres de servicio Docker `mongodb` y `ollama`. Por eso, para una ejecución local se deben sobrescribir los endpoints como en el ejemplo anterior.

## Configuración

La configuración de la aplicación usa `appsettings.json`, `appsettings.Development.json` y variables de entorno de ASP.NET Core. En variables de entorno, `:` se representa con `__`.

| Configuración | Valor predeterminado | Uso |
|---|---|---|
| `ConnectionStrings:MongoDb` | `mongodb://mongodb:27017` | Conexión a MongoDB. La aplicación usa la base `RagDatabase`. |
| `Ollama:Endpoint` | `http://ollama:11434` | Endpoint base de Ollama. |
| `Ollama:Model` | `llama3.2` | Modelo de generación de respuestas. |
| `Ollama:EmbeddingModel` | `nomic-embed-text` | Modelo de embeddings. |
| `DocumentSettings:ScanPaths` | `/app/documents` | Rutas que escanea el servicio hospedado. |
| `DocumentSettings:ScanIntervalMinutes` | `5` | Intervalo de escaneo periódico. |
| `DocumentSettings:PollingIntervalSeconds` | `30` | Intervalo de polling interno. |
| `DocumentSettings:EnableFileSystemWatcher` | `true` | Habilita la detección por watcher cuando está disponible. |
| `DocumentSettings:ExcludePatterns` | `*.tmp`, `*.bak`, `*.temp`, `backup`, `temp` | Patrones excluidos por el scanner. |

En Docker, `docker-compose.yml` establece directamente `ConnectionStrings__MongoDb`, `Ollama__Endpoint`, `DocumentSettings__ScanPaths__0` y `ASPNETCORE_URLS`. Por tanto, no todas las variables de `.env.example` sustituyen valores del compose principal. Revisa el compose antes de asumir que una variable es consumida por la aplicación.

El chunker que usa actualmente `DocumentProcessor` trabaja con 1000 caracteres y 200 de solapamiento. Los valores `DOCUMENT_CHUNK_SIZE` y `DOCUMENT_CHUNK_OVERLAP` del archivo de ejemplo documentan una configuración prevista, pero no se deben considerar efectivos mientras el processor no los lea.

## Uso y rutas

| Ruta | Función |
|---|---|
| `/` | Página de inicio. |
| `/chat` | Consulta una pregunta y muestra una respuesta Markdown. Enter envía y Shift+Enter inserta una línea. |
| `/documents` | Lista documentos, filtra por nombre/estado, ejecuta un escaneo, reprocesa, muestra chunks y elimina documentos. |
| `/statistics` | Dashboard de métricas en memoria. |
| `/health` | Health check usado por Docker y monitorización. |

La interfaz de `/chat` limita la entrada a 2000 caracteres y muestra una consulta y su respuesta actual. El historial persistido de `ChatSessionService` todavía no está conectado a esta página.

## Procesamiento y búsqueda

1. `DocumentScanner` localiza documentos compatibles en `DocumentSettings:ScanPaths`.
2. `WordTextExtractor` procesa `.docx` con Open XML y `PdfTextExtractor` procesa `.pdf` con iText7.
3. `DocumentProcessor` calcula SHA-256 para omitir archivos sin cambios.
4. `DocumentChunker` crea fragmentos conservando, cuando está disponible, página, sección, posiciones y metadatos.
5. Ollama genera un embedding para cada fragmento.
6. MongoDB guarda el documento en `documents` y los fragmentos en `chunks`.
7. `RagService`, que es el servicio usado por `/chat`, genera el embedding de la consulta, carga los chunks, calcula similitud coseno en memoria, construye el contexto y solicita la respuesta a Ollama.

La búsqueda vectorial de MongoDB, el reranking mejorado, el ensamblaje de chunks contiguos y la caché de embeddings están preparados en servicios/scripts, pero no forman parte de todo el flujo activo de `/chat`. Consulta [docs/Architecture.md](docs/Architecture.md) para el detalle.

## Operación y despliegue

### Comandos habituales

```powershell
# Ver servicios
docker compose ps

# Ver logs de la aplicación
docker compose logs -f blazor-app

# Reiniciar un servicio
docker compose restart blazor-app

# Actualizar y reconstruir
docker compose up -d --build

# Detener sin borrar los datos
docker compose down
```

No uses `docker compose down -v` salvo que quieras borrar los volúmenes de MongoDB y Ollama. Esto elimina la base de datos y los modelos descargados.

### Persistencia y backups

El compose principal usa los volúmenes `mongo_data` y `ollama_data`, monta la carpeta de documentos en modo lectura y escribe backups en `backups/`. También puedes usar:

```powershell
# Backup desde el script del repositorio
bash ./backup.sh manual-backup

# Verificar la configuración de MongoDB
docker compose exec mongodb mongosh RagDatabase --eval "db.getCollectionNames()"
```

Consulta [DOCKER_README.md](DOCKER_README.md) y [MONGODB_SETUP.md](MONGODB_SETUP.md) para procedimientos de backup/restore y comprobaciones de índices.

Para producción, coloca la aplicación detrás de un reverse proxy con HTTPS, restringe la exposición de MongoDB y Ollama a la red interna, protege las credenciales mediante secretos y conserva backups fuera del host de los contenedores. La aplicación no implementa autenticación/autorización por documento en esta versión.

El archivo `docker-compose-simple.yml` es una variante reducida para desarrollo. No ofrece los health checks, la inicialización de modelos, el scanner de documentos ni el servicio de backup del compose principal.

## Solución de problemas

### `/health` no responde

Comprueba el estado y los logs:

```powershell
docker compose ps
docker compose logs --tail=100 blazor-app
```

Si el contenedor reinicia, revisa que MongoDB y Ollama estén saludables. El health check de la aplicación escucha en el puerto 8080 dentro del contenedor.

### Ollama no responde o la consulta devuelve un error 404

```powershell
docker compose logs ollama ollama-init
docker compose exec ollama ollama list
```

Espera a que `ollama-init` complete y confirma que aparecen `llama3.2` y `nomic-embed-text`. Si la aplicación corre fuera de Docker, usa `http://localhost:11434` en `Ollama:Endpoint`.

### No aparecen documentos

- Verifica que la ruta configurada en `DOCUMENTS_PATH` existe y contiene `.docx` o `.pdf`.
- Confirma el montaje: `docker compose exec blazor-app ls -la /app/documents`.
- Revisa el scanner: `docker compose logs blazor-app`.
- En `/documents`, pulsa **Escanear y actualizar**.

### MongoDB no tiene colecciones o índices

```powershell
docker compose logs mongodb
docker compose exec mongodb mongosh RagDatabase --eval "db.getCollectionNames()"
docker compose exec mongodb mongosh RagDatabase --eval "db.chunks.getIndexes()"
```

Ejecuta `verify-mongodb-setup.js` siguiendo [MONGODB_SETUP.md](MONGODB_SETUP.md). Recuerda que el inicializador de `/docker-entrypoint-initdb.d` no se repite sobre un volumen existente.

### La respuesta es lenta

La ruta activa de RAG carga todos los chunks y calcula similitud en memoria antes de llamar a Ollama. Revisa el tiempo de ambos servicios y la memoria disponible:

```powershell
docker compose stats
docker compose logs --tail=100 blazor-app ollama
```

La búsqueda vectorial indexada y la integración completa de la caché son mejoras pendientes, no parámetros que puedan habilitarse solo desde `.env`.

## Documentación técnica

- [API y contratos internos](docs/API.md): rutas disponibles, health check, servicios C# y contratos de datos.
- [Arquitectura](docs/Architecture.md): capas, flujo de ingesta, flujo de consulta, persistencia, observabilidad y límites actuales.
- [Despliegue Docker existente](DOCKER_README.md).
- [Configuración de MongoDB](MONGODB_SETUP.md).
- [Resumen de integración](BlazorApp/INTEGRATION_README.md).
- [Logging y monitorización](BlazorApp/LOGGING_IMPLEMENTATION.md).

## Desarrollo y validación

Desde la raíz del repositorio:

```powershell
dotnet restore BlazorApp/BlazorApp.csproj
dotnet build BlazorApp/BlazorApp.csproj
```

El proyecto no expone una API REST generada por controladores. Las operaciones descritas en `docs/API.md` son rutas de UI, health checks o servicios internos invocados por componentes y servicios hospedados.
