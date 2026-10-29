// Script de configuración de MongoDB para RAG
// Ejecutar con: mongosh < script.js

// Crear base de datos
use RagDatabase;

// Limpiar colecciones existentes (opcional)
db.embeddings.drop();
db.documents.drop();
db.chunks.drop();
db.embedding_cache.drop();

// ===== COLECCIÓN DOCUMENTS =====
// Almacena metadatos de documentos procesados
db.createCollection("documents", {
    validator: {
        $jsonSchema: {
            bsonType: "object",
            required: ["filePath", "fileName", "fileType", "processedDate", "fileSize", "status"],
            properties: {
                filePath: { bsonType: "string" },
                fileName: { bsonType: "string" },
                fileType: { bsonType: "string", enum: ["pdf", "docx", "txt"] },
                processedDate: { bsonType: "date" },
                fileSize: { bsonType: "long" },
                status: { bsonType: "string", enum: ["pending", "processing", "completed", "error"] },
                processingDurationMs: { bsonType: "long" },
                pageCount: { bsonType: "int" },
                originalTextLength: { bsonType: "int" },
                chunkCount: { bsonType: "int" },
                documentHash: { bsonType: "string" },
                lastModifiedDate: { bsonType: "date" },
                metadata: { bsonType: "object" },
                tags: { bsonType: "array", items: { bsonType: "string" } }
            }
        }
    }
});

// Índices para documents
db.documents.createIndex({ filePath: 1 }, { unique: true });
db.documents.createIndex({ processedDate: -1 });
db.documents.createIndex({ status: 1 });
db.documents.createIndex({ fileName: "text" });
db.documents.createIndex({ tags: 1 });

// ===== COLECCIÓN CHUNKS =====
// Almacena chunks de texto con embeddings
db.createCollection("chunks", {
    validator: {
        $jsonSchema: {
            bsonType: "object",
            required: ["documentId", "chunkIndex", "text", "embedding", "createdDate"],
            properties: {
                documentId: { bsonType: "objectId" },
                chunkIndex: { bsonType: "int" },
                text: { bsonType: "string" },
                embedding: { bsonType: "array", items: { bsonType: "double" } },
                createdDate: { bsonType: "date" },
                pageNumber: { bsonType: "int" },
                characterStart: { bsonType: "int" },
                characterEnd: { bsonType: "int" },
                metadata: { bsonType: "object" },
                tokenCount: { bsonType: "int" }
            }
        }
    }
});

// Índice vectorial para búsqueda por embedding
db.chunks.createIndex({ embedding: "vector" }, {
    name: "vector_index",
    "type": "vector",
    "dimension": 768, // nomic-embed-text tiene 768 dimensiones
    "similarity": "cosine"
});

// Índices para chunks
db.chunks.createIndex({ documentId: 1, chunkIndex: 1 }, { unique: true });
db.chunks.createIndex({ createdDate: -1 });
db.chunks.createIndex({ text: "text" });

// ===== COLECCIÓN EMBEDDING_CACHE =====
// Cache para embeddings de queries frecuentes
db.createCollection("embedding_cache", {
    validator: {
        $jsonSchema: {
            bsonType: "object",
            required: ["queryHash", "embedding", "createdDate", "expirationDate"],
            properties: {
                queryHash: { bsonType: "string" },
                embedding: { bsonType: "array", items: { bsonType: "double" } },
                createdDate: { bsonType: "date" },
                expirationDate: { bsonType: "date" },
                queryText: { bsonType: "string" },
                usageCount: { bsonType: "int" },
                lastUsedDate: { bsonType: "date" }
            }
        }
    }
});

// Índices para embedding_cache
db.embedding_cache.createIndex({ queryHash: 1 }, { unique: true });
db.embedding_cache.createIndex({ expirationDate: 1 }, { expireAfterSeconds: 0 });
db.embedding_cache.createIndex({ createdDate: -1 });

// ===== COLECCIÓN CHAT_SESSIONS =====
// Para almacenar conversaciones del chat
db.createCollection("chat_sessions", {
    validator: {
        $jsonSchema: {
            bsonType: "object",
            required: ["sessionId", "createdDate", "status"],
            properties: {
                sessionId: { bsonType: "string" },
                createdDate: { bsonType: "date" },
                status: { bsonType: "string", enum: ["active", "archived", "deleted"] },
                title: { bsonType: "string" },
                userId: { bsonType: "string" },
                lastActivityDate: { bsonType: "date" },
                messageCount: { bsonType: "int" },
                metadata: { bsonType: "object" },
                tags: { bsonType: "array", items: { bsonType: "string" } }
            }
        }
    }
});

db.chat_sessions.createIndex({ sessionId: 1 }, { unique: true });
db.chat_sessions.createIndex({ createdDate: -1 });
db.chat_sessions.createIndex({ status: 1 });
db.chat_sessions.createIndex({ userId: 1 });

// ===== COLECCIÓN CHAT_MESSAGES =====
db.createCollection("chat_messages", {
    validator: {
        $jsonSchema: {
            bsonType: "object",
            required: ["sessionId", "messageIndex", "role", "content", "timestamp"],
            properties: {
                sessionId: { bsonType: "string" },
                messageIndex: { bsonType: "int" },
                role: { bsonType: "string", enum: ["user", "assistant", "system"] },
                content: { bsonType: "string" },
                timestamp: { bsonType: "date" },
                sources: { bsonType: "array", items: { bsonType: "object" } },
                metadata: { bsonType: "object" },
                processingTimeMs: { bsonType: "long" }
            }
        }
    }
});

db.chat_messages.createIndex({ sessionId: 1, messageIndex: 1 }, { unique: true });
db.chat_messages.createIndex({ sessionId: 1, timestamp: -1 });
db.chat_messages.createIndex({ timestamp: -1 });

print("✅ Base de datos configurada exitosamente!");
print("Colecciones creadas: documents, chunks, embedding_cache, chat_sessions, chat_messages");