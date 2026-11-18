# API y contratos internos

## Alcance

La aplicación no expone actualmente controladores REST ni endpoints `MapGet`, `MapPost`, `MapPut` o `MapDelete`. Su superficie pública es una aplicación Blazor Server interactiva. Las operaciones de documentos y RAG se ejecutan mediante servicios C# registrados en inyección de dependencias.

Este documento distingue tres tipos de contrato:

1. Rutas navegables de la aplicación.
2. Health check HTTP para operación y Docker.
3. Servicios internos que pueden reutilizarse si se crea una API HTTP en el futuro.

## Rutas HTTP de la aplicación

| Método | Ruta | Respuesta/uso |
|---|---|---|
| `GET` | `/` | Página de inicio Blazor. |
| `GET` | `/chat` | Interfaz de consulta RAG. |
| `GET` | `/chat?query={texto}` | Carga una consulta inicial y la envía una vez al inicializar la página. La UI limita el texto introducido a 2000 caracteres. |
| `GET` | `/documents` | Interfaz de administración de documentos. |
| `GET` | `/statistics` | Dashboard de métricas recopiladas en memoria. |
| `GET` | `/health` | Health check de ASP.NET Core. Docker espera una respuesta HTTP satisfactoria. |

Blazor gestiona la interacción de las páginas mediante el circuito SignalR/Blazor Server. No se debe llamar a `/chat` esperando un JSON con una respuesta RAG; para eso sería necesario añadir un controlador o un endpoint minimal API.

### `GET /health`

`Program.cs` registra `app.MapHealthChecks("/health")`. El endpoint se usa en el `HEALTHCHECK` de `BlazorApp/Dockerfile` y en `docker-compose.yml`.

Ejemplo:

```powershell
Invoke-WebRequest http://localhost:8080/health
```

El estado HTTP indica si el health check está disponible. La implementación actual registra `AddHealthChecks()` sin comprobaciones personalizadas de MongoDB u Ollama, por lo que un `200` no sustituye a la revisión de las dependencias en los logs.

## Servicio RAG activo

`BlazorApp/Services/RagService.cs` es el servicio inyectado por `Components/Pages/Chat.razor`.

### `QueryAsync(string userQuery, int maxResults = 5)`

**Entrada**

- `userQuery`: pregunta del usuario.
- `maxResults`: número máximo de chunks recuperados; la página usa el valor predeterminado.

**Proceso**

1. Genera el embedding de la consulta con `Ollama:EmbeddingModel` mediante `POST /api/embeddings`.
2. Lee los documentos de `chunks`.
3. Calcula similitud coseno en memoria y ordena los resultados.
4. Carga la información de los documentos de `documents`.
5. Envía contexto y pregunta al modelo mediante `POST /api/chat`.
6. Añade una sección Markdown de fuentes con nombre de documento y número de fragmentos.

**Salida**

Devuelve `Task<string>` con Markdown. Si no hay chunks, devuelve un mensaje indicando que no encontró información relevante. Las excepciones se registran y se convierten en un mensaje de error textual.

**Limitaciones actuales**

- No utiliza `$vectorSearch` de MongoDB.
- Carga todos los chunks antes de calcular la similitud.
- No conecta `EmbeddingCacheService`.
- No persiste la pregunta y respuesta desde `/chat`.
- Las fuentes activas son básicas y no garantizan página, sección o puntuación.

## Orquestador disponible, no conectado a `/chat`

`BlazorApp/Services/RagOrchestrator.cs` coordina `DocumentProcessor`, `EnhancedRagService`, `ChatSessionService` y `ResponseFormatter`, pero `Chat.razor` no lo inyecta. Sus métodos públicos son contratos internos y no endpoints HTTP.

| Método | Resultado | Propósito |
|---|---|---|
| `ProcessDocumentAsync(string filePath, CancellationToken)` | `DocumentProcessingResult` | Extrae, divide, embebe y persiste un documento. |
| `QueryDocumentsAsync(string userQuery, string? sessionId = null, int maxResults = 10, CancellationToken)` | `QueryResult` | Consulta mediante `EnhancedRagService`, con la ruta mejorada disponible. |
| `ManageSessionAsync(string userId, string? sessionId = null, CancellationToken)` | `SessionResult` | Crea o carga una sesión persistida. |
| `GetProcessingStatisticsAsync(CancellationToken)` | `ProcessingStatistics` | Devuelve contadores de documentos y chunks con embeddings. |
| `GetDocumentsWithStatusAsync(CancellationToken)` | `List<DocumentStatusSummary>` | Lista el estado resumido de documentos. |
| `ReprocessDocumentAsync(string documentId, CancellationToken)` | `DocumentProcessingResult` | Elimina/recrea el procesamiento del documento. |
| `DeleteDocumentAsync(string documentId, CancellationToken)` | `DeleteResult` | Elimina el documento y sus chunks asociados. |

Los resultados incluyen `Success`, un mensaje legible y, según la operación, identificadores, respuesta, fechas o contadores. No se deben exponer directamente como contrato externo sin añadir autenticación, validación de entrada y códigos HTTP.

## Servicios internos de administración

La página `/documents` usa `DocumentManagementService` y `DocumentScanner` directamente en el circuito Blazor:

- `GetStatisticsAsync()` obtiene totales de documentos, chunks y tamaño.
- `GetDocumentsAsync(page, pageSize, status, searchTerm)` devuelve una página filtrada.
- `GetDocumentChunksAsync(documentId)` carga los fragmentos de un documento.
- `ReprocessDocumentAsync(documentId)` vuelve a ejecutar el procesamiento.
- `DeleteDocumentAsync(documentId)` elimina un documento y sus chunks.
- `DocumentScanner.ScanAndProcessAsync(CancellationToken)` escanea y procesa archivos compatibles.

Estas llamadas no tienen rutas HTTP JSON independientes. La autorización tampoco está implementada en esta versión; la ruta `/documents` debe protegerse antes de exponer la aplicación a usuarios no confiables.

## Integración con Ollama

La aplicación crea `HttpClient` con el endpoint configurado en `Ollama:Endpoint` y un timeout de cinco minutos.

### Embeddings

```http
POST {Ollama:Endpoint}/api/embeddings
Content-Type: application/json

{
  "model": "nomic-embed-text",
  "prompt": "texto a vectorizar"
}
```

La respuesta debe contener un array numérico `embedding`. La dimensión debe ser consistente entre chunks y consultas.

### Chat

```http
POST {Ollama:Endpoint}/api/chat
Content-Type: application/json

{
  "model": "llama3.2",
  "messages": [
    { "role": "system", "content": "contexto y reglas RAG" },
    { "role": "user", "content": "pregunta" }
  ],
  "stream": false
}
```

La respuesta debe contener `message.content`. `ollama-init/entrypoint.sh` descarga ambos modelos en el compose principal.

## Contratos de persistencia

La aplicación usa estas colecciones en `RagDatabase`:

- `documents`: ruta, nombre, extensión, tamaño, hash SHA-256, estado, fechas, metadatos, tags y cantidad de chunks.
- `chunks`: `documentId`, índice, posiciones, texto, metadatos, página, conteos y vector `Embedding`.
- `embedding_cache`: caché híbrida preparada por `EmbeddingCacheService`.
- `chat_sessions` y `chat_messages`: sesiones y mensajes preparados por `ChatSessionService`.

Los modelos C# están en `BlazorApp/Models`. Los scripts de MongoDB aún contienen diferencias entre nombres legacy y propiedades actuales; verifica el esquema de la instancia antes de integrar un cliente externo.

## Reglas para añadir una API HTTP

Si se necesita una API pública, debe añadirse una capa explícita que:

1. Valide tamaño, formato y contenido de entradas.
2. No acepte rutas arbitrarias del sistema para procesar documentos.
3. Devuelva códigos HTTP consistentes y DTOs estables en vez de excepciones o mensajes internos.
4. Proteja administración, sesiones y fuentes con autenticación/autorización.
5. Aplique cancelación, límites de resultados y timeouts.
6. No exponga connection strings, prompts internos ni trazas de excepciones.
