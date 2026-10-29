# MongoDB Setup para RAG - Guía Completa

## Visión General

Esta guía describe la configuración de MongoDB optimizada para el sistema RAG (Retrieval Augmented Generation) con soporte para búsqueda vectorial, embeddings y metadata de documentos.

---

## Tabla de Contenidos

1. [Requisitos Previos](#requisitos-previos)
2. [Arquitectura de Base de Datos](#arquitectura-de-base-de-datos)
3. [Estrategia de Instalación](#estrategia-de-instalación)
4. [Configuración Rápida (Docker)](#configuración-rápida-docker)
5. [Configuración Manual](#configuración-manual)
6. [Verificación de Setup](#verificación-de-setup)
7. [Queries de Ejemplo](#queries-de-ejemplo)
8. [Troubleshooting](#troubleshooting)
9. [Best Practices](#best-practices)

---

## Requisitos Previos

### Software
- **MongoDB 7.0+** (necesario para búsqueda vectorial)
  - MongoDB Community Server
  - MongoDB Atlas (cloud)
- **Mongo Shell (mongosh)**

### Verificación
```bash
mongosh --version
# Debe mostrar: mongosh v2.x o superior

mongosh --eval "db.adminCommand('ping')"
# Debe mostrar: { ok: 1 }
```

### Hardware Recomendado
- **Embedding dimension**: 768 (nomic-embed-text)
- **Memoria**: Mínimo 4GB RAM (8GB+ recomendado)
- **Disco**: SSD recomendado para mejor performance

---

## Arquitectura de Base de Datos

### Colecciones Principales

| Colección | Descripción | Tamaño Estimado |
|-----------|-------------|-----------------|
| `documents` | Metadatos de documentos procesados | ~1-10KB por documento |
| `chunks` | Text chunks con embeddings | ~20-50KB por chunk |
| `embedding_cache` | Cache de embeddings de queries | ~4KB por entry |
| `chat_sessions` | Sesiones de chat | ~2-10KB por sesión |
| `chat_messages` | Mensajes de conversación | ~1-5KB por mensaje |

### Relaciones

```
documents (1) ──── (*) chunks
chat_sessions (1) ──── (*) chat_messages
chunks (1) ──── (1) documents
```

---

## Estrategia de Instalación

### Opción 1: Docker Compose (Recomendado)

MongoDB se ejecuta automáticamente con Docker Compose:

```bash
docker-compose up -d mongodb
```

### Opción 2: Instalación Local

1. Descargar MongoDB Community Server
2. Instalar en sistema local
3. Iniciar servicio

### Opción 3: MongoDB Atlas (Cloud)

1. Crear cuenta en [MongoDB Atlas](https://www.mongodb.com/cloud/atlas)
2. Crear cluster gratuito (M0)
3. Configurar network access
4. Obtener connection string

---

## Configuración Rápida (Docker)

### Paso 1: Iniciar MongoDB

```bash
# Iniciar solo MongoDB
docker-compose up -d mongodb

# O iniciar todo el stack
docker-compose up -d
```

### Paso 2: Esperar a que MongoDB esté listo

```bash
# Esperar 10 segundos
timeout 10
```

### Paso 3: Ejecutar script de setup

```bash
# Windows
setup-mongodb.bat

# Linux/Mac
./setup-mongodb.sh
```

### Paso 4: Verificar setup

```bash
mongosh --file verify-mongodb-setup.js
```

---

## Configuración Manual

### Paso 1: Conectar a MongoDB

```bash
# Local
mongosh mongodb://localhost:27017

# Docker
docker exec -it mongodb_rag mongosh

# Con connection string
mongosh "mongodb://localhost:27017"
```

### Paso 2: Ejecutar script de setup

```javascript
// Dentro de mongosh
use("RagDatabase");

// Ejecutar script
load("setup-mongodb-optimized.js");
```

### Paso 3: Verificar

```javascript
// Ver colecciones
show collections;

// Ver índices
db.chunks.getIndexes();

// Verificar índice vectorial
db.chunks.getIndexSpecs().find(index => index.name === "vector_index");
```

---

## Verificación de Setup

### Script de Verificación Automática

```bash
mongosh --file verify-mongodb-setup.js
```

### Verificación Manual

#### 1. Verificar Colecciones

```javascript
use("RagDatabase");
show collections;

// Debe mostrar:
// - chat_messages
// - chat_sessions
// - chunks
// - documents
// - embedding_cache
```

#### 2. Verificar Índices Vectoriales

```javascript
db.chunks.getIndexes().forEach(idx => {
  if (idx.key && idx.key.embedding === "vector") {
    print("✓ Índice vectorial encontrado:");
    print(JSON.stringify(idx, null, 2));
  }
});
```

#### 3. Verificar Validadores

```javascript
db.getCollectionInfos({name: "documents"})[0].options.validator
db.getCollectionInfos({name: "chunks"})[0].options.validator
```

#### 4. Verificar TTL Index

```javascript
db.embedding_cache.getIndexes().forEach(idx => {
  if (idx.expireAfterSeconds) {
    print(`TTL: ${idx.name} - expira en ${idx.expireAfterSeconds}s`);
  }
});
```

---

## Queries de Ejemplo

### 1. Búsqueda Vectorial (RAG Core)

```javascript
// Buscar chunks similares a un embedding
const queryEmbedding = [0.1, -0.2, 0.3, ...]; // Tu embedding

db.chunks.aggregate([
  {
    $vectorSearch: {
      index: "vector_index_cosine",
      path: "embedding",
      queryVector: queryEmbedding,
      numCandidates: 100,
      limit: 5
    }
  },
  {
    $project: {
      content: 1,
      score: { $meta: "vectorSearchScore" },
      documentId: 1
    }
  }
]);
```

### 2. Buscar Documentos por Metadata

```javascript
// Documentos procesados recientemente
db.documents.find({
  status: "completed",
  processedDate: { $gte: new Date(Date.now() - 7 * 24 * 60 * 60 * 1000) }
}).sort({ processedDate: -1 });

// Documentos por etiqueta
db.documents.find({ tags: "proceso" });

// Documentos por filetype
db.documents.find({ fileType: "pdf" });
```

### 3. Buscar Chunks por Documento

```javascript
// Todos los chunks de un documento específico
db.chunks.find({
  documentId: ObjectId("60d5f78b9c1f3e001c9b4567")
}).sort({ chunkIndex: 1 });

// Chunks de una página específica
db.chunks.find({
  documentId: ObjectId("60d5f78b9c1f3e001c9b4567"),
  pageNumber: 5
});
```

### 4. Buscar por Texto Completo

```javascript
// Búsqueda de texto en chunks
db.chunks.find({
  $text: { $search: "proceso de onboarding" }
});

// Con puntuación de relevancia
db.chunks.find({
  $text: { $search: "proceso de onboarding" }
}, {
  score: { $meta: "textScore" }
}).sort({ score: { $meta: "textScore" } });
```

### 5. Consultas de Chat

```javascript
// Últimas sesiones activas
db.chat_sessions.find({
  status: "active"
}).sort({ lastActivityDate: -1 }).limit(10);

// Mensajes de una sesión
db.chat_messages.find({
  sessionId: "uuid-v4-here"
}).sort({ timestamp: 1 });

// Mensajes con fuentes (RAG)
db.chat_messages.find({
  "sources.0": { $exists: true }
});
```

### 6. Consultas de Cache

```javascript
// Embeddings que expiran pronto
db.embedding_cache.find({
  expirationDate: { $lte: new Date(Date.now() + 3600000) } // 1 hora
});

// Embeddings más usados
db.embedding_cache.find().sort({ usageCount: -1 }).limit(10);
```

---

## Troubleshooting

### Error: "No module named 'pymongo'"

**Causa**: MongoDB Shell no está instalado

**Solución**:
```bash
# Windows
choco install mongosh

# macOS
brew install mongosh

# Ubuntu/Debian
sudo apt-get install mongodb-mongosh
```

### Error: "Index not found: vector_index"

**Causa**: El índice vectorial no fue creado

**Solución**:
```javascript
// Verificar índices actuales
db.chunks.getIndexes();

// Re-ejecutar script de setup
load("setup-mongodb-optimized.js");
```

### Error: "Dimension mismatch in vector search"

**Causa**: Los embeddings tienen dimensiones diferentes

**Solución**:
```javascript
// Verificar dimensiones
db.chunks.aggregate([
  { $project: { dim: { $size: "$embedding" } } },
  { $group: { _id: "$dim", count: { $sum: 1 } } }
]);

// Debe mostrar: { _id: 768, count: X } (todos 768 dimensiones)
```

### Error: "TTL index not found"

**Causa**: El índice TTL no fue creado

**Solución**:
```javascript
// Verificar índices
db.embedding_cache.getIndexes();

// Verificar expireAfterSeconds
db.embedding_cache.getIndexes().find(idx => idx.expireAfterSeconds);
```

### Error: "Unauthorized"

**Causa**: Falta autenticación

**Solución**:
```javascript
// Crear usuario
use("admin");
db.createUser({
  user: "rag_admin",
  pwd: "strong_password",
  roles: [
    { role: "readWrite", db: "RagDatabase" },
    { role: "dbAdmin", db: "RagDatabase" }
  ]
});

// Conectar con credenciales
mongosh "mongodb://rag_admin:strong_password@localhost:27017/RagDatabase"
```

---

## Best Practices

### 1. Mantenimiento de Índices

```javascript
// Ver uso de índices
db.chunks.aggregate([
  { $indexStats: {} },
  { $project: { name: 1, accesses: 1, ops: 1 } }
]);

// Eliminar índices no utilizados
db.chunks.dropIndex("index_name");
```

### 2. Optimización de Queries

```javascript
// Usar explain() para analizar queries
db.chunks.find({
  status: "completed"
}).explain("executionStats");
```

### 3. Gestión de Espacio

```javascript
// Ver uso de disco por colección
db.stats();

// Ver uso por colección
db.getCollectionNames().forEach(col => {
  print(`${col}: ${db[col].stats().size / 1024 / 1024} MB`);
});
```

### 4. Backup y Restore

```bash
# Backup
mongodump --db RagDatabase --out /backup

# Restore
mongorestore --db RagDatabase /backup/RagDatabase
```

### 5. Monitoreo

```javascript
// Ver operaciones lentas
db.setProfilingLevel(1, { slowms: 1000 });

// Ver queries lentos
db.system.profile.find().sort({ ts: -1 }).limit(10);
```

---

## Próximos Pasos

1. ✅ Ejecutar script de setup: `setup-mongodb.bat`
2. ✅ Verificar colecciones e índices
3. ✅ Probar queries de búsqueda vectorial
4. ⏭️ Iniciar aplicación Blazor
5. ⏭️ Importar documentos de prueba

---

## Referencias

- [MongoDB Vector Search](https://www.mongodb.com/docs/atlas/atlas-vector-search/)
- [MongoDB Shell](https://www.mongodb.com/docs/mongodb-shell/)
- [JSON Schema Validation](https://www.mongodb.com/docs/manual/core/schema-validation/)
- [TTL Indexes](https://www.mongodb.com/docs/manual/core/index-ttl/)

---

## Soporte

Si encuentras problemas:
1. Revisa la sección de [Troubleshooting](#troubleshooting)
2. Ejecuta el script de verificación: `verify-mongodb-setup.js`
3. Revisa los logs de MongoDB: `docker logs mongodb_rag`