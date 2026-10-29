// MongoDB Optimizado para RAG - Script de Configuración Mejorado
// Ejecutar con: mongosh mongodb://localhost:27017 < setup-mongodb-optimized.js
// O: mongosh --file setup-mongodb-optimized.js

// Configuración global
const DB_NAME = "RagDatabase";
const VECTOR_DIMENSION = 768; // nomic-embed-text tiene 768 dimensiones
const EMBEDDING_MODEL = "nomic-embed-text";
const EMBEDDING_EXPIRATION_SECONDS = 3600 * 24; // 24 horas

use(DB_NAME);

// ============================================
// 1. LIMPIEZA PREVIA (OPCIONAL - COMENTADA)
// ============================================
// Descomentar solo si se necesita reiniciar desde cero
// print("⚠️  ATENCIÓN: Esta acción borrará todos los datos existentes");
// print("Confirmar: Si desea continuar, descomentar las líneas en el script");

// db.embeddings.drop();
// db.documents.drop();
// db.chunks.drop();
// db.embedding_cache.drop();
// db.chat_sessions.drop();
// db.chat_messages.drop();
// db.chat_sessions.drop();
// db.chat_messages.drop();

// ============================================
// 2. CREACIÓN DE COLECCIÓN: documents
// ============================================
print("📄 Creando colección 'documents'...");

db.createCollection("documents", {
  validator: {
    $jsonSchema: {
      bsonType: "object",
      required: ["filePath", "fileName", "fileType", "processedDate", "fileSize", "status"],
      properties: {
        filePath: { 
          bsonType: "string",
          description: "Ruta completa del archivo"
        },
        fileName: { 
          bsonType: "string",
          description: "Nombre del archivo"
        },
        fileType: { 
          bsonType: "string",
          enum: ["pdf", "docx", "txt", "xlsx"],
          description: "Tipo de documento (pdf, docx, txt, xlsx)"
        },
        fileSize: { 
          bsonType: "long",
          minimum: 0,
          description: "Tamaño del archivo en bytes"
        },
        contentHash: {
          bsonType: "string",
          pattern: "^[a-f0-9]{64}$",
          description: "Hash SHA256 del contenido para detección de duplicados"
        },
        processedDate: { 
          bsonType: "date",
          description: "Fecha de procesamiento"
        },
        processingDurationMs: {
          bsonType: "long",
          minimum: 0,
          description: "Duración del procesamiento en milisegundos"
        },
        pageCount: {
          bsonType: "int",
          minimum: 1,
          description: "Número de páginas en el documento"
        },
        chunkCount: {
          bsonType: "int",
          minimum: 0,
          description: "Número de chunks generados"
        },
        status: {
          bsonType: "string",
          enum: ["pending", "processing", "completed", "error"],
          description: "Estado del procesamiento"
        },
        errorMessage: {
          bsonType: "string",
          description: "Mensaje de error (solo cuando status='error')"
        },
        metadata: {
          bsonType: "object",
          properties: {
            title: { bsonType: "string" },
            author: { bsonType: "string" },
            createdDate: { bsonType: "date" },
            modifiedDate: { bsonType: "date" },
            subject: { bsonType: "string" },
            keywords: { bsonType: "array", items: { bsonType: "string" } },
            pageCount: { bsonType: "int" },
            documentProvider: { bsonType: "string" }
          }
        },
        tags: {
          bsonType: "array",
          items: { bsonType: "string" },
          description: "Etiquetas personalizadas para clasificación"
        },
        lastModifiedDate: {
          bsonType: "date",
          description: "Última modificación del archivo original"
        },
        scanPath: {
          bsonType: "string",
          description: "Ruta de escaneo donde se encontró el documento"
        }
      }
    }
  }
});

// Índices para documents
print("🔗 Creando índices para 'documents'...");

db.documents.createIndex(
  { filePath: 1 },
  { 
    name: "filePath_1",
    unique: true,
    background: true 
  }
);

db.documents.createIndex(
  { processedDate: -1 },
  { 
    name: "processedDate_-1",
    background: true 
  }
);

db.documents.createIndex(
  { status: 1 },
  { 
    name: "status_1",
    background: true 
  }
);

db.documents.createIndex(
  { fileName: "text" },
  { 
    name: "fileName_text",
    background: true 
  }
);

db.documents.createIndex(
  { tags: 1 },
  { 
    name: "tags_1",
    background: true 
  }
);

db.documents.createIndex(
  { 
    status: 1, 
    processedDate: -1 
  },
  {
    name: "status_1_processedDate_-1",
    background: true
  }
);

db.documents.createIndex(
  { contentHash: 1 },
  {
    name: "contentHash_1",
    background: true
  }
);

// ============================================
// 3. CREACIÓN DE COLECCIÓN: chunks
// ============================================
print("📄 Creando colección 'chunks'...");

db.createCollection("chunks", {
  validator: {
    $jsonSchema: {
      bsonType: "object",
      required: ["documentId", "chunkIndex", "content", "embedding", "createdDate"],
      properties: {
        documentId: {
          bsonType: "objectId",
          description: "Referencia al documento original"
        },
        chunkIndex: {
          bsonType: "int",
          minimum: 0,
          description: "Índice del chunk en el documento"
        },
        startChar: {
          bsonType: "int",
          minimum: 0,
          description: "Posición inicial en el texto original"
        },
        endChar: {
          bsonType: "int",
          minimum: 0,
          description: "Posición final en el texto original"
        },
        content: {
          bsonType: "string",
          minLength: 10,
          description: "Texto del chunk (mínimo 10 caracteres)"
        },
        embedding: {
          bsonType: "array",
          items: {
            bsonType: "double"
          },
          description: "Vector de embeddings"
        },
        createdDate: {
          bsonType: "date",
          description: "Fecha de creación del chunk"
        },
        pageNumber: {
          bsonType: "int",
          minimum: 1,
          description: "Número de página (si aplica)"
        },
        metadata: {
          bsonType: "object",
          properties: {
            sectionTitle: { bsonType: "string" },
            isHeading: { bsonType: "bool" },
            level: { bsonType: "int", minimum: 1, maximum: 6 },
            keywords: { bsonType: "array", items: { bsonType: "string" } },
            headingHierarchy: { bsonType: "array", items: { bsonType: "string" } }
          }
        },
        tokenCount: {
          bsonType: "int",
          minimum: 1,
          description: "Número de tokens aproximados"
        }
      }
    }
  }
});

// Índice vectorial para búsqueda por embedding
print("🎯 Creando índice vectorial para 'chunks'...");

try {
  db.chunks.createIndex(
    { embedding: "vector" },
    {
      name: "vector_index_cosine",
      "type": "vector",
      "dimension": VECTOR_DIMENSION,
      "similarity": "cosine",
      "engine": "ivf", // Inverted File Index - mejor para our caso
      "indexes": {
        "ivf": {
          "quantizer": {
            "type": "flat" // flat es más preciso, ivf es más rápido
          }
        }
      }
    }
  );
  print("✅ Índice vectorial 'vector_index_cosine' creado exitosamente");
} catch (error) {
  print("❌ Error al crear índice vectorial: " + error.message);
  print("💡 Asegurar que MongoDB 7.0+ esté instalado");
}

// Índices estándar para chunks
print("🔗 Creando índices estándar para 'chunks'...");

db.chunks.createIndex(
  { documentId: 1, chunkIndex: 1 },
  {
    name: "documentId_chunkIndex_unique",
    unique: true,
    background: true
  }
);

db.chunks.createIndex(
  { documentId: 1 },
  {
    name: "documentId_1",
    background: true
  }
);

db.chunks.createIndex(
  { chunkIndex: 1 },
  {
    name: "chunkIndex_1",
    background: true
  }
);

db.chunks.createIndex(
  { createdDate: -1 },
  {
    name: "createdDate_-1",
    background: true
  }
);

db.chunks.createIndex(
  { content: "text" },
  {
    name: "content_text",
    background: true
  }
);

db.chunks.createIndex(
  { pageNumber: 1, documentId: 1 },
  {
    name: "pageNumber_documentId_1",
    background: true
  }
);

// ============================================
// 4. CREACIÓN DE COLECCIÓN: embedding_cache
// ============================================
print("📄 Creando colección 'embedding_cache'...");

db.createCollection("embedding_cache", {
  validator: {
    $jsonSchema: {
      bsonType: "object",
      required: ["queryHash", "embedding", "createdDate", "expirationDate"],
      properties: {
        queryHash: {
          bsonType: "string",
          pattern: "^[a-f0-9]{32}$",
          description: "Hash MD5 de la consulta"
        },
        embedding: {
          bsonType: "array",
          items: {
            bsonType: "double"
          },
          description: "Vector de embeddings"
        },
        createdDate: {
          bsonType: "date",
          description: "Fecha de creación"
        },
        expirationDate: {
          bsonType: "date",
          description: "Fecha de expiración"
        },
        queryText: {
          bsonType: "string",
          description: "Texto original (opcional, para debugging)"
        },
        usageCount: {
          bsonType: "int",
          minimum: 0,
          description: "Número de usos"
        },
        lastUsedDate: {
          bsonType: "date",
          description: "Último uso"
        },
        model: {
          bsonType: "string",
          enum: [EMBEDDING_MODEL, "text-embedding-ada-002"],
          description: "Modelo de embeddings usado"
        }
      }
    }
  }
});

// TTL index para auto-expiración de cache
print("⏳ Creando TTL index para auto-expiración...");

db.embedding_cache.createIndex(
  { expirationDate: 1 },
  {
    name: "expirationDate_1",
    expireAfterSeconds: 0,
    background: true
  }
);

// Índices adicionales para embedding_cache
db.embedding_cache.createIndex(
  { queryHash: 1 },
  {
    name: "queryHash_1",
    unique: true,
    background: true
  }
);

db.embedding_cache.createIndex(
  { createdDate: -1 },
  {
    name: "createdDate_-1",
    background: true
  }
);

db.embedding_cache.createIndex(
  { usageCount: -1 },
  {
    name: "usageCount_-1",
    background: true
  }
);

// ============================================
// 5. CREACIÓN DE COLECCIÓN: chat_sessions
// ============================================
print("📄 Creando colección 'chat_sessions'...");

db.createCollection("chat_sessions", {
  validator: {
    $jsonSchema: {
      bsonType: "object",
      required: ["sessionId", "createdDate", "status"],
      properties: {
        sessionId: {
          bsonType: "string",
          pattern: "^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$",
          description: "UUID v4 de la sesión"
        },
        createdDate: {
          bsonType: "date",
          description: "Fecha de creación de la sesión"
        },
        status: {
          bsonType: "string",
          enum: ["active", "archived", "deleted"],
          description: "Estado de la sesión"
        },
        title: {
          bsonType: "string",
          minLength: 5,
          description: "Título generado por LLM"
        },
        userId: {
          bsonType: "string",
          description: "ID de usuario (opcional)"
        },
        lastActivityDate: {
          bsonType: "date",
          description: "Última actividad en la sesión"
        },
        messageCount: {
          bsonType: "int",
          minimum: 0,
          description: "Número de mensajes en la sesión"
        },
        metadata: {
          bsonType: "object",
          properties: {
            systemPrompt: { bsonType: "string" },
            maxContextTokens: { bsonType: "int", minimum: 100, maximum: 32000 },
            temperature: { bsonType: "double", minimum: 0, maximum: 2 },
            topP: { bsonType: "double", minimum: 0, maximum: 1 }
          }
        },
        tags: {
          bsonType: "array",
          items: { bsonType: "string" },
          description: "Etiquetas de categoría"
        },
        documentsReferenced: {
          bsonType: "array",
          items: {
            bsonType: "objectId"
          },
          description: "IDs de documentos citados en la sesión"
        }
      }
    }
  }
});

// Índices para chat_sessions
print("🔗 Creando índices para 'chat_sessions'...");

db.chat_sessions.createIndex(
  { sessionId: 1 },
  {
    name: "sessionId_1",
    unique: true,
    background: true
  }
);

db.chat_sessions.createIndex(
  { createdDate: -1 },
  {
    name: "createdDate_-1",
    background: true
  }
);

db.chat_sessions.createIndex(
  { status: 1 },
  {
    name: "status_1",
    background: true
  }
);

db.chat_sessions.createIndex(
  { userId: 1 },
  {
    name: "userId_1",
    background: true
  }
);

db.chat_sessions.createIndex(
  { lastActivityDate: -1 },
  {
    name: "lastActivityDate_-1",
    background: true
  }
);

db.chat_sessions.createIndex(
  { status: 1, lastActivityDate: -1 },
  {
    name: "status_1_lastActivityDate_-1",
    background: true
  }
);

// ============================================
// 6. CREACIÓN DE COLECCIÓN: chat_messages
// ============================================
print("📄 Creando colección 'chat_messages'...");

db.createCollection("chat_messages", {
  validator: {
    $jsonSchema: {
      bsonType: "object",
      required: ["sessionId", "messageIndex", "role", "content", "timestamp"],
      properties: {
        sessionId: {
          bsonType: "string",
          pattern: "^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$",
          description: "Referencia a la sesión"
        },
        messageIndex: {
          bsonType: "int",
          minimum: 0,
          description: "Índice del mensaje en la sesión"
        },
        role: {
          bsonType: "string",
          enum: ["user", "assistant", "system"],
          description: "Rol del mensaje"
        },
        content: {
          bsonType: "string",
          minLength: 1,
          description: "Contenido del mensaje"
        },
        timestamp: {
          bsonType: "date",
          description: "Hora de envío"
        },
        sources: {
          bsonType: "array",
          items: {
            bsonType: "object",
            properties: {
              documentId: { bsonType: "objectId" },
              fileName: { bsonType: "string" },
              chunkIndex: { bsonType: "int" },
              relevanceScore: { bsonType: "double", minimum: 0, maximum: 1 },
              snippet: { bsonType: "string" }
            }
          },
          description: "Fuentes citadas (RAG)"
        },
        metadata: {
          bsonType: "object",
          properties: {
            promptTokens: { bsonType: "int", minimum: 0 },
            completionTokens: { bsonType: "int", minimum: 0 },
            totalTokens: { bsonType: "int", minimum: 0 },
            processingTimeMs: { bsonType: "long", minimum: 0 },
            model: { bsonType: "string" },
            temperature: { bsonType: "double" }
          }
        },
        model: {
          bsonType: "string",
          enum: ["llama3.2", "llama3", "gpt-4", "gpt-3.5-turbo"],
          description: "Modelo LLM usado"
        }
      }
    }
  }
});

// Índices para chat_messages
print("🔗 Creando índices para 'chat_messages'...");

db.chat_messages.createIndex(
  { sessionId: 1, messageIndex: 1 },
  {
    name: "sessionId_messageIndex_unique",
    unique: true,
    background: true
  }
);

db.chat_messages.createIndex(
  { sessionId: 1, timestamp: -1 },
  {
    name: "sessionId_timestamp_-1",
    background: true
  }
);

db.chat_messages.createIndex(
  { timestamp: -1 },
  {
    name: "timestamp_-1",
    background: true
  }
);

// ============================================
// 7. COLECCIÓN DE METRICS (OPCIONAL)
// ============================================
print("📊 Creando colección 'rag_metrics' para monitoreo...");

db.createCollection("rag_metrics", {
  validator: {
    $jsonSchema: {
      bsonType: "object",
      required: ["timestamp", "metricType"],
      properties: {
        timestamp: { bsonType: "date" },
        metricType: {
          bsonType: "string",
          enum: ["query", "embedding", "processing", "storage"]
        },
        sessionId: {
          bsonType: "string"
        },
        metrics: {
          bsonType: "object",
          properties: {
            queryTimeMs: { bsonType: "long" },
            embeddingTimeMs: { bsonType: "long" },
            processingTimeMs: { bsonType: "long" },
            documentsRetrieved: { bsonType: "int" },
            tokensUsed: { bsonType: "int" },
            memoryUsageMB: { bsonType: "double" }
          }
        }
      }
    }
  }
});

db.rag_metrics.createIndex({ timestamp: -1 }, { name: "timestamp_-1", background: true });
db.rag_metrics.createIndex({ metricType: 1 }, { name: "metricType_1", background: true });
db.rag_metrics.createIndex({ metricType: 1, timestamp: -1 }, { name: "metricType_timestamp_-1", background: true });

// ============================================
// 8. VERIFICACIÓN FINAL
// ============================================
print("\n" + "=".repeat(60));
print("VERIFICACIÓN DE CONFIGURACIÓN");
print("=".repeat(60));

const collections = db.getCollectionNames();
print("\n📋 Colecciones creadas:");
collections.forEach(col => print(`   - ${col}`));

print("\n🔍 Índices por colección:");
collections.forEach(col => {
  const indexes = db[col].getIndexes();
  print(`   ${col}:`);
  indexes.forEach(idx => {
    const keyStr = JSON.stringify(idx.key).replace(/"|\{|}/g, "");
    print(`     - ${idx.name} (${keyStr})`);
  });
});

print("\n✅ Configuración completada exitosamente!");
print(`\n💡 Próximo paso: Ejecutar la aplicación Blazor o importar datos de prueba`);
print(`\n📚 Documentación de queries disponibles en:\n   - https://www.mongodb.com/docs/manual/core/vector-search/\n   - https://www.mongodb.com/docs/atlas/atlas-vector-search/`);


// ============================================
// 9. VALIDADORES COMPATIBLES CON LOS MODELOS C# ACTUALES
// ============================================
// El esquema original usaba processedDate/status/content/createdDate, mientras
// que la aplicación serializa uploadDate/processingStatus/chunkText/createdAt.
// Este collMod mantiene la instalación compatible con DocumentModel y ChunkModel.

db.runCommand({
  collMod: "documents",
  validator: {
    $jsonSchema: {
      bsonType: "object",
      required: ["filePath", "fileName", "fileType", "fileSize", "processingStatus", "fileHash", "uploadDate", "contentHash"],
      properties: {
        filePath: { bsonType: "string" },
        fileName: { bsonType: "string" },
        fileType: { bsonType: "string" },
        fileSize: { bsonType: "long", minimum: 0 },
        processingStatus: { bsonType: "string", enum: ["Pending", "Processing", "Completed", "Failed"] },
        fileHash: { bsonType: "string" },
        uploadDate: { bsonType: "date" },
        contentHash: { bsonType: "string" },
        processedAt: { bsonType: ["date", "null"] },
        chunkCount: { bsonType: "int", minimum: 0 },
        tags: { bsonType: "array", items: { bsonType: "string" } }
      }
    }
  },
  validationLevel: "strict",
  validationAction: "error"
});

db.runCommand({
  collMod: "chunks",
  validator: {
    $jsonSchema: {
      bsonType: "object",
      required: ["documentId", "chunkIndex", "startChar", "endChar", "chunkText", "embedding", "createdAt", "contentType", "wordCount", "characterCount"],
      properties: {
        documentId: { bsonType: "objectId" },
        chunkIndex: { bsonType: "int", minimum: 0 },
        startChar: { bsonType: "int", minimum: 0 },
        endChar: { bsonType: "int", minimum: 0 },
        chunkText: { bsonType: "string" },
        embedding: { bsonType: "array" },
        createdAt: { bsonType: "date" },
        contentType: { bsonType: "string" },
        wordCount: { bsonType: "int", minimum: 0 },
        characterCount: { bsonType: "int", minimum: 0 },
        pageNumber: { bsonType: ["int", "null"] }
      }
    }
  },
  validationLevel: "strict",
  validationAction: "error"
});

print("✅ Validadores alineados con los modelos C# actuales");

// Índices compatibles con las propiedades PascalCase serializadas por MongoDB.Driver.
try { db.documents.dropIndex("filePath_1"); } catch (error) { print("Índice legacy filePath_1 no encontrado"); }
db.documents.createIndex({ FilePath: 1 }, { name: "FilePath_1", unique: true });
try { db.chunks.dropIndex("documentId_chunkIndex_unique"); } catch (error) { print("Índice legacy documentId_chunkIndex_unique no encontrado"); }
db.chunks.createIndex({ DocumentId: 1, ChunkIndex: 1 }, { name: "DocumentId_ChunkIndex_unique", unique: true });
print("✅ Índices alineados con los modelos C# actuales");

// Validadores finales usando los nombres PascalCase predeterminados por MongoDB.Driver.
db.runCommand({
  collMod: "documents",
  validator: {
    $jsonSchema: {
      bsonType: "object",
      required: ["FilePath", "FileName", "FileType", "FileSize", "ProcessingStatus", "FileHash", "UploadDate", "ContentHash"],
      properties: {
        FilePath: { bsonType: "string" }, FileName: { bsonType: "string" }, FileType: { bsonType: "string" },
        FileSize: { bsonType: "long", minimum: 0 },
        ProcessingStatus: { bsonType: "string", enum: ["Pending", "Processing", "Completed", "Failed"] },
        FileHash: { bsonType: "string" }, UploadDate: { bsonType: "date" }, ContentHash: { bsonType: "string" },
        ProcessedAt: { bsonType: ["date", "null"] }, ChunkCount: { bsonType: "int", minimum: 0 },
        Tags: { bsonType: "array", items: { bsonType: "string" } }
      }
    }
  }, validationLevel: "strict", validationAction: "error"
});
db.runCommand({
  collMod: "chunks",
  validator: {
    $jsonSchema: {
      bsonType: "object",
      required: ["DocumentId", "ChunkIndex", "StartChar", "EndChar", "ChunkText", "Embedding", "CreatedAt", "ContentType", "WordCount", "CharacterCount"],
      properties: {
        DocumentId: { bsonType: "objectId" }, ChunkIndex: { bsonType: "int", minimum: 0 },
        StartChar: { bsonType: "int", minimum: 0 }, EndChar: { bsonType: "int", minimum: 0 },
        ChunkText: { bsonType: "string" }, Embedding: { bsonType: "array" }, CreatedAt: { bsonType: "date" },
        ContentType: { bsonType: "string" }, WordCount: { bsonType: "int", minimum: 0 },
        CharacterCount: { bsonType: "int", minimum: 0 }, PageNumber: { bsonType: ["int", "null"] }
      }
    }
  }, validationLevel: "strict", validationAction: "error"
});
