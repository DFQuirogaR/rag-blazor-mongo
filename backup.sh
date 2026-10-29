#!/bin/bash

# ==========================================
# Script de Backup para MongoDB RAG
# ==========================================
# Uso: ./backup.sh [nombre_backup]
# Si no se especifica nombre, usa fecha/hora actual
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
    echo "  nombre_backup: (opcional) Nombre para el backup"
    echo "  Si no se especifica, se usará la fecha/hora actual"
    echo ""
    echo "Ejemplos:"
    echo "  $0                # backup con timestamp"
    echo "  $0 manual-backup  # backup con nombre específico"
    exit 1
}

# Obtener nombre del backup
if [ -z "$1" ]; then
    BACKUP_NAME="backup_$(date +%Y%m%d_%H%M%S)"
else
    BACKUP_NAME="$1"
fi

# Crear directorio de backup
BACKUP_DIR="${BACKUP_BASE_DIR}/${BACKUP_NAME}"
mkdir -p "${BACKUP_DIR}"

echo "========================================"
echo "Iniciando backup de MongoDB"
echo "========================================"
echo "Directorio: ${BACKUP_DIR}"
echo "Base de datos: ${MONGO_DATABASE}"
echo ""

# Realizar backup con mongodump
echo "Ejecutando mongodump..."
mongodump \
    --host "${MONGO_HOST}" \
    --port "${MONGO_PORT}" \
    --db "${MONGO_DATABASE}" \
    --out "${BACKUP_DIR}"

if [ $? -eq 0 ]; then
    echo ""
    echo "========================================"
    echo "Backup completado exitosamente"
    echo "========================================"
    echo "Ubicación: ${BACKUP_DIR}"
    echo ""
    echo "Estructura:"
    ls -lh "${BACKUP_DIR}/${MONGO_DATABASE}"
else
    echo ""
    echo "========================================"
    echo "ERROR en el backup"
    echo "========================================"
    echo "Verifica que:"
    echo "  1. MongoDB esté corriendo"
    echo "  2. Las variables de conexión sean correctas"
    echo "  3. Tengas permisos de lectura en MongoDB"
    exit 1
fi

# Mostrar tamaño del backup
BACKUP_SIZE=$(du -sh "${BACKUP_DIR}" | cut -f1)
echo ""
echo "Tamaño del backup: ${BACKUP_SIZE}"
echo ""

# Listar archivos generados
echo "Archivos generados:"
find "${BACKUP_DIR}" -type f -name "*.bson" -o -name "*.json" | while read file; do
    SIZE=$(du -sh "$file" | cut -f1)
    echo "  - $(basename $file) (${SIZE})"
done

echo ""
echo "========================================"
echo "Backup finalizado: ${BACKUP_NAME}"
echo "========================================"