use("RagDatabase");

db.runCommand({
  collMod: "documents",
  validator: {
    $jsonSchema: {
      bsonType: "object",
      required: ["FilePath", "FileName", "FileType", "FileSize", "ProcessingStatus", "FileHash", "UploadDate", "ContentHash"],
      properties: {
        FilePath: { bsonType: "string" },
        FileName: { bsonType: "string" },
        FileType: { bsonType: "string" },
        FileSize: { bsonType: "long", minimum: 0 },
        ProcessingStatus: { bsonType: "string", enum: ["Pending", "Processing", "Completed", "Failed"] },
        FileHash: { bsonType: "string" },
        UploadDate: { bsonType: "date" },
        ContentHash: { bsonType: "string" },
        ProcessedAt: { bsonType: ["date", "null"] },
        ChunkCount: { bsonType: "int", minimum: 0 },
        Tags: { bsonType: "array", items: { bsonType: "string" } }
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
      required: ["DocumentId", "ChunkIndex", "StartChar", "EndChar", "ChunkText", "Embedding", "CreatedAt", "ContentType", "WordCount", "CharacterCount"],
      properties: {
        DocumentId: { bsonType: "objectId" },
        ChunkIndex: { bsonType: "int", minimum: 0 },
        StartChar: { bsonType: "int", minimum: 0 },
        EndChar: { bsonType: "int", minimum: 0 },
        ChunkText: { bsonType: "string" },
        Embedding: { bsonType: "array" },
        CreatedAt: { bsonType: "date" },
        ContentType: { bsonType: "string" },
        WordCount: { bsonType: "int", minimum: 0 },
        CharacterCount: { bsonType: "int", minimum: 0 },
        PageNumber: { bsonType: ["int", "null"] }
      }
    }
  },
  validationLevel: "strict",
  validationAction: "error"
});

print("PASCAL_SCHEMA_MIGRATION_OK");
