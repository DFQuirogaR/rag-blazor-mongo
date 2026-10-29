#!/bin/bash

# ==========================================
# Script de Restore para MongoDB RAG
# ==========================================
# Uso: ./restore.sh [nombre_backup]
# ==========================================

set -e

# Configuración
BACKUP_BASE_DIR="./backups"
MONGO_HOST="localhost"
MONGO_PORT="27017"
MONGO_DATABASE="RagDatabase"

# Función para mostrar uso
usage() {
    echo "Uso: $0 [nombre_backup]"
    echo "  nombre_backup: Nombre del backup a restaurar"
    echo ""
    echo "Ejemplos:"
    echo "  $0 backup_20241215_120000"
    echo "  $0 manual-backup"
    exit 1
}

# Verificar argumento
if [ -z "$1" ]; then
    echo "ERROR: Debes especificar el nombre del backup"
    usage
fi

BACKUP_NAME="$1"
BACKUP_DIR="${BACKUP_BASE_DIR}/${BACKUP_NAME}"

# Verificar que el backup existe
if [ ! -d "${BACKUP_DIR}" ]; then
    echo "ERROR: No se encontró el backup: ${BACKUP_DIR}"
    echo ""
    echo "Backups disponibles:"
    if [ -d "${BACKUP_BASE_DIR}" ]; then
        ls -1 "${BACKUP_BASE_DIR}"
    else
        echo "  No se encontró el directorio de backups"
    fi
    exit 1
fi

echo "========================================"
echo "Restaurando backup de MongoDB"
echo "========================================"
echo "Backup: ${BACKUP_NAME}"
echo "Directorio: ${BACKUP_DIR}"
echo "Base de datos: ${MONGO_DATABASE}"
echo ""

# Verificar que hay archivos .bson
if ! ls "${BACKUP_DIR}/${MONGO_DATABASE}"/*.bson 1> /dev/null 2>&1; then
    echo "ERROR: No se encontraron archivos .bson en el backup"
    exit 1
fi

# Preguntar confirmación
echo "¿Estás seguro de restaurar este backup?"
echo "Esto SOBREESCRIBIRÁ los datos actuales en la base de datos."
read -p "Escribe 'SI' para confirmar: " CONFIRM

if [ "${CONFIRM}" != "SI" ]; then
    echo "Restore cancelado"
    exit 0
fi

# Realizar restore con mongorestore
echo ""
echo "Ejecutando mongorestore..."
mongorestore \
    --host "${MONGO_HOST}" \
    --port "${MONGO_PORT}" \
    --db "${MONGO_DATABASE}" \
    --drop \
    "${BACKUP_DIR}/${MONGO_DATABASE}"

if [ $? -eq 0 ]; then
    echo ""
    echo "========================================"
    echo "Restore completado exitosamente"
    echo "========================================"
else
    echo ""
    echo "========================================"
    echo "ERROR en el restore"
    echo "========================================"
    echo "Verifica que:"
    echo "  1. MongoDB esté corriendo"
    echo "  2. Las variables de conexión sean correctas"
    echo "  3. Tengas permisos de escritura en MongoDB"
    exit 1
fi

# Contar documentos restaurados
echo ""
echo "Contando documentos restaurados..."
mongosh \
    --host "${MONGO_HOST}" \
    --port "${MONGO_PORT}" \
    --quiet \
    --eval "
    db = db.getSiblingDB('${MONGO_DATABASE}');
    var collections = db.getCollectionNames();
    var total = 0;
    collections.forEach(function(col) {
        var count = db[col].count();
        print(col + ': ' + count + ' documentos');
        total += count;
    });
    print('Total: ' + total + ' documentos');
"

echo ""
echo "========================================"
echo "Restore finalizado: ${BACKUP_NAME}"
echo "========================================"