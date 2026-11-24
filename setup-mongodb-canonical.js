// Configuración canónica de MongoDB para la fase 2.
// Los nombres coinciden con la serialización predeterminada de MongoDB.Driver
// usada por DocumentModel, ChunkModel y EmbeddingCache.

const DB_NAME = "RagDatabase";
const EMBEDDING_DIMENSION = 768;

use(DB_NAME);

function ensureCollection(name, options = {}) {
  const exists = db.getCollectionNames().includes(name);
  if (!exists) {
    db.createCollection(name, options);
    print(`Colección creada: ${name}`);
    return;
  }

  if (options.validator) {
    db.runCommand({
      collMod: name,
      validator: options.validator,
      validationLevel: options.validationLevel || "strict",
      validationAction: options.validationAction || "error"
    });
    print(`Validador actualizado: ${name}`);
  } else {
    print(`Colección existente: ${name}`);
  }
}

function ensureIndex(collectionName, keys, options = {}) {
  try {
    db.getCollection(collectionName).createIndex(keys, options);
  } catch (error) {
    print(`Aviso: no se pudo crear el índice ${options.name || JSON.stringify(keys)} en ${collectionName}: ${error.message}`);
  }
}

const documentValidator = {
  $jsonSchema: {
    bsonType: "object",
    required: ["FilePath", "FileName", "FileType", "FileSize", "ProcessingStatus", "FileHash", "UploadDate", "ContentHash"],
    properties: {
      FilePath: { bsonType: "string", minLength: 1 },
      FileName: { bsonType: "string", minLength: 1 },
      FileType: { bsonType: "string", enum: ["pdf", "docx", "txt", "xlsx"] },
      FileSize: { bsonType: "long", minimum: 0 },
      ProcessingStatus: { bsonType: "string", enum: ["Pending", "Processing", "Completed", "Failed"] },
      FileHash: { bsonType: "string", minLength: 1 },
      UploadDate: { bsonType: "date" },
      ContentHash: { bsonType: "string", minLength: 1 },
      DocumentMetadata: { bsonType: ["object", "null"] },
      Tags: { bsonType: "array", items: { bsonType: "string" } },
      ChunkCount: { bsonType: "int", minimum: 0 },
      ErrorMessage: { bsonType: ["string", "null"] },
      ProcessedAt: { bsonType: ["date", "null"] }
    }
  }
};

const chunkValidator = {
  $jsonSchema: {
    bsonType: "object",
    required: ["DocumentId", "ChunkIndex", "StartChar", "EndChar", "ChunkText", "Embedding", "CreatedAt", "ContentType", "WordCount", "CharacterCount"],
    properties: {
      DocumentId: { bsonType: "objectId" },
      ChunkIndex: { bsonType: "int", minimum: 0 },
      StartChar: { bsonType: "int", minimum: 0 },
      EndChar: { bsonType: "int", minimum: 0 },
      ChunkText: { bsonType: "string", minLength: 1 },
      Embedding: {
        bsonType: "array",
        minItems: EMBEDDING_DIMENSION,
        maxItems: EMBEDDING_DIMENSION,
        items: { bsonType: ["double", "int", "long", "decimal"] }
      },
      ChunkMetadata: { bsonType: ["object", "null"] },
      PageNumber: { bsonType: ["int", "null"], minimum: 1 },
      ContentType: { bsonType: "string" },
      WordCount: { bsonType: "int", minimum: 0 },
      CharacterCount: { bsonType: "int", minimum: 0 },
      CreatedAt: { bsonType: "date" }
    }
  }
};

const cacheValidator = {
  $jsonSchema: {
    bsonType: "object",
    required: ["QueryHash", "QueryText", "Embedding", "CreatedDate", "ExpirationDate"],
    properties: {
      QueryHash: { bsonType: "string", minLength: 1 },
      QueryText: { bsonType: "string", minLength: 1, maxLength: 2000 },
      Embedding: { bsonType: "array", items: { bsonType: ["double", "int", "long", "decimal"] } },
      CreatedDate: { bsonType: "date" },
      ExpirationDate: { bsonType: "date" },
      UsageCount: { bsonType: "int", minimum: 0 },
      LastUsedDate: { bsonType: ["date", "null"] }
    }
  }
};

ensureCollection("documents", { validator: documentValidator, validationLevel: "strict", validationAction: "error" });
ensureCollection("chunks", { validator: chunkValidator, validationLevel: "strict", validationAction: "error" });
ensureCollection("embedding_cache", { validator: cacheValidator, validationLevel: "strict", validationAction: "error" });

// Estas colecciones se mantienen sin un validador rígido hasta la fase 4,
// donde se definirá el esquema de historial por usuario.
ensureCollection("chat_sessions");
ensureCollection("chat_messages");
ensureCollection("rag_metrics");

// Elimina nombres de índices legacy conocidos sin eliminar datos.
for (const [collectionName, indexName] of [
  ["documents", "filePath_1"],
  ["chunks", "documentId_chunkIndex_unique"],
  ["embedding_cache", "expirationDate_1"]
]) {
  try { db.getCollection(collectionName).dropIndex(indexName); } catch (error) { /* no existe */ }
}

ensureIndex("documents", { FilePath: 1 }, { name: "FilePath_1", unique: true });
ensureIndex("documents", { ContentHash: 1 }, { name: "ContentHash_1" });
ensureIndex("documents", { ProcessingStatus: 1, ProcessedAt: -1 }, { name: "ProcessingStatus_1_ProcessedAt_-1" });
ensureIndex("documents", { FileName: "text", FilePath: "text" }, { name: "FileName_FilePath_text" });
ensureIndex("documents", { Tags: 1 }, { name: "Tags_1" });

ensureIndex("chunks", { DocumentId: 1, ChunkIndex: 1 }, { name: "DocumentId_ChunkIndex_unique", unique: true });
ensureIndex("chunks", { DocumentId: 1 }, { name: "DocumentId_1" });
ensureIndex("chunks", { CreatedAt: -1 }, { name: "CreatedAt_-1" });
ensureIndex("chunks", { ChunkText: "text" }, { name: "ChunkText_text" });
ensureIndex("chunks", { PageNumber: 1, DocumentId: 1 }, { name: "PageNumber_DocumentId_1" });

ensureIndex("embedding_cache", { ExpirationDate: 1 }, { name: "ExpirationDate_ttl", expireAfterSeconds: 0 });
ensureIndex("embedding_cache", { QueryHash: 1 }, { name: "QueryHash_unique", unique: true });
ensureIndex("embedding_cache", { CreatedDate: -1 }, { name: "CreatedDate_-1" });

ensureIndex("chat_sessions", { UserId: 1, LastActive: -1 }, { name: "UserId_LastActive_-1" });
ensureIndex("chat_messages", { SessionId: 1, Timestamp: 1 }, { name: "SessionId_Timestamp_1" });
ensureIndex("rag_metrics", { Timestamp: -1 }, { name: "Timestamp_-1" });

print("MongoDB fase 2 configurado con esquema canónico PascalCase.");
print("La búsqueda vectorial queda diferida: la ruta activa usa similitud coseno en memoria.");
print(`Dimensión esperada de embeddings: ${EMBEDDING_DIMENSION}`);
