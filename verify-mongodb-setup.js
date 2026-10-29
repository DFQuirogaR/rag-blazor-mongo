// verify-mongodb-setup.js
// Script para verificar la configuración de MongoDB para RAG

use("RagDatabase");

print("\n" + "=".repeat(60));
print("VERIFICACIÓN DE CONFIGURACIÓN MongoDB para RAG");
print("=".repeat(60));

// Contador de errores
let errors = 0;
let warnings = 0;

// ============================================
// VERIFICACIÓN DE COLECCIONES
// ============================================
print("\n[1/5] Verificando colecciones...");

const requiredCollections = [
  "documents",
  "chunks",
  "embedding_cache",
  "chat_sessions",
  "chat_messages"
];

const actualCollections = db.getCollectionNames();

requiredCollections.forEach(col => {
  if (actualCollections.includes(col)) {
    const count = db[col].countDocuments();
    print(`   ✓ ${col}: ${count} documentos`);
  } else {
    print(`   ✗ ${col}: NO ENCONTRADA`);
    errors++;
  }
});

// ============================================
// VERIFICACIÓN DE ÍNDICES VECTORIALES
// ============================================
print("\n[2/5] Verificando índices vectoriales...");

try {
  const vectorIndexes = db.chunks.getIndexes().filter(idx => 
    idx.key && (idx.key.embedding === "vector" || idx.type === "vector")
  );
  
  if (vectorIndexes.length > 0) {
    vectorIndexes.forEach(idx => {
      print(`   ✓ Índice vectorial encontrado: ${idx.name}`);
      print(`     - Dimensión: ${idx.numDimensions || "768"}`);
      print(`     - Similaridad: ${idx.similarity || "cosine"}`);
    });
  } else {
    print(`   ✗ NO SE ENCONTRARON ÍNDICES VECTORIALES`);
    print(`   ⚠️  La búsqueda vectorial NO estará disponible`);
    warnings++;
  }
} catch (error) {
  print(`   ✗ ERROR al verificar índices vectoriales: ${error.message}`);
  warnings++;
}

// ============================================
// VERIFICACIÓN DE ÍNDICES DE TEXTO
// ============================================
print("\n[3/5] Verificando índices de texto...");

const textIndexes = db.chunks.getIndexes().filter(idx => 
  idx.key && Object.keys(idx.key).some(key => idx.key[key] === "text")
);

if (textIndexes.length > 0) {
  textIndexes.forEach(idx => {
    print(`   ✓ Índice de texto: ${idx.name}`);
  });
} else {
  print(`   ⚠️  No se encontraron índices de texto en 'chunks'`);
  warnings++;
}

// ============================================
// VERIFICACIÓN DE ÍNDICES DE CACHE TTL
// ============================================
print("\n[4/5] Verificando TTL index para cache...");

const ttlIndexes = db.embedding_cache.getIndexes().filter(idx => 
  idx.expireAfterSeconds !== undefined
);

if (ttlIndexes.length > 0) {
  ttlIndexes.forEach(idx => {
    print(`   ✓ TTL index: ${idx.name} (expira en ${idx.expireAfterSeconds}s)`);
  });
} else {
  print(`   ✗ NO SE ENCONTRARON TTL INDEX`);
  print(`   ⚠️  El cache no se autolimpiará automáticamente`);
  errors++;
}

// ============================================
// VERIFICACIÓN DE VALIDADORES
// ============================================
print("\n[5/5] Verificando validadores JSON Schema...");

const collectionsWithValidators = [];
const collectionsWithoutValidators = [];

actualCollections.forEach(col => {
  const collInfo = db.getCollectionInfos({name: col})[0];
  if (collInfo && collInfo.options && collInfo.options.validator) {
    collectionsWithValidators.push(col);
    const validatorCount = Object.keys(collInfo.options.validator.$jsonSchema.properties || {}).length;
    print(`   ✓ ${col}: ${validatorCount} validaciones`);
  } else {
    if (col !== "system.indexes" && col !== "system.sessions") {
      collectionsWithoutValidators.push(col);
    }
  }
});

if (collectionsWithoutValidators.length > 0) {
  print(`   ⚠️  Colecciones sin validadores: ${collectionsWithoutValidators.join(", ")}`);
}

// ============================================
// RESUMEN FINAL
// ============================================
print("\n" + "=".repeat(60));
print("RESUMEN");
print("=".repeat(60));

if (errors === 0 && warnings === 0) {
  print("\n✅ ¡Todo está correctamente configurado!");
  print("\nLa base de datos está lista para usar con el sistema RAG.");
} else if (errors === 0) {
  print(`\n⚠️  Configuración completa con ${warnings} advertencia(s)`);
  print("\nEl sistema funcionará, pero algunas funcionalidades pueden estar limitadas:");
  print("  - Búsqueda vectorial puede no estar disponible");
  print("  - Cache no se autolimpiará automáticamente");
  print("  - Verificar los mensajes de advertencia arriba");
} else {
  print(`\n❌ Configuración incompleta: ${errors} error(es), ${warnings} advertencia(s)`);
  print("\nPor favor, revise los mensajes de error arriba y vuelva a ejecutar el script de setup.");
}

print("\n" + "=".repeat(60));

// ============================================
// PRUEBAS BÁSICAS (OPCIONAL)
// ============================================
print("\n[OPT] Pruebas básicas de consulta...");

try {
  // Verificar que las colecciones están vacías (esperado para nueva setup)
  const docsCount = db.documents.countDocuments();
  const chunksCount = db.chunks.countDocuments();
  print(`   • documentos: ${docsCount} registros`);
  print(`   • chunks: ${chunksCount} registros`);
  
  if (docsCount > 0 || chunksCount > 0) {
    print(`   ⚠️  Las colecciones ya contienen datos`);
  }
  
} catch (error) {
  print(`   ✗ Error en pruebas básicas: ${error.message}`);
}

print("\n✅ Verificación completada");
