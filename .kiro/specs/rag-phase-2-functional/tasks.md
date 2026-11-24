# Plan de implementación de RAG Phase 2

**Estado de la planificación:** reorganizado para separar el bloqueador de salida del backlog posterior.

## Criterio de estado

- **[x]** Implementación comprobable en el código.
- **[-]** Implementación parcial, no integrada o sin evidencia suficiente.
- **[ ]** Pendiente.
- **[GATE]** Debe completarse antes de iniciar la fase 3.
- **[DEFERRED]** Se conserva como trabajo válido, pero no bloquea la fase 3.

La presencia de archivos, timestamps o scripts no demuestra por sí sola que una tarea esté terminada.

## Decisión de salida

No es necesario completar todas las optimizaciones RAG antes de iniciar la autenticación local de la fase 3. La fase 2 debe entregar una línea base funcional y reproducible.

### Bloqueadores antes de Fase 3

Se deben completar estas tareas antes de comenzar la autenticación local:

1. Esquema canónico de documentos y chunks.
2. Compilación y pruebas ejecutables.
3. Ingesta comprobada de un `.docx` y un `.pdf`.
4. Consulta RAG conocida desde `/chat`.
5. Smoke test completo con Docker Compose.
6. Protección básica de rutas de documentos contra rutas arbitrarias.

La optimización vectorial, caché, historial y estadísticas no bloquean la autenticación, siempre que la ruta RAG actual funcione y esté documentada como línea base.

## Tareas de salida obligatoria

- [x] [GATE] **1.1 Unificar el esquema MongoDB**
  - Se creó y ejecutó `setup-mongodb-canonical.js` con nombres PascalCase alineados con los modelos C#.
  - `verify-mongodb-setup.js` encontró las colecciones, validadores e índice TTL; la única advertencia es el índice vectorial diferido.

- [x] [GATE] **1.3 Alinear modelos documentales**
  - Los modelos C# y el script canónico usan propiedades PascalCase.
  - La ingesta Docker confirmó 53 documentos, 346 chunks y embeddings de 768 dimensiones.
  - `ChatSession` y `ChatMessage` quedan disponibles para la fase 4 sin conectar todavía el historial a `/chat`.

- [x] [GATE] **2.1 Procesamiento y límite de chunks**
  - `DocumentChunker` ahora divide también párrafos o tablas que superan el tamaño máximo.
  - La prueba `DocumentChunkerTests` confirma que ningún chunk supera 1000 caracteres.
  - La ingesta Docker terminó sin fallos de embedding después de reprocesar los chunks antiguos.

- [x] [GATE] **2.2 Validar el scanner mínimo**
  - El scanner procesó 53 documentos con `Failed: 0` mediante Docker Compose.
  - Se conserva el procesamiento secuencial; el paralelismo queda diferido.

- [x] [GATE] **5.1 Integración mínima**
  - La aplicación compila correctamente y mantiene la ruta activa `Chat.razor -> RagService`.
  - DI y los servicios principales se resuelven en la compilación.
  - Docker confirmó contenedores saludables para MongoDB, Ollama y Blazor.

- [x] [GATE] **5.2 Pruebas de línea base**
  - Se corrigieron pruebas desactualizadas respecto a los contratos actuales.
  - `dotnet test BlazorAppTests/BlazorAppTests.csproj --no-restore` pasa con 26 pruebas.
  - Las pruebas cubren extractores, `DocumentChunker` y `RagOrchestrator`; la prueba end-to-end se validó mediante el smoke test Docker y los logs de la aplicación.

- [x] [GATE] **6.1 Validar Docker Compose**
  - `docker compose config --quiet` y `docker compose up -d --build` se validaron correctamente.
  - MongoDB, Ollama y Blazor quedaron saludables.
  - La ingesta procesó 53 documentos con 346 chunks y no dejó embeddings faltantes.

- [x] [GATE] **6.2 Actualizar documentación de línea base**
  - `README.md`, `MONGODB_SETUP.md` y `DOCKER_README.md` identifican el script canónico y la similitud en memoria.
  - La fase 2 documenta que la búsqueda vectorial y la caché avanzada quedan diferidas.
  - Los comandos de Docker y verificación están documentados.

- [x] [GATE] **Seguridad documental mínima**
  - El scanner procesa únicamente las rutas configuradas.
  - Los documentos se montan en modo lectura en Docker.
  - MongoDB y Ollama se mantienen en la red del stack, con puertos publicados solo para desarrollo local.

## Tareas que pasan a fases posteriores

Estas tareas no se eliminan. Se retiran del camino crítico de la fase 2 porque pertenecen a una evolución posterior:

- [DEFERRED] **3.1 Búsqueda RAG avanzada**
  - Conectar `EnhancedRagService` o un contrato tipado de RAG.
  - Añadir reranking, ensamblaje de chunks y límite de contexto verificable.
  - Se coordina con la fase 4 cuando se cree el contrato tipado para historial.

- [DEFERRED] **3.2 Caché de embeddings**
  - Integrar `EmbeddingCacheService` en ingesta y consultas.
  - Medir hit/miss y evitar regeneraciones.
  - No bloquea el login local.

- [DEFERRED] **3.3 Búsqueda vectorial indexada**
  - Verificar el índice real y conectar `$vectorSearch` cuando el entorno MongoDB lo soporte.
  - Mantener la similitud en memoria como línea base hasta tener una comparación reproducible.

- [DEFERRED] **4.1 Interfaz conversacional completa**
  - El historial visual y el panel de sesiones se implementan en la fase 4.
  - Se conserva en fase 2 únicamente la validación de chat básico.

- [DEFERRED] **4.2 Historial de chat**
  - Se implementa en `rag-phase-4-history-traceability` con usuario local autenticado y `OwnerUserId`.
  - No duplicar esta tarea en la fase 2.

- [DEFERRED] **4.3 Fuentes detalladas**
  - La fase 2 valida que exista una fuente básica.
  - Las fuentes tipadas con página, sección, cita y relevancia se implementan en la fase 4.

- [DEFERRED] **5.3 Estadísticas avanzadas**
  - El logging y el health check básico son parte del gate.
  - Métricas persistentes, auditoría y dashboard empresarial corresponden a la fase 6.

- [DEFERRED] **6.3 Guía de usuario completa**
  - Mantener instrucciones mínimas de ejecución en `README.md`.
  - Crear guía completa cuando el login, historial y permisos estén implementados.

## Validación del checkpoint F2

Antes de comenzar la fase 3 se debe registrar:

```text
1. dotnet build BlazorApp/BlazorApp.csproj
2. dotnet test BlazorAppTests/BlazorAppTests.csproj
3. docker compose up -d --build
4. verificar docker compose ps
5. procesar un .docx y un .pdf
6. consultar una pregunta conocida en /chat
7. verificar persistencia tras reiniciar blazor-app
8. guardar evidencia de errores o dependencias no disponibles
```

El checkpoint no exige que la búsqueda vectorial, el caché, el historial o las estadísticas persistentes estén terminados. Sí exige que la línea base actual sea reproducible y no tenga errores bloqueadores para iniciar la fase 3.
