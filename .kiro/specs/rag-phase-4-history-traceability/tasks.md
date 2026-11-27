# Implementation Plan: historial y trazabilidad

## Overview

Implementar el historial de consultas por usuario autenticado en Blazor, MongoDB y Ollama mediante `HistoryService`. Las tareas 1 a 3 ya completaron los contratos, modelos, aislamiento de persistencia y contrato tipado del RAG. Las tareas restantes completan la coordinación de consultas, la integración de `/chat`, la administración, las pruebas automatizadas y la validación reproducible con Docker.

Cada tarea debe ejecutarse como un prompt incremental para un agente de generación de código: debe escribir, modificar o probar código, reutilizar los componentes integrados por las tareas anteriores y dejar el flujo conectado. No se incluyen despliegues, aceptación manual, documentación de usuario ni tareas operativas sin cambios de código.

## Tasks

- [x] 1. Consolidar modelos, opciones y contratos de historial
  - [x] 1.1 Completar las opciones tipadas para límites de consulta, mensajes, paginación y timeout, con valores configurables y validación de rango.
    - Mantener el límite de consulta entre 1 y 2000 caracteres y la página entre 1 y 50.
    - _Requisitos: R1.1, R1.5, R2.2_
  - [x] 1.2 Completar `ChatSession`, `ChatMessage`, `SourceReference` y `HistoryEntryMetadata` con propietario, estados, `EntryId`, metadatos del modelo y fuentes estructuradas.
    - Conservar fechas UTC, tratar `OwnerEmailSnapshot` solo como dato descriptivo y usar `IsReusable` para distinguir intentos válidos.
    - _Requisitos: R1.2, R1.3, R1.4, R3.5, R4.3, R5_
  - [x] 1.3 Completar `RagQueryRequest`, `RagResponse`, estados de resultado, códigos de fallo y contratos de exportación JSON/Markdown.
    - _Requisitos: R3.3, R4.3, R5_

- [x] 2. Implementar persistencia aislada por usuario
  - [x] 2.1 Completar `CurrentUserContext`/`ApplicationActor` para obtener `OwnerUserId` del servidor y rechazar operaciones sin usuario autenticado.
    - No aceptar el propietario desde componentes Blazor, parámetros HTTP ni datos enviados por el cliente.
    - _Requisitos: R2.1, R4.4, R5_
  - [x] 2.2 Completar `HistoryRepository` y los servicios de sesiones/mensajes con `OwnerUserId` como filtro obligatorio en cada lectura y mutación.
    - Implementar recuperación ordenada por `LastActiveUtc`, paginación, mensajes cronológicos y estado no disponible para sesión inexistente o ajena.
    - _Requisitos: R2.1, R2.2, R2.3, R2.5, R4.1, R4.2_
  - [x] 2.3 Crear o actualizar índices de `chat_sessions` y `chat_messages`, más la estrategia de búsqueda literal sin regex.
    - Usar los índices definidos en el diseño y una proyección/normalización compatible con búsqueda de título, consulta y respuesta sin distinguir mayúsculas.
    - _Requisitos: R2.2, R3.1, R3.2_
  - [x] 2.4 Implementar eliminación transaccional cuando MongoDB tenga replica set y eliminación lógica con `Status = deleted` cuando no lo tenga.
    - Evitar dos borrados independientes que puedan dejar sesión y mensajes incoherentes.
    - _Requisitos: R4.4, R4.5_
  - [x] 2.5 Escribir pruebas de unidad para filtros de propietario, paginación, orden cronológico y estados de sesión.
    - * Probar que dos usuarios no pueden leer ni modificar sesiones o mensajes del otro.
    - _Requisitos: R2.1, R2.2, R2.3, R2.5, R4.1, R4.2_

- [x] 3. Integrar el contrato tipado del RAG
  - [x] 3.1 Adaptar `RagService` para recibir `RagQueryRequest` y devolver `RagResponse` sin extraer fuentes desde Markdown.
    - Mapear texto de respuesta, fuentes `SourceReference`, metadatos del modelo y `FailureCode` de forma explícita.
    - _Requisitos: R1.3, R1.4, R3.5, R5_
  - [x] 3.2 Mantener el adaptador temporal para consumidores legacy y evitar nuevas dependencias de la respuesta textual no tipada.
    - _Requisitos: R5_
  - [x] 3.3 Escribir pruebas unitarias del contrato para respuestas válidas, fallos, cancelación, timeout y fuentes estructuradas.
    - * Verificar que la cita persistida no depende del formato Markdown del modelo.
    - _Requisitos: R1.3, R1.4, R3.5, R5_

- [~] 4. Completar `HistoryService` y el ciclo de consulta
  - [ ] 4.1 Implementar la validación previa de consulta y los límites configurados.
    - Rechazar texto nulo, vacío o fuera del rango de 1 a 2000 caracteres antes de crear una sesión.
    - Devolver un resultado de validación sin invocar `RagService` ni persistir mensajes o sesiones.
    - _Requisitos: R1.1, R1.5_
  - [ ] 4.2 Implementar la selección o creación de sesión y persistir el mensaje de usuario antes de llamar al RAG.
    - Conservar exactamente la consulta original, generar un `EntryId` correlacionable y aplicar siempre el usuario de `CurrentUserContext`.
    - _Requisitos: R1.1, R1.2, R2.1_
  - [ ] 4.3 Persistir respuestas válidas y resultados fallidos como estados explícitos.
    - Guardar texto, UTC, modelo, estado, idioma y fuentes estructuradas para respuestas válidas.
    - Guardar fallos, cancelaciones y timeouts como intentos inválidos no reutilizables, asociados al mismo `EntryId`.
    - No escribir consultas ni respuestas completas en logs; usar identificadores y diagnóstico mínimo.
    - _Requisitos: R1.3, R1.4, R3.5_
  - [ ] 4.4 Propagar `CancellationToken`, aplicar el timeout configurable y cerrar el ciclo sin entradas huérfanas.
    - Traducir cancelación y timeout al estado persistible correspondiente y mantener la correlación entre mensaje de usuario, resultado y sesión.
    - _Requisitos: R1.4, R4.5_
  - [ ]* 4.5 Escribir pruebas unitarias para validación, correlación, persistencia de estados y cancelación/timeout.
    - Cubrir que la validación no crea datos ni invoca RAG, que una respuesta válida es reutilizable y que un fallo no lo es.
    - _Requisitos: R1.1, R1.2, R1.3, R1.4, R1.5_
  - [ ] 4.6 Integrar repositorio, contexto de usuario y RAG tipado en una única operación pública para ejecutar una consulta histórica.
    - El método público debe dejar conectado el flujo completo: validar, seleccionar sesión, persistir consulta, ejecutar RAG y persistir el resultado.
    - _Requisitos: R1.1, R1.2, R1.3, R1.4, R2.1, R5_

- [~] 5. Conectar `/chat` con el historial
  - [ ] 5.1 Sustituir la llamada directa a `RagService` por `HistoryService` y mapear en la UI los estados de usuario, assistant e intento inválido.
    - Mostrar mensajes en orden cronológico, mantener la sesión activa y no aceptar sesiones ajenas.
    - _Requisitos: R1.1, R1.3, R1.4, R2.1, R2.3_
  - [ ] 5.2 Implementar carga de una sesión sin invocar RAG.
    - Recuperar mensajes históricos mediante `HistoryService`, representar una sesión inexistente o ajena como no disponible y conservar el orden cronológico.
    - _Requisitos: R2.3, R2.4, R2.5_
  - [ ] 5.3 Implementar reutilización literal de una consulta rellenando solo el editor.
    - No llamar a Ollama ni reutilizar respuestas fallidas durante la acción de reutilizar.
    - Exigir un nuevo envío para ejecutar RAG y generar un `EntryId` distinto.
    - _Requisitos: R1.4, R3.3, R3.4_
  - [ ] 5.4 Renderizar fuentes estructuradas con nombre, página, sección, cita y relevancia cuando existan.
    - Conservar la cita histórica aunque el documento actual ya no esté disponible.
    - _Requisitos: R3.5_
  - [ ]* 5.5 Escribir pruebas de componente/integración para carga, reutilización, nuevo envío y representación de estados/fuentes.
    - Verificar expresamente que cargar y reutilizar no invocan RAG y que un nuevo envío crea otra entrada.
    - _Requisitos: R2.3, R2.4, R3.3, R3.4, R3.5_

- [~] 6. Implementar administración, búsqueda y exportación
  - [ ] 6.1 Implementar búsqueda paginada sobre título, consulta y respuesta.
    - Aplicar `OwnerUserId`, comparación insensible a mayúsculas y tratamiento literal del texto sin regex.
    - Respetar los límites de página y ordenar resultados por `LastActiveUtc` descendente.
    - _Requisitos: R2.1, R2.2, R3.1, R3.2_
  - [ ] 6.2 Implementar archivar y restaurar sin modificar mensajes ni contenido.
    - Archivar debe excluir la sesión de la lista activa y restaurar debe hacerla visible de nuevo, siempre dentro del propietario actual.
    - _Requisitos: R4.1, R4.2_
  - [ ] 6.3 Implementar exportación JSON y Markdown mediante los contratos existentes.
    - Incluir consulta, respuesta, idioma, fechas, estados y fuentes estructuradas en orden cronológico.
    - _Requisitos: R4.3_
  - [ ] 6.4 Implementar eliminación con la estrategia transaccional o lógica del repositorio.
    - Traducir errores administrativos a un resultado sin éxito falso y mantener sesión/mensajes coherentes.
    - _Requisitos: R4.4, R4.5_
  - [ ]* 6.5 Escribir pruebas unitarias para búsqueda literal, estados de archivo, exportación y errores sin borrado parcial.
    - Cubrir propietarios distintos, sesiones archivadas/restauradas y el comportamiento con y sin replica set.
    - _Requisitos: R3.1, R3.2, R4.1, R4.2, R4.3, R4.4, R4.5_
  - [ ] 6.6 Conectar los componentes administrativos con los servicios de historial y los endpoints/componentes existentes.
    - Reutilizar la autorización y el contexto de usuario existentes; no crear una ruta alternativa que omita `OwnerUserId`.
    - _Requisitos: R2.1, R3.1, R4.1, R4.2, R4.3, R4.4_

- [~] 7. Añadir pruebas de integración y persistencia
  - [ ] 7.1 Crear pruebas de integración con MongoDB para documentos, índices, aislamiento por propietario, orden, paginación, estados y eliminación.
    - Verificar tanto el camino transaccional con replica set como el estado lógico sin replica set cuando el entorno lo permita.
    - _Requisitos: R2.1, R2.2, R2.3, R2.5, R4.1, R4.2, R4.4, R4.5_
  - [ ] 7.2 Crear una prueba de integración con Ollama para una consulta conocida.
    - Verificar el contrato tipado, fuentes y metadatos, y que el contenido estructurado se persiste sin analizar Markdown.
    - _Requisitos: R1.1, R1.2, R1.3, R3.5, R5_
  - [ ] 7.3 Crear pruebas automatizadas de flujo para consulta válida e inválida, fallo/cancelación, carga, reutilización, búsqueda, exportación, archivo y eliminación.
    - Verificar que cargar o reutilizar no invoca el modelo hasta un nuevo envío y que el nuevo envío genera otra entrada.
    - _Requisitos: R1.1, R1.4, R1.5, R2.3, R2.4, R3.1, R3.3, R3.4, R4.1, R4.3, R4.4_
  - [x] 7.4 Ejecutar la suite C# en modo no interactivo y corregir regresiones de compilación o integración antes del smoke test Docker.
    - _Requisitos: R1-R5_

- [~] 8. Conectar y validar el flujo completo en Docker
  - [ ] 8.1 Actualizar Docker Compose y scripts de inicialización para MongoDB, SQLite de Identity y Ollama persistentes.
    - Configurar replica set cuando esté disponible y mantener secretos fuera de código y logs.
    - _Requisitos: R4.4, criterio de finalización_
  - [ ] 8.2 Crear un smoke test automatizado que autentique dos usuarios locales y valide consulta, recarga, búsqueda, reutilización, exportación, archivo y eliminación.
    - Verificar aislamiento de sesiones y que cargar/reutilizar no llama a Ollama antes de un nuevo envío.
    - _Requisitos: R1.1, R1.2, R2.1, R2.3, R3.1, R3.3, R3.4, R4.1, R4.3, R4.4, criterio de finalización_
  - [x] 8.3 Añadir aserciones automatizadas de disponibilidad y persistencia de MongoDB, SQLite de Identity y Ollama.
    - Registrar únicamente identificadores, estados y diagnósticos mínimos, sin secretos ni consultas/respuestas completas.
    - _Requisitos: R1.3, R1.4, criterio de finalización_
  - [x] 8.4 Ejecutar compilación, pruebas y smoke test Docker en una pasada reproducible, corrigiendo cualquier fallo de cableado desde `/chat` hasta persistencia.
    - _Requisitos: R1-R5, criterio de finalización_

- [ ] 9. Checkpoint final de código
  - Ejecutar la compilación y todas las pruebas automatizadas en modo no interactivo; corregir fallos antes de marcar el plan completo.
  - _Requisitos: R1-R5, criterio de finalización_

## Notes

- Las tareas marcadas con `*` son pruebas opcionales y pueden omitirse para un MVP; las tareas de implementación no son opcionales.
- La implementación usa C# porque el repositorio contiene una aplicación Blazor y un proyecto de pruebas .NET.
- Las tareas se limitan a escribir, modificar o probar código. No incluyen despliegue, aceptación manual, recopilación de métricas, formación ni documentación de usuario.
- No se añaden pruebas basadas en propiedades porque el diseño no contiene una sección `Correctness Properties`; las invariantes se cubren con pruebas unitarias, de componente, integración y flujo.
- Las tareas 1, 2 y 3 conservan estado completado. Las tareas 4 a 8 conservan estado en progreso y se ordenan por dependencias de implementación. La tarea 9 permanece pendiente hasta que todo el código y las pruebas estén conectados.
- Cada prompt de implementación debe construir sobre los prompts anteriores y terminar integrando el componente creado, sin dejar código huérfano.

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["4.1"] },
    { "id": 1, "tasks": ["4.2"] },
    { "id": 2, "tasks": ["4.3"] },
    { "id": 3, "tasks": ["4.4", "4.5"] },
    { "id": 4, "tasks": ["4.6"] },
    { "id": 5, "tasks": ["5.1"] },
    { "id": 6, "tasks": ["5.2"] },
    { "id": 7, "tasks": ["5.3"] },
    { "id": 8, "tasks": ["5.4"] },
    { "id": 9, "tasks": ["5.5"] },
    { "id": 10, "tasks": ["6.1"] },
    { "id": 11, "tasks": ["6.2"] },
    { "id": 12, "tasks": ["6.3"] },
    { "id": 13, "tasks": ["6.4"] },
    { "id": 14, "tasks": ["6.5"] },
    { "id": 15, "tasks": ["6.6"] },
    { "id": 16, "tasks": ["7.1", "7.2", "7.3"] },
    { "id": 17, "tasks": ["7.4"] },
    { "id": 18, "tasks": ["8.1"] },
    { "id": 19, "tasks": ["8.2"] },
    { "id": 20, "tasks": ["8.3"] },
    { "id": 21, "tasks": ["8.4"] },
    { "id": 22, "tasks": ["9"] }
  ]
}
```
