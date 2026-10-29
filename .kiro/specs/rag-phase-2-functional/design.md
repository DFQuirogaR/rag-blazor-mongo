# Diseño del Sistema RAG Funcional

## Arquitectura del Sistema

```
┌─────────────────────────────────────────────────────────┐
│                     Componentes del Sistema              │
├─────────────────────────────────────────────────────────┤
│  Frontend (Blazor Server) │   Backend (C# .NET10)        │
│  ┌──────────────────────┐   ┌──────────────────────┐  │
│  │  Chat UI Moderna     │   │  RagService mejorado  │  │
│  │  • Enter para enviar  │   │  • Búsqueda vectorial│  │
│  │  • Cajas redondeadas  │   │  • Gestión de context │  │
│  └──────────────────────┘   └──────────────────────┘  │
│                           │                            │
├─────────────────────────────────────────────────────────┤
│               Procesamiento de Documentos               │
│  ┌──────────────────────┐   ┌──────────────────────┐  │
│  │  DocumentProcessor  │   │  TextExtractor       │  │
│  │  • Chunking          │   │  • Word (.docx)     │  │
│  │  • Embedding         │   │  • PDF               │  │
│  └──────────────────────┘   └──────────────────────┘  │
│                           │                            │
├─────────────────────────────────────────────────────────┤
│                 Almacenamiento                          │
│  ┌──────────────────────┐   ┌──────────────────────┐  │
│  │  MongoDB            │   │  FileSystem           │  │
│  │  • Chunks           │   │  • Documentos fuente   │  │
│  │  • Embeddings      │   │  • Metadata          │  │
│  │  • Metadata        │   └──────────────────────┘  │
│  └──────────────────────┘                             │
│                           │                            │
├─────────────────────────────────────────────────────────┤
│                 Infraestructura                        │
│  ┌──────────────────────┐   ┌──────────────────────┐  │
│  │  Ollama             │   │  Docker Compose      │  │
│  │  • LLM             │   │  • MongoDB           │  │
│  │  • Embeddings     │   │  • Ollama            │  │
│  └──────────────────────┘   │  • Blazor App       │  │
│                           └──────────────────────┘  │
└─────────────────────────────────────────────────────────┘
```

## Diseño de Base de Datos

### Colección `documents`
```json
{
  "_id": ObjectId,
  "file_path": "string",
  "file_name": "string",
  "file_type": "docx|pdf",
  "file_size": number,
  "content_hash": "string",
  "processed_at": ISODate,
  "metadata": {
    "title": "string",
    "author": "string",
    "created_date": ISODate,
    "modified_date": ISODate,
    "page_count": number
  },
  "processing_status": "pending|processing|completed|error",
  "error_message": "string"
}
```

### Colección `chunks`
```json
{
  "_id": ObjectId,
  "document_id": ObjectId,
  "chunk_index": number,
  "start_char": number,
  "end_char": number,
  "content": "string",
  "embedding": [number], // vector de embeddings
  "metadata": {
    "page_number": number,
    "section_title": "string",
    "is_heading": boolean
  },
  "created_at": ISODate
}
```

### Índices
- **Vectorial**: `chunks.embedding` (índice vectorial para búsqueda)
- **Búsqueda texto**: `chunks.content` (text index)
- **Documento origen**: `chunks.document_id`
- **Timestamp**: `documents.processed_at`

## Diseño de UI (/chat)

### Layout
```html
<div class="chat-container">
  <!-- Header -->
  <div class="chat-header">
    <h3>Asistente RAG - Procesos Datacenter</h3>
  </div>
  
  <!-- Historial de mensajes -->
  <div class="chat-history">
    <!-- Mensajes del usuario -->
    <div class="message user">
      <div class="bubble">¿Cuál es el proceso para X?</div>
    </div>
    
    <!-- Respuestas del sistema -->
    <div class="message assistant">
      <div class="bubble rounded-box">
        <p><strong>Paso 1:</strong> Descripción del paso...</p>
        <p><strong>Paso 2:</strong> Descripción del paso...</p>
        <small class="source-info">Fuente: documento.docx, página 5</small>
      </div>
    </div>
  </div>
  
  <!-- Input area -->
  <div class="input-area">
    <input type="text" 
           @bind="userQuery" 
           @bind:event="oninput"
           @onkeypress="HandleKeyPress"
           placeholder="Pregunta sobre procesos del datacenter..." 
           class="chat-input" />
    <button @onclick="AskRag" class="send-button">
      <i class="bi bi-send"></i>
    </button>
  </div>
  
  <!-- Indicador de estado -->
  <div class="status-indicator" @if="isProcessing">
    <div class="typing-animation">Procesando...</div>
  </div>
</div>
```

### Componentes de CSS
```css
/* Chat container */
.chat-container {
  max-width: 800px;
  margin: 0 auto;
  padding: 20px;
  background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
  border-radius: 20px;
}

/* Mensajes */
.message {
  margin-bottom: 20px;
}

.message.user {
  text-align: right;
}

.message.assistant {
  text-align: left;
}

/* Burbujas de chat */
.bubble {
  display: inline-block;
  padding: 12px 18px;
  max-width: 80%;
  word-wrap: break-word;
}

.user .bubble {
  background: #007bff;
  color: white;
  border-radius: 18px 18px 4px 18px;
}

.assistant .bubble {
  background: #f8f9fa;
  color: #212529;
  border: 2px solid #dee2e6;
  border-radius: 18px 18px 18px 4px;
  box-shadow: 0 2px 8px rgba(0,0,0,0.1);
}

/* Caja redondeada para respuestas */
.rounded-box {
  border-radius: 15px;
  border: 2px solid #6c757d;
  padding: 16px;
  background: linear-gradient(135deg, #f8f9fa 0%, #e9ecef 100%);
}

/* Input area */
.input-area {
  display: flex;
  gap: 10px;
  margin-top: 20px;
}

.chat-input {
  flex: 1;
  padding: 12px 16px;
  border: 2px solid #6c757d;
  border-radius: 25px;
  font-size: 16px;
  transition: border-color 0.3s;
}

.chat-input:focus {
  outline: none;
  border-color: #007bff;
  box-shadow: 0 0 0 3px rgba(0,123,255,0.25);
}

.send-button {
  width: 50px;
  height: 50px;
  border-radius: 50%;
  background: #007bff;
  color: white;
  border: none;
  cursor: pointer;
  transition: background 0.3s;
}

.send-button:hover {
  background: #0056b3;
}

/* Indicador de estado */
.status-indicator {
  text-align: center;
  padding: 10px;
  color: #6c757d;
}

.typing-animation::after {
  content: '...';
  animation: typing 1.5s infinite;
}

@keyframes typing {
  0% { content: '.'; }
  33% { content: '..'; }
  66% { content: '...'; }
  100% { content: '.'; }
}

/* Información de fuente */
.source-info {
  display: block;
  margin-top: 8px;
  font-size: 12px;
  color: #6c757d;
  font-style: italic;
}
```

## Diseño de Clases C#

### DocumentProcessor
```csharp
public class DocumentProcessor
{
    private readonly ITextExtractor _textExtractor;
    private readonly ITextEmbeddingGenerationService _embeddingService;
    private readonly IMongoDatabase _database;
    
    public DocumentProcessor(
        ITextExtractor textExtractor,
        ITextEmbeddingGenerationService embeddingService,
        IMongoDatabase database)
    {
        _textExtractor = textExtractor;
        _embeddingService = embeddingService;
        _database = database;
    }
    
    public async Task ProcessDocumentAsync(string filePath)
    {
        // 1. Extraer texto del documento
        var documentContent = await _textExtractor.ExtractTextAsync(filePath);
        
        // 2. Crear documento en DB
        var document = new DocumentModel(filePath, documentContent);
        await SaveDocumentAsync(document);
        
        // 3. Chunking del contenido
        var chunks = ChunkDocument(documentContent);
        
        // 4. Generar embeddings para cada chunk
        foreach (var chunk in chunks)
        {
            var embedding = await _embeddingService.GenerateEmbeddingAsync(chunk.Content);
            chunk.Embedding = embedding;
            await SaveChunkAsync(chunk);
        }
    }
    
    private List<ChunkModel> ChunkDocument(string content, int chunkSize = 1500, int overlap = 200)
    {
        // Implementación de chunking inteligente
        // Considerar párrafos, secciones, etc.
    }
}
```

### TextExtractor
```csharp
public interface ITextExtractor
{
    Task<string> ExtractTextAsync(string filePath);
    bool SupportsFileType(string extension);
}

public class WordTextExtractor : ITextExtractor
{
    public bool SupportsFileType(string extension) => 
        extension.Equals(".docx", StringComparison.OrdinalIgnoreCase);
    
    public async Task<string> ExtractTextAsync(string filePath)
    {
        // Usar DocumentFormat.OpenXml para .docx
        // Mantener estructura (títulos, párrafos)
    }
}

public class PdfTextExtractor : ITextExtractor
{
    public bool SupportsFileType(string extension) => 
        extension.Equals(".pdf", StringComparison.OrdinalIgnoreCase);
    
    public async Task<string> ExtractTextAsync(string filePath)
    {
        // Usar iTextSharp o similar para PDF
        // Mantener información de páginas
    }
}
```

### EnhancedRagService
```csharp
public class EnhancedRagService
{
    private readonly IMongoCollection<ChunkModel> _chunksCollection;
    private readonly ITextEmbeddingGenerationService _embeddingService;
    private readonly IChatCompletionService _chatService;
    
    public async Task<string> QueryDocumentsAsync(string userQuery, int maxResults = 5)
    {
        // 1. Embedding de la consulta
        var queryEmbedding = await _embeddingService.GenerateEmbeddingAsync(userQuery);
        
        // 2. Búsqueda vectorial en MongoDB
        var relevantChunks = await SearchVectorialAsync(queryEmbedding, maxResults);
        
        // 3. Ensamblar contexto
        var context = BuildContext(relevantChunks);
        
        // 4. Generar prompt con contexto
        var prompt = BuildPrompt(userQuery, context);
        
        // 5. LLM para respuesta final
        var response = await GenerateResponseAsync(prompt);
        
        // 6. Formatear respuesta con fuentes
        return FormatResponse(response, relevantChunks);
    }
    
    private async Task<List<ChunkModel>> SearchVectorialAsync(float[] queryEmbedding, int limit)
    {
        // Búsqueda vectorial usando similitud coseno
        // MongoDB 7+ soporta búsqueda vectorial nativa
    }
}
```

## Flujos de Trabajo

### 1. Ingestion de Documentos
```
[Archivo Word/PDF] → [TextExtractor] → [Texto plano] → 
[Chunking] → [Embedding] → [MongoDB] → [Ready for query]
```

### 2. Procesamiento de Consulta
```
[User Query] → [Query Embedding] → [Vector Search] → 
[Top K Chunks] → [Context Assembly] → [LLM Prompt] → 
[LLM Response] → [Formatted Output]
```

### 3. UI Interacción
```
[User types] → [Enter detection] → [Send to backend] → 
[Show typing indicator] → [Process RAG] → [Update UI] → 
[Display formatted response]
```

## Consideraciones de Implementación

### Para MongoDB
- Usar MongoDB 7+ para soporte nativo de búsqueda vectorial
- Índices compuestos para optimizar búsqueda
- Considerar Atlas Search si escalabilidad es crítica

### Para Ollama
- Configurar timeout apropiado para embeddings
- Cache de embeddings pre-generados
- Modelos optimizados para español (si aplica)

### Para Blazor
- Estado reactivo para UI
- Componentes reutilizables
- Manejo de errores amigable
- Loading states claros

### Para Despliegue
- Docker Compose actualizado
- Variables de entorno para configuración
- Health checks para servicios
- Logging estructurado
