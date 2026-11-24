# Requisitos: localización y respuestas multilingües

## Alcance

Esta fase añade interfaz en español, inglés y portugués y solicita al RAG que genere la explicación en el idioma elegido. Depende de identidad y del historial de la fase 4. No añade otro modelo ni otro proveedor LLM.

## Decisiones

- Locales soportados exactamente: `es`, `en`, `pt`.
- Locale inicial: `es`.
- Preferencia de interfaz: navegador y sesión autenticada.
- Consulta original: se conserva sin traducir.
- Títulos, identificadores técnicos y citas: se conservan sin modificar.
- Un único modelo Ollama configurado inicialmente.

## Requisitos

### R1. Selector y preferencia

1. El selector debe mostrar solamente `es`, `en` y `pt`.
2. Un locale ausente o inválido debe convertirse en `es`.
3. Cambiar el idioma debe actualizar la interfaz sin reiniciar la aplicación.
4. La preferencia debe conservarse al navegar por `/chat`, `/documents` y `/statistics`.
5. El idioma de la sesión debe almacenarse cuando exista una sesión activa.

### R2. Recursos de interfaz

1. Debe existir un recurso completo para cada locale.
2. Deben traducirse encabezados, botones, placeholders, estados de carga, estados vacíos, validaciones y errores.
3. Una clave ausente debe usar el recurso español y registrar la clave.
4. Los valores dinámicos deben formatearse según el locale.
5. La UI no debe mostrar marcadores de formato sin resolver.

### R3. Respuesta RAG

1. La solicitud tipada debe recibir exactamente el locale seleccionado.
2. El texto explicativo debe generarse en ese locale.
3. La consulta original debe permanecer sin cambios en el historial.
4. Nombres de documentos, identificadores técnicos y citas deben conservarse en su forma original.
5. Una limitación de idioma no debe presentarse como respuesta generada correctamente.

### R4. Pruebas

Deben existir pruebas para los tres locales, fallback, formato dinámico, prompt/contrato y conservación de fuentes.

## Fuera de alcance

- Traducción automática de documentos.
- Traducción de citas.
- Modelo diferente por idioma.
- Detección automática como sustituto del selector.

## Criterio de finalización

Un usuario puede cambiar entre `es`, `en` y `pt`, navegar sin perder la preferencia, recibir respuestas en el idioma seleccionado y consultar fuentes sin que sus nombres o citas sean alterados.
