#!/bin/sh

# Inicializa los modelos que necesita la aplicación RAG en el servicio Ollama.
# Este contenedor no ejecuta ollama serve; se conecta al contenedor ollama por la red Compose.

set -eu

OLLAMA_HOST="${OLLAMA_HOST:-http://ollama:11434}"
export OLLAMA_HOST

printf '%s\n' '========================================'
printf '%s\n' 'Inicializando Ollama con modelos RAG'
printf '%s\n' "Servidor: ${OLLAMA_HOST}"
printf '%s\n' '========================================'

# Esperar a que el cliente pueda consultar el servidor remoto.
ready=0
for i in $(seq 1 30); do
    if ollama list >/dev/null 2>&1; then
        ready=1
        printf '%s\n' 'Ollama está listo'
        break
    fi
    printf 'Esperando Ollama... (%s/30)\n' "$i"
    sleep 2
done

if [ "$ready" -ne 1 ]; then
    printf '%s\n' 'Error: Ollama no está disponible después de 60 segundos' >&2
    exit 1
fi

printf '%s\n' 'Modelos actuales:'
ollama list || true

for model in llama3.2 nomic-embed-text; do
    printf '\nVerificando modelo: %s\n' "$model"

    if ollama list | awk 'NR > 1 {print $1}' | grep -Eq "^${model}(:latest)?$"; then
        printf "El modelo '%s' ya está instalado\n" "$model"
    else
        printf "Descargando modelo '%s'...\n" "$model"
        ollama pull "$model"
        printf "Modelo '%s' descargado\n" "$model"
    fi
done

printf '%s\n' '========================================'
printf '%s\n' 'Modelos disponibles:'
ollama list
printf '%s\n' 'Inicialización de Ollama completada'
printf '%s\n' '========================================'
