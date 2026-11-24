# RAG Phase 2: requisitos y estado de implementación

**Última sincronización:** 2026-11-13

## Alcance

Este documento describe los requisitos de la fase 2 y su estado comprobable en el repositorio. La sincronización se basa en el código y los archivos de configuración actuales; no implica que los requisitos pendientes estén implementados.

### Leyenda de estados

- **[x] Implementado:** existe una implementación identificable y coherente con el requisito.
- **[-] Parcial:** existe parte de la implementación, pero falta integración, validación o algún criterio relevante.
- **[ ] Pendiente:** no existe implementación suficiente o no hay evidencia verificable.

## Visión general actual

El proyecto es una aplicación Blazor Server que procesa documentos `.docx` y `.pdf`, genera embeddings con Ollama y almacena documentos y chunks en MongoDB.

La ingesta está compuesta por `DocumentScanner`, `DocumentProcessor`, `DocumentChunker`, los extractores de Word/PDF y los servicios de embedding. La página real `/chat` usa `RagService`, que recupera chunks y calcula similitud en memoria. `EnhancedRagService` y `RagOrchestrator` existen, pero no son el camino que usa actualmente `Chat.razor`.

La ruta de chat actual ofrece una consulta única. Tiene envío con Enter, estado de carga, Markdown y una tarjeta de respuesta, pero no conecta el historial persistido de `ChatSessionService` con la interfaz.

## Criterio de salida de la fase 2

La fase 2 no necesita completar todas las optimizaciones RAG antes de iniciar la autenticación local de la fase 3. Para pasar a la fase 3 debe existir una base comprobable: compilación, pruebas ejecutables, esquema canónico de documentos/chunks, ingesta de un documento real, consulta RAG conocida y smoke test completo con Docker Compose.

La búsqueda vectorial indexada, caché de embeddings, reranking avanzado, historial de chat y estadísticas persistentes se mantienen como trabajo posterior en sus especificaciones correspondientes. No se consideran eliminados; dejan de ser bloqueadores de la fase 3.

## Requisitos funcionales

### RF1: Gestión de documentos

- **[-] RF1.1:** `DocumentScanner` lee `DocumentSettings:ScanPaths`, admite extensiones Word/PDF y puede trabajar con polling o `FileSystemWatcher`. La configuración admite varias rutas, pero el watcher efectivo se configura solo sobre la primera ruta existente y los patrones con comodines no se interpretan como glob completo.
- **[x] RF1.2:** `WordTextExtractor` usa Open XML y conserva párrafos, títulos, tablas y estilos. `PdfTextExtractor` usa iText7 y conserva páginas mediante marcadores de página.
- **[-] RF1.3:** existe procesamiento incremental por hash SHA-256 y procesamiento por lotes. El flujo actual es secuencial y la caché de embeddings no evita todavía la generación de embeddings de chunks.
- **[-] RF1.4:** `DocumentModel` guarda ruta, nombre, tamaño, hash, fechas y estado, y los servicios escriben mediante `ILogger`. No existe una colección de auditoría separada ni una validación end-to-end del registro de operaciones.

### RF2: Procesamiento y almacenamiento

- **[x] RF2.1:** `DocumentChunker` separa por párrafos, headings, páginas y secciones, con un tamaño actual de 1000 caracteres y un solapamiento de 200.
- **[x] RF2.2:** `DocumentProcessor` genera un embedding para cada chunk y lo guarda en `ChunkModel.Embedding` mediante el servicio de Ollama.
- **[x] RF2.3:** documentos y chunks se almacenan en MongoDB mediante modelos con atributos BSON y validaciones. Los scripts de inicialización y migración todavía contienen diferencias entre el esquema legacy y los nombres actuales de las propiedades C#.
- **[-] RF2.4:** existen scripts para índices de texto, compuestos y vectoriales, pero no hay evidencia verificable de que el índice vectorial esté creado en la instancia actual. Además, la aplicación activa no ejecuta una búsqueda vectorial de MongoDB.
- **[x] RF2.5:** cada chunk conserva el documento de origen, índice, posiciones, texto, página/sección cuando está disponible, metadatos y embedding.

### RF3: Búsqueda semántica

- **[x] RF3.1:** `RagService` y `EnhancedRagService` generan el embedding de la consulta mediante Ollama.
- **[-] RF3.2:** el servicio utilizado por `/chat` calcula similitud coseno en memoria y carga los chunks disponibles. No usa un índice vectorial de MongoDB.
- **[-] RF3.3:** `RagService` ordena los chunks por similitud y toma los resultados superiores, pero la recuperación no usa búsqueda vectorial indexada y no tiene una prueba end-to-end.
- **[-] RF3.4:** `EnhancedRagService` contiene lógica de reranking híbrido, pero `/chat` utiliza el `RagService` legado y no esa ruta mejorada.
- **[-] RF3.5:** la ruta Enhanced tiene un límite aproximado de contexto, pero el servicio activo no ensambla chunks contiguos ni aplica un límite de tokens completo.

### RF4: Interfaz de usuario `/chat`

- **[-] RF4.1:** `/chat` tiene una interfaz moderna con gradientes, tarjetas y estados visuales, pero es una consulta única y no muestra el historial de conversación.
- **[x] RF4.2:** `Chat.razor` envía la consulta al pulsar Enter y conserva Shift+Enter para salto de línea.
- **[x] RF4.3:** las respuestas se muestran en una tarjeta con borde y esquinas redondeadas.
- **[x] RF4.4:** existe una respuesta actual y una acción de limpiar, pero no existe un historial visual de mensajes ni un panel de sesiones conectado a la página.
- **[x] RF4.5:** la página muestra spinner, estado de carga y texto de procesamiento mientras espera la respuesta.
- **[-] RF4.6:** `MarkdownRenderer` y `ResponseFormatter` existen, pero la página usa directamente la salida de `RagService`; la integración con el formateador mejorado no es completa.
- **[-] RF4.7:** la respuesta activa puede incluir el nombre del documento y el número de fragmentos, pero no muestra de forma consistente página, sección, puntuación y cita estructurada.

### RF5: Estado y rendimiento

- **[-] RF5.1:** `EmbeddingCacheService` implementa caché en memoria/MongoDB, expiración, invalidación y métricas. El flujo de ingesta/RAG no la consulta ni la usa para evitar regeneraciones.
- **[x] RF5.2:** el hash SHA-256 permite reconocer documentos iguales o modificados; al cambiar un archivo se reemplazan sus chunks.
- **[-] RF5.3:** el chunker acepta tamaño y solapamiento, pero `DocumentProcessor` fija actualmente 1000/200 y no los obtiene de una sección de configuración dedicada.
- **[-] RF5.4:** `EnhancedRagService` limita aproximadamente el contexto a 3000 tokens. La ruta activa de `/chat` no aplica el mismo límite.
- **[-] RF5.5:** Ollama tiene timeout de cinco minutos y los servicios manejan excepciones. No hay un timeout de consulta alineado con el objetivo de respuesta ni cancelación conectada a todo el pipeline de la UI.

## Requisitos no funcionales

### RNF1: Usabilidad

- **[-] RNF1.1:** existe una interfaz simple y feedback visual, pero no hay medición que confirme menos de cinco segundos.
- **[x] RNF1.2:** `/chat` puede utilizarse sin un manual para realizar una consulta básica.
- **[x] RNF1.3:** la UI muestra estados de carga, procesamiento, respuesta y error.
- **[-] RNF1.4:** hay textos de ayuda en la interfaz, pero no existe la guía de usuario prevista en `docs/UserGuide.md`.

### RNF2: Rendimiento

- **[-] RNF2.1:** la arquitectura puede procesar documentos, pero no hay benchmark ni validación con miles de documentos.
- **[-] RNF2.2:** hay scripts de índices, pero la búsqueda activa recorre chunks en memoria.
- **[ ] RNF2.3:** el scanner y la ingesta no tienen procesamiento paralelo comprobado.
- **[-] RNF2.4:** los embeddings se almacenan como vectores, pero no existe una ruta de búsqueda indexada que valide el uso eficiente de memoria.

### RNF3: Mantenibilidad

- **[x] RNF3.1:** la aplicación separa modelos, extractores, procesamiento, RAG, componentes y middleware.
- **[x] RNF3.2:** la conexión, Ollama y las rutas de documentos se configuran externamente.
- **[x] RNF3.3:** existen logging estructurado, middleware de solicitudes, métricas y una página `/statistics`.
- **[-] RNF3.4:** hay documentación técnica parcial en `DOCKER_README.md`, `MONGODB_SETUP.md`, `BlazorApp/INTEGRATION_README.md` y `BlazorApp/LOGGING_IMPLEMENTATION.md`, pero no existen `docs/API.md` ni `docs/Architecture.md`.

### RNF4: Seguridad

- **[-] RNF4.1:** se validan extensión y existencia del archivo, pero no se observa una validación completa de rutas contra una raíz permitida.
- **[-] RNF4.2:** la UI limita la consulta a 2000 caracteres, pero no hay una sanitización específica documentada en el backend.
- **[ ] RNF4.3:** no se observa autenticación, autorización por documento ni control de acceso a fuentes.
- **[x] RNF4.4:** los servicios y middleware registran operaciones y errores mediante logging.

## Criterios de aceptación

- **[-] AC1: Extracción de documentos.** El flujo scanner → extractor → chunker → embedding → MongoDB existe, pero depende de MongoDB/Ollama y no tiene una prueba end-to-end.
- **[-] AC2: Búsqueda funcional.** `/chat` consulta documentos y devuelve contexto relevante, pero usa similitud en memoria y no existe una prueba que confirme la devolución exacta de pasos.
- **[x] AC3: UI moderna.** `/chat` permite enviar con Enter y muestra la respuesta en una tarjeta con borde redondeado.
- **[-] AC4: Formato de respuesta.** Hay Markdown, pasos y fuentes en las piezas disponibles, pero la ruta activa no presenta siempre la página, sección y cita detallada previstas.
- **[ ] AC5: Rendimiento.** No existe una medición reproducible con 100 documentos que confirme una respuesta inferior a tres segundos.

## Estado del checkpoint de cierre

Validación ejecutada con Docker Compose:

- MongoDB, Ollama y Blazor quedaron saludables.
- Se verificaron las colecciones y validadores canónicos.
- Se procesaron 53 documentos.
- Se almacenaron 346 chunks con embeddings de 768 dimensiones.
- Ningún chunk superó 1000 caracteres después del reprocesamiento.
- No quedaron embeddings faltantes.
- `/health` y `/chat` respondieron con HTTP 200.
- Una consulta RAG conocida recuperó 5 chunks y recibió respuesta de Ollama.

La búsqueda vectorial indexada, la caché conectada, el historial y las estadísticas persistentes permanecen fuera del cierre de esta fase.

## Configuración actual

- **Documentos:** `DocumentSettings:ScanPaths`; en Docker la ruta principal es `/app/documents`.
- **Base de datos:** `RagDatabase`, con colecciones `documents`, `chunks` y caché de embeddings según los servicios/scripts.
- **Ollama:** modelo de chat `llama3.2` y modelo de embeddings `nomic-embed-text`, con endpoint configurable.
- **Chunking:** 1000 caracteres y overlap de 200 en el processor actual.
- **Despliegue:** `docker-compose.yml` y `docker-compose-simple.yml`. El compose principal monta la inicialización optimizada de MongoDB; el compose simple es una variante reducida.

## Rutas canónicas

- Chat: `BlazorApp/Components/Pages/Chat.razor`, ruta `/chat`.
- Administración: `BlazorApp/Components/Pages/Admin/Documents.razor`, ruta `/documents`.
- Procesamiento: `BlazorApp/Services/DocumentProcessor.cs` y `BlazorApp/Services/DocumentChunker.cs`.
- Búsqueda activa: `BlazorApp/Services/RagService.cs`.
- Búsqueda mejorada no conectada a `/chat`: `BlazorApp/Services/EnhancedRagService.cs` y `BlazorApp/Services/RagOrchestrator.cs`.
- Proyecto de pruebas: `BlazorAppTests`; no existe `BlazorApp.Tests/IntegrationTests/`.
