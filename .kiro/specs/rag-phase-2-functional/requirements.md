# RAG Phase 2: Sistema de Documentación Funcional

## Introducción

El sistema actual de RAG solo maneja 3 documentos "await" hardcodeados. La segunda fase requiere un sistema funcional que pueda extraer, procesar y responder basándose en documentos Word/PDF reales que contienen procesos de agentes de primer nivel de un datacenter.

## Visión General

Sistema RAG completo que:
1. Extrae texto de documentos Word (.docx) y PDF de carpetas locales
2. Procesa y almacena documentos con embeddings vectoriales
3. Realiza búsqueda semántica para encontrar pasos específicos
4. Presenta resultados en una UI moderna y amigable

## Requisitos Funcionales

### RF1: Gestión de Documentos
- **RF1.1**: Sistema debe escanear carpetas configuradas para documentos Word/PDF
- **RF1.2**: Extracción de texto de .docx y .pdf manteniendo estructura
- **RF1.3**: Procesamiento por lotes de documentos nuevos/modificados
- **RF1.4**: Logging de documentos procesados con metadata (ruta, fecha, tamaño)

### RF2: Procesamiento y Almacenamiento
- **RF2.1**: Chunking inteligente de documentos (por secciones/páginas)
- **RF2.2**: Generación de embeddings para cada chunk
- **RF2.3**: Almacenamiento en MongoDB con estructura optimizada
- **RF2.4**: Soporte para índices vectoriales para búsqueda eficiente
- **RF2.5**: Metadata por chunk (documento origen, página, tipo)

### RF3: Búsqueda Semántica
- **RF3.1**: Embedding de consultas de usuario
- **RF3.2**: Búsqueda vectorial con similitud coseno
- **RF3.3**: Recuperación de chunks más relevantes
- **RF3.4**: Reranking opcional basado en relevancia
- **RF3.5**: Context window management (ensamblar chunks contiguos)

### RF4: Interfaz de Usuario (/chat)
- **RF4.1**: UI moderna similar a ChatGPT
- **RF4.2**: Input que envíe con tecla Enter
- **RF4.3**: Respuestas en cajas de texto con bordes redondeados
- **RF4.4**: Historial de conversación visual
- **RF4.5**: Indicadores de estado (typing, processing)
- **RF4.6**: Formato Markdown en respuestas
- **RF4.7**: Citas a documentos fuente

### RF5: Gestión de Estado y Rendimiento
- **RF5.1**: Cache de embeddings para documentos ya procesados
- **RF5.2**: Reconocimiento de documentos duplicados/modificados
- **RF5.3**: Configuración de tamaño de chunk y overlap
- **RF5.4**: Límites de tokens en prompts
- **RF5.5**: Timeout y manejo de errores

## Requisitos No Funcionales

### RNF1: Usabilidad
- **RNF1.1**: Tiempo de respuesta < 5 segundos para consultas
- **RNF1.2**: UI intuitiva que no requiera manual
- **RNF1.3**: Feedback visual claro durante procesamiento
- **RNF1.4**: Documentación mínima de interfaz

### RNF2: Rendimiento
- **RNF2.1**: Soporte para miles de documentos
- **RNF2.2**: Búsqueda eficiente con índices
- **RNF2.3**: Procesamiento paralelo de ingestion
- **RNF2.4**: Memoria eficiente en embeddings

### RNF3: Mantenibilidad
- **RNF3.1**: Código modular y testeable
- **RNF3.2**: Configuración externalizada
- **RNF3.3**: Logging estructurado
- **RNF3.4**: Documentación de API interna

### RNF4: Seguridad
- **RNF4.1**: Validación de rutas de acceso
- **RNF4.2**: Sanitización de input de usuario
- **RNF4.3**: Control de acceso a documentos
- **RNF4.4**: Logging de operaciones

## Criterios de Aceptación

### AC1: Extracción de Documentos
- Dado un folder con documentos Word/PDF
- Cuando se ejecuta el proceso de ingestion
- Entonces todos los documentos son extraídos y almacenados en MongoDB

### AC2: Búsqueda Funcional
- Dado un documento con pasos de proceso
- Cuando el usuario pregunta sobre un paso específico
- Entonces el sistema devuelve los pasos exactos del documento

### AC3: UI Moderna
- Dado un usuario en la página /chat
- Cuando escribe una pregunta y presiona Enter
- Entonces la pregunta se envía y la respuesta aparece en caja con borde redondeado

### AC4: Formato de Respuesta
- Dado un resultado de búsqueda relevante
- Cuando el sistema genera respuesta
- Entonces la respuesta muestra:
  - Pasos numerados del documento
  - Formato claro y legible
  - Referencia al documento fuente
  - Estilo visual consistente

### AC5: Performance
- Dado un conjunto de 100 documentos
- Cuando se realiza una consulta
- Entonces la respuesta se genera en < 3 segundos

## Configuración Inicial

### Carpetas de Documentos
- Ruta configurable para documentos Word/PDF
- Soporte para múltiples carpetas
- Exclusión de patrones (.tmp, backup)

### MongoDB Optimizado
- Colección `documents` con metadata
- Colección `chunks` con embeddings
- Índices vectoriales para búsqueda
- Índices por source, type, timestamp

### Configuración de Procesamiento
- Tamaño de chunk: 1000-2000 caracteres
- Overlap: 200 caracteres
- Modelo de embeddings: nomic-embed-text:latest
- Modelo LLM: llama3.2:latest
