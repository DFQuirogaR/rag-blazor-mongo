# Implementation Plan: historial y localización RAG

> **Estado:** plan histórico supersedido. No marques estas tareas como ruta de ejecución. La organización vigente está en `.kiro/specs/README.md` y en las carpetas `rag-phase-3-identity-authorization`, `rag-phase-4-history-traceability`, `rag-phase-5-localization` y `rag-phase-6-audit-statistics`.

## Overview

Conectar `/chat` con el historial persistido por alcance anónimo en MongoDB, añadir búsqueda y administración de sesiones, localizar la interfaz en `es`/`en`/`pt` y pasar el idioma seleccionado al flujo RAG. Las tareas están ordenadas para que los contratos, modelos y límites existan antes de integrar la UI y las pruebas de extremo a extremo.

## Criterio de estado

- **[x]** Implementación comprobable en el código.
- **[-]** Implementación parcial, no integrada o con evidencia insuficiente.
- **[ ]** Pendiente.

Este documento se conserva solo como referencia histórica. Las tareas vigentes están separadas por fase en las carpetas indicadas arriba y no deben ejecutarse desde este archivo. Las tareas marcadas con `*` pertenecían al plan anterior.

## Instrucción para la ejecución

Convertir el diseño de la funcionalidad en una serie de prompts para un LLM de generación de código que implemente cada paso con progreso incremental. Cada prompt debe construir sobre los anteriores y terminar conectando los componentes. Solo se incluyen tareas de escritura, modificación o prueba de código; no se implementa código durante la creación de este documento.

## Dependencias

```text
1.1 -> 1.2 -> 1.3 -> 1.4 -> 1.5
1.3, 1.4 -> 2.1 -> 2.2 -> 2.3
2.3 -> 2.4 -> 2.5 -> 2.6 -> 2.7 -> 2.8 -> 2.9 -> 2.10 -> 2.11
1.2, 2.3 -> 3.1 -> 3.2 -> 3.3 -> 3.4
2.3 -> 4.1 -> 4.2 -> 4.3 -> 4.4 -> 4.5 -> 4.6
2.3, 3.2 -> 5.1 -> 5.2 -> 5.3 -> 5.4 -> 5.5 -> 5.6 -> 5.7
2.10 -> 6.1 -> 6.2 -> 6.3, 6.4, 6.5 -> 6.6 -> 6.7
2.1, 2.2, 6.2 -> 7.1 -> 7.2 -> 7.3 -> 7.4
1.3, 2.3 -> 8.1 -> 8.2 -> 8.3 -> 8.4 -> 8.5
1.5, 2.11, 3.4, 4.6, 5.7, 6.7, 7.4, 8.5 -> 9.1 -> 9.2
9.2, 1.3, 2.3 -> 10.1 -> 10.2, 10.3, 10.4 -> 10.5
10.5 -> 11.1 -> 11.2, 11.3, 11.4 -> 11.5
```

## Tasks

- [ ] 1. Preparar modelos, configuración y persistencia base
  - [ ] 1.1 Crear opciones y tipos de dominio del historial
    - Crear `HistoryOptions` con validación de `MaxQueryLength` (1..2000), `MaxMessagesPerSession` (1..10000), `DefaultPageSize` (1..50), timeout (1..120), retención, locales admitidos y configuración de limpieza.
    - Crear tipos compartidos para `SupportedLocale`, `SessionStatus`, `EntryStatus`, resultados de operación, paginación, alcance y formato de exportación.
    - **Verificación:** la validación de configuración rechaza valores fuera de rango al iniciar y no sustituye silenciosamente un valor inválido.
    - **Requisitos:** 2.4, 4.5, 9.1, 9.2, 9.6.

  - [ ] 1.2 Extender los modelos MongoDB y las referencias de fuente
    - Modificar `BlazorApp/Models/ChatSession.cs` y `BlazorApp/Models/ChatMessage.cs` para conservar compatibilidad y añadir `PrivacyScopeId`, estado, `EntryId`, locale de respuesta, estado del intento, reutilización, motivo seguro y metadatos.
    - Crear el modelo `SourceReference` con identificador, nombre original, ubicación, cita, fecha de recuperación, relevancia y disponibilidad actual.
    - Mantener la consulta original exactamente en `Content` y conservar `SourceDocuments` para compatibilidad legacy.
    - **Verificación:** la serialización round-trip conserva consultas, respuestas, estados, locales, fuentes y metadatos sin incluir secretos ni traducir títulos o citas.
    - **Requisitos:** 1.2, 1.3, 7.6, 8.1, 8.6, 9.2.
    - **Propiedades relacionadas:** Property 4, Property 9, Property 12, Property 17.

  - [ ] 1.3 Actualizar `ChatSessionService` para alcance, límites e índices
    - Modificar `BlazorApp/Services/ChatSessionService.cs` o extraer repositorios internos para aplicar `PrivacyScopeId` como filtro obligatorio, ordenar sesiones por `LastActive` descendente y paginar entre 1 y 50.
    - Consultar `chat_messages` para búsqueda y carga ordenada, en lugar de depender solo de `ChatSession.Messages`.
    - Aplicar el límite de mensajes antes de insertar y propagar `CancellationToken` y timeout configurado.
    - Crear o verificar índices `{ PrivacyScopeId: 1, Status: 1, LastActive: -1 }` y `{ SessionId: 1, Timestamp: 1 }` mediante el mecanismo de inicialización existente.
    - **Verificación:** las lecturas y mutaciones sin alcance se rechazan; la paginación nunca devuelve más de 50; el límite de mensajes no altera mensajes previos.
    - **Requisitos:** 2.1, 2.4, 2.6, 2.7, 9.2, 9.3, 9.4, 9.6.
    - **Propiedades relacionadas:** Property 7, Property 18.

  - [ ] 1.4 Implementar `HistoryScopeProvider`
    - Crear el servicio scoped de alcance anónimo para emitir un identificador aleatorio en servidor y conservarlo en una cookie protegida.
    - No aceptar `UserId`, `sessionId` ni valores enviados por el cliente como sustitutos del alcance.
    - Registrar el servicio en `BlazorApp/Program.cs` y exponer solo la resolución del alcance actual para los casos de uso.
    - **Verificación:** dos cookies de alcance distintas no pueden leer las sesiones de la otra; el campo opcional `UserId` no cambia la autorización.
    - **Requisitos:** 2.6, 2.7, 9.3, 9.4.

  - [ ] 1.5 Checkpoint de modelos y persistencia
    - Compilar la solución y ejecutar las pruebas disponibles de modelos y persistencia.
    - Verificar que los índices, opciones y nombres de colecciones son compatibles con `chat_sessions` y `chat_messages` antes de integrar contratos de aplicación.
    - **Verificación:** la compilación pasa y cualquier dependencia externa no disponible queda identificada sin convertirla en un fallo de comportamiento.

- [ ] 2. Crear contratos tipados y servicios de aplicación
  - [ ] 2.1 Definir el contrato tipado de RAG
    - Crear `RagQueryRequest`, `RagResponse`, `RagResponseStatus`, `RagModelMetadata` y el contrato que use `HistoryService` para invocar RAG.
    - Incluir consulta original, `ResponseLocale`, máximo de resultados, `CorrelationId` y `CancellationToken`.
    - **Verificación:** el contrato no contiene una consulta traducida ni permite un locale fuera de `es`, `en` o `pt`.
    - **Requisitos:** 7.1, 7.5, 7.6, 8.3, 9.6.
    - **Propiedades relacionadas:** Property 16.

  - [ ] 2.2 Adaptar `RagService` a respuestas estructuradas
    - Modificar `BlazorApp/Services/RagService.cs` para conservar la recuperación actual, devolver fuentes tipadas y preservar metadatos del modelo/contexto.
    - Añadir al prompt el locale solicitado, la preservación de identificadores, nombres de documentos y citas, y la prohibición de inventar contexto.
    - Mantener temporalmente `QueryAsync(string)` como adaptador de compatibilidad para consumidores existentes.
    - **Verificación:** la operación tipada devuelve `SourceReference[]`, locale, estado y metadatos sin obligar a parsear Markdown; el adaptador legacy sigue compilando.
    - **Requisitos:** 7.1, 7.2, 7.3, 7.4, 7.5, 8.1, 8.6.
    - **Propiedades relacionadas:** Property 9, Property 16, Property 17.

  - [ ] 2.3 Implementar la coordinación de `HistoryService`
    - Crear `BlazorApp/Services/HistoryService.cs` y sus interfaces/dobles necesarios.
    - Implementar `SubmitAsync` con validación previa, creación o selección de sesión, mensaje de usuario correlacionado, llamada tipada a RAG y mensaje assistant válido o intento inválido.
    - Diferenciar `Completed`, `Invalid`, `Rejected`, `Cancelled`, `TimedOut` y `Unavailable`; conservar el texto original y evitar que fallos sean reutilizables.
    - Abrir scopes de correlación y no registrar el contenido completo de consulta o respuesta.
    - **Verificación:** entradas inválidas no crean sesión ni llaman a RAG; las válidas tienen sesión y `EntryId`; fallos y cancelaciones conservan la consulta y nunca se presentan como respuesta válida.
    - **Requisitos:** 1.1, 1.2, 1.3, 1.4, 7.1, 7.6, 8.3, 9.1, 9.2, 9.5, 9.6.
    - **Propiedades relacionadas:** Property 1, Property 3, Property 4, Property 5, Property 17.

  - [ ]* 2.4 Escribir prueba de propiedad de validación
    - Implementar en `BlazorAppTests` la prueba FsCheck de **Property 1: Validación bloquea entradas fuera de límites** con fakes de `RagService` y `ChatSessionService`.
    - Ejecutar al menos 100 casos y añadir el comentario `Feature: rag-phase-3-history-localization, Property 1: Validación bloquea entradas fuera de límites`.
    - **Verificación:** las entradas inválidas conservan el texto, no cambian la sesión y no llaman a RAG; las válidas conservan su texto original.
    - **Requisitos:** 1.4, 9.1.
    - **Propiedad:** Property 1.

  - [ ]* 2.5 Escribir prueba de propiedad de correlación
    - Implementar en `BlazorAppTests` la prueba FsCheck de **Property 3: Una consulta válida tiene una sesión y una entrada correlacionada**.
    - Ejecutar al menos 100 casos con y sin sesión seleccionada y añadir el comentario `Feature: rag-phase-3-history-localization, Property 3: Una consulta válida tiene una sesión y una entrada correlacionada`.
    - **Verificación:** existe una sesión activa antes del mensaje de usuario, se conserva la consulta original y el resultado puede asociarse mediante `EntryId`.
    - **Requisitos:** 1.1, 7.6.
    - **Propiedad:** Property 3.

  - [ ]* 2.6 Escribir prueba de propiedad de respuestas válidas
    - Implementar en `BlazorAppTests` la prueba FsCheck de **Property 4: Una respuesta válida conserva idioma, fecha y fuentes**.
    - Generar locales soportados y listas de `SourceReference`; añadir el comentario `Feature: rag-phase-3-history-localization, Property 4: Una respuesta válida conserva idioma, fecha y fuentes`.
    - **Verificación:** el historial conserva texto, locale exacto, timestamp UTC y todas las fuentes proporcionadas.
    - **Requisitos:** 1.2, 7.6, 8.1.
    - **Propiedad:** Property 4.

  - [ ]* 2.7 Escribir prueba de propiedad de fallos no reutilizables
    - Implementar en `BlazorAppTests` la prueba FsCheck de **Property 5: Los fallos nunca se presentan como respuestas reutilizables** para respuesta parcial, fallo, cancelación y timeout.
    - Añadir el comentario `Feature: rag-phase-3-history-localization, Property 5: Los fallos nunca se presentan como respuestas reutilizables`.
    - **Verificación:** el intento conserva un motivo seguro, tiene estado no válido, `IsReusable = false` y nunca se presenta como respuesta completa.
    - **Requisitos:** 1.3, 4.6, 9.6.
    - **Propiedad:** Property 5.

  - [ ]* 2.8 Escribir prueba de propiedad de solicitud RAG
    - Implementar en `BlazorAppTests` la prueba FsCheck de **Property 16: La solicitud RAG conserva locale y consulta** con un fake que capture `RagQueryRequest`.
    - Añadir el comentario `Feature: rag-phase-3-history-localization, Property 16: La solicitud RAG conserva locale y consulta`.
    - **Verificación:** la solicitud contiene exactamente la consulta original y el código exacto del locale, sin usar la respuesta guardada.
    - **Requisitos:** 7.1, 7.6, 8.3.
    - **Propiedad:** Property 16.

  - [ ]* 2.9 Escribir prueba de propiedad de metadatos
    - Implementar en `BlazorAppTests` la prueba FsCheck de **Property 17: Los metadatos de modelo y contexto se preservan** con diccionarios generados de claves y valores seguros.
    - Añadir el comentario `Feature: rag-phase-3-history-localization, Property 17: Los metadatos de modelo y contexto se preservan`.
    - **Verificación:** todas las claves y valores proporcionados por RAG aparecen en el `HistoryEntry` aunque la UI no los muestre.
    - **Requisitos:** 8.6.
    - **Propiedad:** Property 17.

  - [ ] 2.10 Registrar contratos y servicios en DI
    - Modificar `BlazorApp/Program.cs` para registrar `HistoryOptions`, `HistoryScopeProvider`, `LocalizationService`, `HistoryService` y los adaptadores tipados de RAG con los ciclos de vida definidos por el diseño.
    - Configurar la validación de opciones al inicio y conservar los registros existentes de MongoDB, Ollama y Serilog.
    - **Verificación:** la aplicación puede resolver todos los servicios sin crear estado de idioma en singleton ni duplicar la ruta RAG activa.
    - **Requisitos:** 5.2, 5.3, 6.1, 7.1, 9.5.

  - [ ] 2.11 Checkpoint de servicios
    - Compilar `BlazorApp` y `BlazorAppTests` y ejecutar las pruebas unitarias disponibles de contratos y `HistoryService`.
    - **Verificación:** los servicios se resuelven por DI, los fakes permiten probar sin MongoDB/Ollama y no se ha cambiado la ingestión documental.

- [ ] 3. Integrar el historial en `Chat`
  - [ ] 3.1 Sustituir la llamada directa de `Chat.razor`
    - Modificar `BlazorApp/Components/Pages/Chat.razor` para depender de `HistoryService`, `LocalizationService` y el selector de idioma, manteniendo la ruta `/chat`, Enter, Shift+Enter y el límite visual de 2000 caracteres.
    - Mantener el texto del editor cuando la validación falla y evitar una segunda llamada al modelo durante una misma operación.
    - **Verificación:** enviar una consulta válida sigue mostrando estado de carga y resultado; una consulta inválida conserva el texto y no invoca RAG.
    - **Requisitos:** 1.1, 1.4, 7.1, 9.1.

  - [ ] 3.2 Crear componentes de historial y estados visuales
    - Crear `HistoryPanel`, `LanguageSelector` y los componentes de fuente/estado que el diseño identifica, en `BlazorApp/Components/`.
    - Actualizar `BlazorApp/Components/ChatMessage.razor` y su CSS para diferenciar consulta, respuesta válida, resultado guardado, error e intento no reutilizable.
    - **Verificación:** la UI presenta estados localizables de carga, vacío, error, respuesta guardada, fuente disponible y documento no disponible.
    - **Requisitos:** 1.5, 2.5, 3.6, 8.2, 8.4, 8.5.

  - [ ] 3.3 Cargar sesiones sin llamar a RAG
    - Conectar la selección de sesión a `LoadSessionAsync`, mostrar mensajes en orden ascendente por `Timestamp` y conservar el indicador de resultado guardado.
    - Tratar una sesión fuera del alcance o ausente como no disponible con cero mensajes y mensaje localizado.
    - **Verificación:** seleccionar una sesión genera cero solicitudes a RAG, muestra cada mensaje una vez y no presenta contenido de otra cookie de alcance.
    - **Requisitos:** 1.5, 2.6, 2.7, 3.3, 8.4.
    - **Propiedades relacionadas:** Property 6.

  - [ ]* 3.4 Escribir la prueba de propiedad de orden cronológico
    - Implementar FsCheck para **Property 6: La presentación cronológica es estable**, generando mensajes con timestamps repetidos y estados distintos.
    - **Verificación:** la proyección está ordenada ascendentemente, conserva cada mensaje una vez y asigna una categoría visual según rol/estado.
    - **Requisitos:** 1.5.
    - **Propiedad:** Property 6.

- [ ] 4. Implementar búsqueda, paginación, fuentes y reutilización
  - [ ] 4.1 Implementar listado y búsqueda literal
    - Añadir a `HistoryService`/repositorio la lista de sesiones activas o archivadas, búsqueda por título, consulta y respuesta sin distinguir mayúsculas, y paginación configurable.
    - Escapar el texto buscado como literal y no como expresión regular; el texto vacío o solo espacios debe devolver las sesiones recientes.
    - **Verificación:** la lista ordena por `LastActive` descendente, respeta el tamaño configurado y las búsquedas encuentran solo coincidencias en los tres campos admitidos.
    - **Requisitos:** 2.4, 3.1, 3.2.
    - **Propiedades relacionadas:** Property 7, Property 8.

  - [ ]* 4.2 Escribir prueba de propiedad de orden y paginación
    - Implementar FsCheck para **Property 7: La lista respeta orden y límites de paginación**.
    - Generar sesiones con `LastActive` repetidos y tamaños de página dentro y fuera de 1..50; añadir el comentario `Feature: rag-phase-3-history-localization, Property 7: La lista respeta orden y límites de paginación`.
    - **Verificación:** el resultado queda ordenado por `LastActive` descendente, nunca supera 50 y normaliza tamaños inválidos antes de consultar.
    - **Requisitos:** 2.4.
    - **Propiedad:** Property 7.

  - [ ]* 4.3 Escribir prueba de propiedad de búsqueda literal
    - Implementar FsCheck para **Property 8: La búsqueda encuentra coincidencias en todos los campos admitidos**.
    - Incluir mayúsculas, espacios, texto en título/consulta/respuesta y caracteres de regex; añadir el comentario `Feature: rag-phase-3-history-localization, Property 8: La búsqueda encuentra coincidencias en todos los campos admitidos`.
    - **Verificación:** solo se devuelven sesiones con coincidencia literal sin distinguir mayúsculas y ningún patrón se interpreta como expresión regular.
    - **Requisitos:** 3.1, 3.2.
    - **Propiedad:** Property 8.

  - [ ] 4.4 Implementar consulta de fuentes históricas
    - Crear `GetSourceAsync` para resolver disponibilidad actual del documento sin eliminar `StoredDocumentName`, `Location`, `Quote` ni metadatos históricos.
    - Actualizar el componente de fuente para mostrar nombre y ubicación almacenados y deshabilitar solo la apertura cuando el documento ya no exista.
    - **Verificación:** una fuente ausente en el índice sigue mostrando la cita guardada y un aviso localizado de indisponibilidad.
    - **Requisitos:** 3.5, 7.4, 8.1, 8.5.
    - **Propiedades relacionadas:** Property 9.

  - [ ]* 4.5 Escribir prueba de propiedad de fuentes
    - Implementar FsCheck para **Property 9: Las fuentes conservan su representación documental** con campos opcionales presentes y ausentes.
    - Añadir el comentario `Feature: rag-phase-3-history-localization, Property 9: Las fuentes conservan su representación documental`.
    - **Verificación:** no se modifican identificadores, nombres originales, citas ni metadatos; solo se omiten campos que realmente no fueron proporcionados.
    - **Requisitos:** 3.5, 7.4, 8.1.
    - **Propiedad:** Property 9.

  - [ ] 4.6 Implementar reutilización explícita
    - Añadir `ReuseQueryAsync` para devolver únicamente el texto original al editor, sin invocar RAG ni crear una entrada.
    - Conectar el botón de reutilización en `Chat.razor`/`HistoryPanel`; el nuevo envío debe pasar por `SubmitAsync` y crear un `EntryId` diferente.
    - **Verificación:** seleccionar una consulta llena el editor sin ejecutar RAG; solo la confirmación ejecuta una recuperación nueva contra el índice actual.
    - **Requisitos:** 3.3, 3.4, 8.3.
    - **Propiedades relacionadas:** Property 16.

- [ ] 5. Implementar archivado, restauración, exportación y eliminación
  - [ ] 5.1 Implementar estados de sesión y archivado/restauración
    - Añadir `ArchiveAsync` y `RestoreAsync` en `HistoryService`, con filtros de alcance y compatibilidad con el estado legacy de `CustomProperties["status"]`.
    - Excluir archivadas de la lista activa sin modificar mensajes, fuentes ni fechas.
    - **Verificación:** archivar cambia solo la disponibilidad y restaurar devuelve la sesión activa con el mismo contenido y timestamps.
    - **Requisitos:** 4.1, 4.2.
    - **Propiedades relacionadas:** Property 10, Property 11.

  - [ ]* 5.2 Escribir prueba de propiedad de archivado
    - Implementar FsCheck para **Property 10: Archivar excluye sin perder contenido** y añadir el comentario `Feature: rag-phase-3-history-localization, Property 10: Archivar excluye sin perder contenido`.
    - **Verificación:** archivar excluye la sesión activa, pero conserva sin cambios mensajes, referencias, timestamps y contenido.
    - **Requisitos:** 4.1.
    - **Propiedad:** Property 10.

  - [ ]* 5.3 Escribir prueba de propiedad de restauración
    - Implementar FsCheck para **Property 11: Restaurar es reversible para el contenido** y añadir el comentario `Feature: rag-phase-3-history-localization, Property 11: Restaurar es reversible para el contenido`.
    - **Verificación:** restaurar devuelve la sesión a activas y conserva exactamente título, mensajes, referencias y fechas.
    - **Requisitos:** 4.2.
    - **Propiedad:** Property 11.

  - [ ] 5.4 Implementar exportación JSON y Markdown
    - Implementar `ExportAsync` usando lectura completa de la sesión y formatos JSON/Markdown.
    - Incluir consultas, respuestas, fechas UTC, locale, estados, fuentes, metadatos disponibles y texto original; excluir secretos de infraestructura.
    - Conectar descargas en la UI y mostrar un error localizado sin modificar la sesión si falla la exportación.
    - **Verificación:** ambos archivos contienen todos los elementos de la sesión y el JSON se deserializa; una excepción no cambia la sesión.
    - **Requisitos:** 4.4, 4.6, 7.6, 8.1, 8.6.
    - **Propiedades relacionadas:** Property 12.

  - [ ]* 5.5 Escribir prueba de propiedad de exportación
    - Implementar FsCheck para **Property 12: La exportación preserva el historial** y añadir el comentario `Feature: rag-phase-3-history-localization, Property 12: La exportación preserva el historial`.
    - Añadir ejemplos para JSON válido, Markdown, fuentes opcionales y ausencia de secretos.
    - **Verificación:** cada caso exportado contiene consulta, respuesta, fechas, locale, estado y fuentes disponibles sin traducir la consulta.
    - **Requisitos:** 4.4, 7.6, 8.1.
    - **Propiedad:** Property 12.

  - [ ] 5.6 Implementar eliminación transaccional
    - Implementar `DeleteAsync` con preflight de capacidad transaccional, borrado de sesión y mensajes en una única transacción MongoDB y rollback ante fallo.
    - Rechazar antes de mutar cuando la instancia no soporte transacciones; aplicar alcance y no revelar contenido en rechazos.
    - Conectar confirmación y error localizado en la UI.
    - **Verificación:** una eliminación completada no permite recuperar sesión ni mensajes; un fallo conserva ambos; una instancia sin transacciones no ejecuta borrados parciales.
    - **Requisitos:** 4.3, 4.6, 9.4.

  - [ ] 5.7 Checkpoint de administración de sesiones
    - Compilar y ejecutar pruebas de archivado, restauración, exportación y eliminación con repositorios aislados.
    - **Verificación:** los errores administrativos no alteran datos y todos los casos pasan por el filtro de privacidad.

- [ ] 6. Implementar localización `es`/`en`/`pt`
  - [ ] 6.1 Crear recursos completos de traducción
    - Crear recursos compilados para `es`, `en` y `pt` con claves para encabezados, botones, placeholders, instrucciones, carga, vacío, validación y errores de historial/RAG.
    - Mantener el recurso español como fallback no vacío y evitar claves de UI con texto incrustado en componentes.
    - **Verificación:** cada locale contiene una entrada para cada clave pública requerida por `Chat`, historial, fuentes y estados de error.
    - **Requisitos:** 5.1, 6.1.

  - [ ] 6.2 Implementar `LocalizationService` scoped
    - Crear el servicio con normalización estricta de `es`, `en` y `pt`, preferencia local, notificación de cambios y `Get(key, args)` con fallback español.
    - Registrar la clave ausente una vez por resolución y resolver argumentos dinámicos según la cultura activa sin dejar marcadores.
    - **Verificación:** preferencia ausente o inválida se sustituye por `es`; una clave faltante devuelve el español, registra la clave y formatea sus valores.
    - **Requisitos:** 5.3, 5.4, 5.5, 6.2, 6.3, 6.4, 6.5.
    - **Propiedades relacionadas:** Property 2, Property 14, Property 15.

  - [ ]* 6.3 Escribir prueba de propiedad de normalización de locale
    - Implementar FsCheck para **Property 2: Locale se normaliza al conjunto soportado** y añadir el comentario `Feature: rag-phase-3-history-localization, Property 2: Locale se normaliza al conjunto soportado`.
    - **Verificación:** una preferencia ausente, nula o inválida produce `es`; `es`, `en` y `pt` se conservan exactamente.
    - **Requisitos:** 5.4, 5.5.
    - **Propiedad:** Property 2.

  - [ ]* 6.4 Escribir prueba de propiedad de fallback
    - Implementar FsCheck para **Property 14: El fallback de localización es español y deja trazabilidad** y añadir el comentario `Feature: rag-phase-3-history-localization, Property 14: El fallback de localización es español y deja trazabilidad`.
    - **Verificación:** una clave ausente en `en`/`pt` devuelve el valor español no vacío, registra la clave y no devuelve una plantilla sin resolver.
    - **Requisitos:** 6.3, 6.5.
    - **Propiedad:** Property 14.

  - [ ]* 6.5 Escribir prueba de propiedad de formato dinámico
    - Implementar FsCheck para **Property 15: Los valores dinámicos quedan formateados** y añadir el comentario `Feature: rag-phase-3-history-localization, Property 15: Los valores dinámicos quedan formateados`.
    - Incluir fechas, números y texto dinámico en los tres locales.
    - **Verificación:** el resultado no contiene marcadores pendientes y aplica la cultura del locale seleccionado.
    - **Requisitos:** 6.4.
    - **Propiedad:** Property 15.

  - [ ] 6.6 Integrar selector y conservación entre rutas
    - Modificar `Chat.razor`, `MainLayout` y las páginas `/documents` y `/statistics` para consumir el locale scoped y volver a renderizar sin reinicio.
    - Crear `LanguageSelector` con exactamente `es`, `en` y `pt`; persistir la preferencia local y copiarla a `SessionMetadata.Language` cuando exista sesión activa.
    - **Verificación:** cambiar idioma en cualquiera de las tres rutas actualiza textos visibles y se conserva al navegar entre ellas.
    - **Requisitos:** 5.1, 5.2, 5.3, 5.6, 6.2.

  - [ ] 6.7 Checkpoint de localización
    - Compilar la aplicación y ejecutar pruebas de recursos, fallback y componentes localizados.
    - **Verificación:** los tres recursos cargan, el selector no muestra valores adicionales y los estados de error no exponen detalles de conexión.

- [ ] 7. Integrar el idioma de respuesta RAG
  - [ ] 7.1 Aplicar locale al prompt y al contrato de respuesta
    - Conectar `LocalizationService`/estado de sesión con `RagQueryRequest.ResponseLocale` sin traducir la consulta original.
    - Ajustar el prompt de `RagService` para que el texto explicativo use el locale solicitado y conserve identificadores técnicos, nombres de documentos y citas.
    - **Verificación:** un fake de chat completion recibe el locale exacto; una consulta en otro idioma no cambia la entrada almacenada.
    - **Requisitos:** 7.1, 7.2, 7.3, 7.6.
    - **Propiedades relacionadas:** Property 16.

  - [ ]* 7.2 Escribir pruebas de contrato para idioma y fuentes
    - Añadir pruebas unitarias de `RagService` para los tres locales, preservación de identificadores/citas, fuentes tipadas y estados `Completed`, `NoContext`, `UnsupportedLocale` y `Failed`.
    - **Verificación:** el test no depende de texto generativo exacto; verifica locale solicitado, preservación documental y estado correcto.
    - **Requisitos:** 7.2, 7.3, 7.4, 7.5, 8.1.

  - [ ] 7.3 Manejar locale no disponible y guardar idioma
    - Implementar el mensaje de limitación localizado cuando Ollama/modelo no pueda producir el locale solicitado.
    - Guardar `ResponseLocale` en la entrada y conservar la consulta original exactamente; no marcar la limitación como respuesta generada o traducida.
    - **Verificación:** una respuesta `UnsupportedLocale` no se presenta como válida/reutilizable y la consulta permanece sin cambios.
    - **Requisitos:** 7.5, 7.6, 1.3.

  - [ ] 7.4 Checkpoint de RAG localizado
    - Ejecutar pruebas unitarias con un fake de `IChatCompletionService` y compilar el flujo `/chat` completo.
    - **Verificación:** los tres locales recorren el mismo pipeline tipado y las fuentes conservan título/cita originales.

- [ ] 8. Aplicar privacidad, retención y observabilidad
  - [ ] 8.1 Centralizar autorización por alcance
    - Revisar todas las operaciones de `HistoryService`, repositorios, exportación, archivado, restauración y eliminación para exigir el alcance emitido por `HistoryScopeProvider`.
    - Rechazar carga, exportación, archivado o eliminación fuera del alcance con cero mensajes y sin registrar contenido de la sesión.
    - **Verificación:** variar `UserId` no concede acceso; cada operación fuera de alcance devuelve rechazo/no disponible y deja datos intactos.
    - **Requisitos:** 2.6, 2.7, 4.6, 9.3, 9.4.

  - [ ] 8.2 Implementar retención elegible y limpieza segura
    - Añadir evaluación de sesiones inactivas según `RetentionDays`, excluyendo sesiones archivadas y recientes.
    - Marcar `eligible-for-cleanup` sin eliminar; ejecutar después la eliminación transaccional ya implementada.
    - **Verificación:** solo sesiones antiguas y activas quedan elegibles; marcar elegibilidad nunca elimina por sí solo.
    - **Requisitos:** 4.5, 9.6.
    - **Propiedades relacionadas:** Property 13.

  - [ ]* 8.3 Escribir prueba de propiedad de retención
    - Implementar FsCheck para **Property 13: La retención solo marca sesiones elegibles** con fechas, estados y política habilitada/deshabilitada.
    - **Verificación:** sesiones recientes, archivadas o con política deshabilitada no se marcan ni eliminan.
    - **Requisitos:** 4.5.
    - **Propiedad:** Property 13.

  - [ ] 8.4 Implementar logging, cancelación y timeout
    - Integrar `Serilog`, `CorrelationId` y los componentes existentes de logging para registrar operación, sesión, duración, resultado y correlación sin contenido completo.
    - Propagar cancelación y aplicar timeout entre 1 y 120 segundos; devolver `Cancelled`/`TimedOut` sin informar éxito ni perder datos previamente guardados.
    - **Verificación:** logs de éxito, rechazo, fallo, cancelación y timeout contienen los campos definidos y no contienen consulta/respuesta completa, secretos ni detalles de conexión en UI.
    - **Requisitos:** 4.6, 6.5, 9.5, 9.6.

  - [ ] 8.5 Checkpoint de privacidad y operación
    - Ejecutar pruebas con scopes distintos, cancelación, timeout, errores de persistencia y retención.
    - **Verificación:** ninguna operación fuera de alcance muta datos; los errores técnicos quedan solo en logs estructurados y los mensajes de usuario son localizados.

- [ ] 9. Completar pruebas unitarias y de propiedades
  - [ ] 9.1 Configurar la infraestructura de pruebas
    - Modificar `BlazorAppTests/BlazorAppTests.csproj` para añadir `FsCheck`/`FsCheck.Xunit` y utilidades de prueba sin acoplar las pruebas unitarias a MongoDB u Ollama.
    - Crear fakes/builders para sesiones, mensajes, fuentes, locales, respuestas RAG y scopes.
    - **Verificación:** la suite ejecuta al menos 100 casos por propiedad y puede correr sin dependencias externas.
    - **Requisitos:** 10.6 y estrategia de pruebas del diseño.

  - [ ]* 9.2 Añadir pruebas unitarias de ejemplos y errores
    - Cubrir límites exactos 1, 2000 y 2001; Unicode y espacios; estados completed/invalid/cancelled/timed-out/failed; exportación JSON/Markdown; fallback; búsqueda literal; cancelación y timeout.
    - **Verificación:** cada caso de borde tiene una aserción de resultado, persistencia y llamadas a dependencias; los errores no informan éxito.
    - **Requisitos:** 1.3, 1.4, 3.1, 4.4, 4.6, 6.3, 9.1, 9.6.

- [ ] 10. Añadir integración MongoDB y pruebas de componentes
  - [ ] 10.1 Preparar fixtures de integración aislados
    - Crear `BlazorAppTests/IntegrationTests/` o la ruta equivalente existente con fixtures para `chat_sessions` y `chat_messages`, scope aleatorio, índices y limpieza por prueba.
    - Añadir preflight que identifique MongoDB no disponible o sin transacciones como `NotRunnableExternalDependency`.
    - **Verificación:** las pruebas no usan `backups/`, no comparten sesiones entre casos y distinguen dependencia externa no ejecutable de fallo de producto.
    - **Requisitos:** 2.1, 4.3, 9.3, 10.6.

  - [ ]* 10.2 Probar persistencia, búsqueda y privacidad
    - Verificar persistencia separada, orden/paginación, búsqueda en título/consulta/respuesta, filtro obligatorio de scope, límite de mensajes y archivado/restauración.
    - **Verificación:** los resultados coinciden con el contrato de `HistoryService` y ningún scope puede cargar o mutar otro.
    - **Requisitos:** 2.1, 2.4, 2.6, 2.7, 3.1, 4.1, 4.2, 9.2, 9.3.

  - [ ]* 10.3 Probar transacción, exportación y retención
    - Verificar eliminación atómica y rollback, exportaciones con fuentes/metadatos, marcado de retención y exclusión de archivadas.
    - **Verificación:** una falla inducida no deja huérfanos; los archivos contienen los datos originales y la retención no elimina al marcar.
    - **Requisitos:** 4.3, 4.4, 4.5, 4.6, 8.1, 8.6.

  - [ ]* 10.4 Probar componentes Blazor
    - Añadir pruebas de componente/navegador para selector `es`/`en`/`pt`, estados de carga/vacío/error, mensajes por rol, fuentes, sesión guardada, sesión no disponible y documento ausente.
    - Verificar carga sin RAG y reutilización que llena el editor sin enviar hasta la confirmación.
    - **Verificación:** los textos visibles corresponden al locale activo y las acciones generan exactamente las llamadas esperadas.
    - **Requisitos:** 1.5, 2.5, 3.3, 3.4, 3.6, 5.1, 5.2, 5.6, 6.2, 8.2, 8.4, 8.5.

  - [ ] 10.5 Checkpoint de integración
    - Ejecutar la suite unitaria, de propiedades, integración y componentes con reportes separados para dependencias externas no disponibles.
    - **Verificación:** no quedan fallos de compilación; los casos no ejecutables están clasificados y no ocultan fallos de comportamiento.

- [ ] 11. Validar el flujo E2E de la fase 3
  - [ ] 11.1 Crear fixture E2E de `/chat`
    - Configurar la ruta `/chat` con dobles controlables de RAG y almacenamiento, conservando una opción de ejecución contra MongoDB/Ollama reales.
    - **Verificación:** el escenario puede crear una sesión, enviar una consulta y recuperar la respuesta sin depender del texto exacto de un modelo generativo.
    - **Requisitos:** 10.1, 10.6.

  - [ ]* 11.2 Cubrir consulta, historial y reutilización
    - Verificar que una consulta y respuesta se recuperan, que seleccionar `en`/`pt` localiza encabezados/carga/vacío/error, y que reutilizar genera una llamada RAG nueva y una entrada distinta.
    - **Verificación:** el historial guardado no se usa como respuesta vigente y la segunda entrada conserva su propia correlación y locale.
    - **Requisitos:** 10.1, 10.2, 10.3, 8.3.

  - [ ]* 11.3 Cubrir administración y exportación E2E
    - Verificar eliminación completa, exportación JSON/Markdown con consulta, respuesta, idioma y todas las fuentes, y ausencia posterior de sesión/mensajes.
    - **Verificación:** no existe recuperación posterior dentro del scope y ambos formatos contienen los campos exigidos.
    - **Requisitos:** 10.4, 10.5.

  - [ ]* 11.4 Cubrir dependencias externas y cierre E2E
    - Ejecutar el preflight de MongoDB/Ollama; si no están disponibles, marcar los escenarios externos como no ejecutables y mantener activos los escenarios con fakes.
    - **Verificación:** el informe distingue `NotRunnableExternalDependency` de fallos funcionales y cubre los cinco flujos E2E definidos en el requisito 10.
    - **Requisitos:** 10.1, 10.2, 10.3, 10.4, 10.5, 10.6.

  - [ ] 11.5 Checkpoint final
    - Ejecutar compilación y la suite reproducible completa con los modos unitario, integración y E2E definidos.
    - Verificar que cada requisito implementable tiene una prueba o criterio de verificación trazable y que no se implementaron cambios en ingesta documental fuera del alcance.
    - **Verificación:** el proyecto compila, las pruebas aplicables pasan y las dependencias externas no disponibles quedan informadas según el requisito 10.6.

## Notes

- Las tareas marcadas con `*` son pruebas opcionales y pueden omitirse para un MVP, pero las tareas de implementación que las preceden deben conservar contratos verificables.
- Las pruebas de propiedad usan `FsCheck`/`FsCheck.Xunit` y corresponden a las 18 propiedades de `design.md`; cada propiedad aparece en una tarea separada o en una tarea que agrupa únicamente las propiedades indicadas.
- MongoDB y Ollama se prueban con dobles en la mayoría de la suite. Las pruebas que requieran dependencias externas deben clasificarse como no ejecutables por dependencia externa cuando corresponda.
- No se incluyen tareas de despliegue, documentación de usuario, métricas de rendimiento manuales ni aceptación manual, porque el flujo exige únicamente trabajo de código y pruebas automatizadas.

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1"] },
    { "id": 1, "tasks": ["1.2"] },
    { "id": 2, "tasks": ["1.3"] },
    { "id": 3, "tasks": ["1.4"] },
    { "id": 4, "tasks": ["1.5"] },
    { "id": 5, "tasks": ["2.1"] },
    { "id": 6, "tasks": ["2.2"] },
    { "id": 7, "tasks": ["2.3"] },
    { "id": 8, "tasks": ["2.4"] },
    { "id": 9, "tasks": ["2.5"] },
    { "id": 10, "tasks": ["2.6"] },
    { "id": 11, "tasks": ["2.7"] },
    { "id": 12, "tasks": ["2.8"] },
    { "id": 13, "tasks": ["2.9"] },
    { "id": 14, "tasks": ["2.10"] },
    { "id": 15, "tasks": ["2.11"] },
    { "id": 16, "tasks": ["3.1", "4.1", "6.1", "8.1"] },
    { "id": 17, "tasks": ["3.2", "4.2", "6.2", "8.2"] },
    { "id": 18, "tasks": ["5.1", "7.1"] },
    { "id": 19, "tasks": ["3.3", "4.3", "6.3", "8.3"] },
    { "id": 20, "tasks": ["3.4", "4.4", "5.2", "6.4", "7.2", "8.4"] },
    { "id": 21, "tasks": ["4.5", "5.3", "6.5", "7.3"] },
    { "id": 22, "tasks": ["4.6", "5.4", "6.6"] },
    { "id": 23, "tasks": ["5.5", "6.7", "7.4", "8.5"] },
    { "id": 24, "tasks": ["5.6"] },
    { "id": 25, "tasks": ["5.7"] },
    { "id": 26, "tasks": ["9.1"] },
    { "id": 27, "tasks": ["9.2"] },
    { "id": 28, "tasks": ["10.1"] },
    { "id": 29, "tasks": ["10.2", "10.3", "10.4"] },
    { "id": 30, "tasks": ["10.5"] },
    { "id": 31, "tasks": ["11.1"] },
    { "id": 32, "tasks": ["11.2", "11.3", "11.4"] },
    { "id": 33, "tasks": ["11.5"] }
  ]
}
```
