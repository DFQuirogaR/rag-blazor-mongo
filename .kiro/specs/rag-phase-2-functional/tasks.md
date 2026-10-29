# RAG Phase 2 Implementation Plan

## Overview

This plan implements the functional RAG system that extracts text from Word/PDF documents, processes them with embeddings, and enables vector-based semantic search with a modern chat interface.

## Task Dependency Graph

```json
{
  "tasks": {
    "1.1": ["1.2", "1.3"],
    "1.2": ["2.1"],
    "1.3": ["2.1"],
    "2.1": ["2.2", "2.3", "3.1"],
    "2.2": ["5.1"],
    "2.3": ["5.1"],
    "3.1": ["3.2", "3.3", "4.1"],
    "3.2": ["5.1"],
    "3.3": ["5.1"],
    "4.1": ["4.2", "4.3"],
    "4.2": ["5.1"],
    "4.3": ["5.1"],
    "5.1": ["5.2", "5.3", "6.1"],
    "5.2": ["6.1"],
    "5.3": ["6.1"],
    "6.1": ["6.2", "6.3"],
    "6.2": [],
    "6.3": []
  },
  "waves": [
    ["1.1"],
    ["1.2", "1.3"],
    ["2.1"],
    ["2.2", "2.3", "3.1"],
    ["3.2", "3.3", "4.1"],
    ["4.2", "4.3"],
    ["5.1"],
    ["5.2", "5.3", "6.1"],
    ["6.2", "6.3"]
  ]
}
```

## Tasks

- [x] 1.1 Configurar MongoDB Optimizado para RAG
  - **Description**: Crear colecciones e �ndices para RAG funcional
  - **Files**: N/A (comandos MongoDB)
  - **Criteria**:
    - Colecci�n `documents` con metadata completa
    - Colecci�n `chunks` con embeddings y metadatos
    - �ndices vectoriales creados
    - �ndices de b�squeda de texto
  - _Requirements: RF2.4, RNF2.2_
  - _Dependencies: None_

- [x] 1.2 Implementar ITextExtractor para Word/PDF
  - **Description**: Crear extractores de texto para .docx y .pdf
  - **Files**:
    - `Services/ITextExtractor.cs`
    - `Services/WordTextExtractor.cs`
    - `Services/PdfTextExtractor.cs`
  - **Criteria**:
    - Extracci�n texto de .docx manteniendo estructura
    - Extracci�n texto de .pdf con informaci�n de p�ginas
    - Manejo de errores por formato no soportado
  - _Requirements: RF1.2, RNF4.1_
  - _Dependencies: 1.1_

- [x] 1.3 Crear modelos de datos para MongoDB
  - **Description**: Modelos C# para documentos y chunks
  - **Files**:
    - `Models/DocumentModel.cs`
    - `Models/ChunkModel.cs`
    - `Models/EmbeddingResult.cs`
  - **Criteria**:
    - Modelos con atributos BSON
    - M�todos de serializaci�n/deserializaci�n
    - Validaci�n de datos
  - _Requirements: RF2.3, RNF3.1_
  - _Dependencies: 1.1_

- [x] 2.1 Implementar DocumentProcessor
  - **Description**: Sistema de ingesti�n de documentos
  - **Files**:
    - `Services/DocumentProcessor.cs`
    - `Services/DocumentChunker.cs`
  - **Criteria**:
    - Chunking inteligente con overlap
    - Generaci�n de embeddings por chunk
    - Almacenamiento en MongoDB con metadata
    - Manejo de documentos duplicados/modificados
  - _Requirements: RF1.3, RF2.1, RF2.2_
  - _Dependencies: 1.1, 1.2, 1.3_

- [x] 2.2 Configurar sistema de escaneo de carpetas
  - **Description**: Monitorizaci�n de carpetas para nuevos documentos
  - **Files**:
    - `Services/DocumentScanner.cs`
    - `appsettings.json` (configuraci�n rutas)
  - **Criteria**:
    - Configuraci�n de rutas m�ltiples
    - Exclusi�n de patrones (.tmp, backup)
    - Procesamiento incremental
    - Logging de operaciones
  - _Requirements: RF1.1, RNF3.2_
  - _Dependencies: 2.1_

- [x] 2.3 Implementar DocumentManagementService
  - **Description**: Servicio de gesti�n de documentos procesados
  - **Files**:
    - `Services/DocumentManagementService.cs`
    - `Pages/Admin/Documents.razor`
  - **Criteria**:
    - Listado de documentos procesados
    - Re-procesamiento de documentos
    - Eliminaci�n de documentos
    - Estad�sticas de procesamiento
  - _Requirements: RF1.4, RNF3.3_
  - _Dependencies: 2.1_

- [x] 3.1 Mejorar RagService con b�squeda real
  - **Description**: B�squeda vectorial con similitud coseno
  - **Files**:
    - `Services/EnhancedRagService.cs`
    - `Extensions/MongoDbExtensions.cs`
  - **Criteria**:
    - B�squeda vectorial eficiente
    - Reranking de resultados
    - Context window management
    - Formateo de respuestas con fuentes
  - _Requirements: RF3.1, RF3.2, RF3.3, RF3.4, RF3.5_
  - _Dependencies: 2.1_

- [x] 3.2 Implementar cache de embeddings
  - **Description**: Cache para embeddings frecuentes
  - **Files**:
    - `Services/EmbeddingCacheService.cs`
    - `Services/IEmbeddingCache.cs`
  - **Criteria**:
    - Cache en memoria con expiraci�n
    - Cache en MongoDB para persistencia
    - Invalidador de cache por cambios
    - M�tricas de hit/miss
  - _Requirements: RF5.1, RNF2.3_
  - _Dependencies: 3.1_

- [-] 3.3 Configurar �ndices vectoriales
  - **Description**: Optimizar MongoDB para b�squeda vectorial
  - **Files**: Scripts MongoDB
  - **Criteria**:
    - �ndices vectoriales creados
    - �ndices compuestos para b�squedas comunes
    - Configuraci�n de r�plica sets (opcional)
    - Backup/restore configurado
  - _Requirements: RF2.4, RNF2.2_
  - _Dependencies: 3.1_

- [-] 4.1 Redise�ar p�gina /chat
  - **Description**: Interfaz moderna tipo ChatGPT
  - **Files**:
    - `Components/Pages/Chat.razor`
    - `Components/Pages/Chat.razor.css`
    - `Components/ChatMessage.razor`
  - **Criteria**:
    - Layout moderno con gradientes
    - Input que env�a con Enter
    - Respuestas en cajas redondeadas
    - Historial de conversaci�n
    - Indicadores visuales de estado
  - _Requirements: RF4.1, RF4.2, RF4.3, RF4.4, RF4.5_
  - _Dependencies: 3.1_

- [x] 4.2 Implementar ChatService con estado
  - **Description**: Gesti�n de estado de conversaci�n
  - **Files**:
    - `Services/ChatSessionService.cs`
    - `Models/ChatSession.cs`
    - `Models/ChatMessage.cs`
  - **Criteria**:
    - Persistencia de conversaciones
    - Contexto hist�rico para LLM
    - Limpieza autom�tica de conversaciones
    - Exportaci�n de conversaciones
  - _Requirements: RF5.2, RNF1.1_
  - _Dependencies: 4.1_

- [x] 4.3 Formateador de respuestas con Markdown
  - **Description**: Respuestas formateadas con Markdown
  - **Files**:
    - `Services/ResponseFormatter.cs`
    - `Components/MarkdownRenderer.razor`
  - **Criteria**:
    - Formato Markdown en respuestas
    - Citas a documentos fuente
    - Pasos numerados para procesos
    - Highlight de informaci�n importante
  - _Requirements: RF4.6, RF4.7_
  - _Dependencies: 4.1_

- [x] 5.1 Integrar componentes principales
  - **Description**: Conectar DocumentProcessor, RagService y UI
  - **Files**:
    - `Program.cs` (actualizaci�n DI)
    - `Services/RagOrchestrator.cs`
  - **Criteria**:
    - DI configurado correctamente
    - Flujo end-to-end funcional
    - Manejo de errores unificado
    - Logging consistente
  - _Requirements: RNF3.1, RNF3.3_
  - _Dependencies: 2.1, 3.1, 4.1_

- [-] 5.2 Implementar pruebas de integraci�n
  - **Description**: Tests para flujos principales
  - **Files**:
    - `BlazorApp.Tests/IntegrationTests/`
    - `TestData/` (documentos de prueba)
  - **Criteria**:
    - Tests de extracci�n de documentos
    - Tests de b�squeda vectorial
    - Tests de UI/UX
    - Tests de performance
  - _Requirements: RNF1.1, RNF2.1_
  - _Dependencies: 5.1_

- [x] 5.3 Configurar logging y monitoreo
  - **Description**: Sistema de observabilidad
  - **Files**:
    - `Program.cs` (config logging)
    - `Middleware/RequestLoggingMiddleware.cs`
  - **Criteria**:
    - Logging estructurado
    - M�tricas de performance
    - Alertas de errores
    - Dashboard de estad�sticas
  - _Requirements: RNF3.3_
  - _Dependencies: 5.1_

- [x] 6.1 Actualizar Docker Compose
  - **Description**: Configuraci�n de despliegue actualizada
  - **Files**:
    - `docker-compose.yml`
    - `Dockerfile` (actualizaciones)
    - `.env.example`
  - **Criteria**:
    - Todos los servicios funcionando
    - Variables de entorno documentadas
    - Health checks implementados
    - Backup configurado
  - _Requirements: RNF3.2, RNF4.1_
  - _Dependencies: 5.1_

- [-] 6.2 Crear documentaci�n t�cnica
  - **Description**: Documentaci�n para desarrolladores
  - **Files**:
    - `README.md` (actualizado)
    - `docs/API.md`
    - `docs/Architecture.md`
  - **Criteria**:
    - Setup instructions completos
    - API documentation
    - Troubleshooting guide
    - Deployment guide
  - _Requirements: RNF3.1, RNF3.4_
  - _Dependencies: 6.1_

- [x] 6.3 Crear gu�a de usuario
  - **Description**: Documentaci�n para usuarios finales
  - **Files**:
    - `docs/UserGuide.md`
    - `docs/Troubleshooting.md`
  - **Criteria**:
    - Gu�a paso a paso de uso
    - FAQ de problemas comunes
    - Screenshots de UI
    - Best practices documentadas
  - _Requirements: RNF1.4_
  - _Dependencies: 4.1_
