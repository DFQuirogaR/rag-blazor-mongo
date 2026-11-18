# Diseño del sistema RAG funcional

**Última sincronización:** 2026-11-13

## Estado del diseño

Este documento distingue la arquitectura que existe en el repositorio de las capacidades que todavía son objetivo de la fase 2. La documentación anterior describía `EnhancedRagService` como el camino principal, pero la página real `/chat` usa `RagService`.

## Arquitectura implementada

```text
DocumentSettings:ScanPaths
          |
          v
  DocumentScanner (hosted service)
          |
          v
  DocumentProcessor
     |       |        |
     |       |        +--> IEmbeddingCache / EmbeddingCacheService
     |       +-----------> DocumentChunker
     +-------------------> ITextExtractor
                              |--> WordTextExtractor (.docx)
                              +--> PdfTextExtractor (.pdf)
          |
          v
        MongoDB
   documents / chunks / embedding_cache

Blazor /chat
     |
     v
 Chat.razor --> RagService --> MongoDB chunks --> similitud coseno en memoria
                       |
                       +--> Ollama embeddings y respuesta

Ruta disponible pero no conectada a /chat:
 RagOrchestrator --> EnhancedRagService + ResponseFormatter + sesiones
```

### Composición y dependencias

`BlazorApp/Program.cs` registra MongoDB, los servicios de Ollama, los extractores, `DocumentProcessor`, `EnhancedRagService`, `ResponseFormatter`, `IEmbeddingCache`, `RagOrchestrator`, `ChatSessionService`, `DocumentScanner` y `RagService`.

La página `BlazorApp/Components/Pages/Chat.razor` inyecta únicamente `RagService` y `ILogger<Chat>`. Por ello, la integración de sesiones, reranking y formateo avanzado no se puede considerar parte del flujo activo de `/chat`.

## Procesamiento de documentos

1. `DocumentScanner` obtiene las rutas configuradas y localiza archivos compatibles.
2. `ITextExtractor` selecciona `WordTextExtractor` o `PdfTextExtractor`.
3. `DocumentProcessor` calcula el hash SHA-256 y evita reprocesar archivos sin cambios.
4. `DocumentChunker` divide el contenido respetando párrafos, headings, páginas y secciones.
5. Se genera un embedding por chunk mediante Ollama.
6. El documento y sus chunks se guardan en MongoDB.
7. Al cambiar un documento, se reemplazan sus chunks y se registra el estado del procesamiento.

El tamaño usado actualmente es 1000 caracteres con 200 caracteres de solapamiento. El diseño anterior mostraba 1500 caracteres, por lo que ese valor ya no representa la implementación real.

## Modelo de datos actual

Los modelos C# principales son `DocumentModel`, `ChunkModel`, `EmbeddingResult`, `ChatSession` y `ChatMessage`.

### `documents`

El modelo de documento conserva, como mínimo:

- Identificador MongoDB.
- Ruta y nombre del archivo.
- Tipo y tamaño.
- Hash del contenido.
- Fechas de procesamiento y modificación cuando están disponibles.
- Estado de procesamiento y mensaje de error.
- Metadatos extraídos del documento.

### `chunks`

El modelo de chunk conserva, como mínimo:

- Identificador del documento de origen.
- Índice del chunk.
- Posición inicial y final.
- Texto del chunk.
- Embedding numérico.
- Página, sección y metadatos de extracción cuando están disponibles.
- Fecha de creación.

Los modelos usan atributos BSON y validaciones. Los scripts de MongoDB contienen nombres legacy en algunos pasos y nombres derivados de propiedades C# en otros; esa diferencia debe resolverse en una futura tarea de esquema, no asumirse como un contrato único.

## MongoDB e índices

Los scripts relevantes son:

- `setup-mongodb-optimized.js`.
- `mongo-schema-migration.js`.
- `setup-mongodb.js`.
- `verify-mongodb-setup.js`.

Hay definiciones para índices de texto, compuestos y vectoriales. No hay evidencia verificable de que el índice vectorial esté activo en la instancia actual. El script optimizado también aclara que la definición de un índice vectorial de Atlas no equivale necesariamente a `createIndex` estándar.

La búsqueda que usa `/chat` no consume un índice vectorial. Carga los chunks y calcula similitud coseno en memoria. Por tanto, el soporte de índices queda documentado como capacidad preparada, no como parte del flujo comprobado.

## Servicios RAG

### `RagService` activo

`BlazorApp/Services/RagService.cs`:

1. Genera el embedding de la consulta.
2. Obtiene los chunks disponibles.
3. Calcula similitud coseno en memoria.
4. Selecciona los resultados superiores.
5. Construye el contexto y solicita una respuesta a Ollama.
6. Devuelve texto que puede incluir referencias básicas a documentos.

Esta ruta no usa búsqueda vectorial MongoDB, reranking completo, ensamblaje de chunks contiguos ni la caché de embeddings.

### `EnhancedRagService` disponible

`BlazorApp/Services/EnhancedRagService.cs` contiene búsqueda mejorada, reranking híbrido y límite aproximado de contexto. `RagOrchestrator` puede coordinarlo con procesamiento, sesiones y `ResponseFormatter`, pero `Chat.razor` no inyecta ni llama a ese orquestador.

## Interfaz `/chat`

La implementación actual de `BlazorApp/Components/Pages/Chat.razor` contiene:

- Textarea con límite de 2000 caracteres.
- Envío con Enter y salto de línea con Shift+Enter.
- Estado de carga y spinner.
- Una consulta enviada y una respuesta visible a la vez.
- Acción para limpiar y comenzar una nueva consulta.
- `MarkdownRenderer` para presentar la respuesta.

No contiene un historial visual de mensajes ni una lista de sesiones. `ChatSessionService` existe y persiste sesiones, pero no está conectado a esta página.

## Almacenamiento de sesiones

`ChatSessionService` y los modelos de chat permiten persistencia, carga, limpieza y exportación de conversaciones. El servicio está registrado como singleton. Su existencia no demuestra que el historial esté activo en la UI ni que el contexto histórico se esté enviando al LLM.

## Observabilidad

La aplicación registra:

- `ErrorHandlingMiddleware`.
- `RequestLoggingMiddleware`.
- `PerformanceMiddleware`.
- Extensiones de logging estructurado.
- Health check `/health`.
- Página `/statistics`.

La presencia de estos componentes no sustituye una prueba de ejecución, una métrica de rendimiento validada o alertas operativas configuradas.

## Despliegue

Existen `docker-compose.yml` y `docker-compose-simple.yml`. El compose principal incluye MongoDB, Ollama, Blazor, health checks, variables de entorno y montaje del script de inicialización optimizado. El compose simple es una variante reducida y no ofrece exactamente las mismas garantías.

## Decisiones pendientes

Para completar la arquitectura objetivo, las tareas pendientes son:

1. Conectar `/chat` con `RagOrchestrator` o con `EnhancedRagService`.
2. Implementar búsqueda vectorial MongoDB real y verificar el índice activo.
3. Integrar `EmbeddingCacheService` en las lecturas y escrituras de embeddings.
4. Conectar `ChatSessionService` con la interfaz y el contexto del LLM.
5. Unificar el esquema MongoDB y elegir un script canónico de inicialización.
6. Añadir pruebas end-to-end e integración.
7. Completar la documentación técnica y de usuario.
8. Añadir las validaciones de seguridad y rutas previstas en los requisitos.
