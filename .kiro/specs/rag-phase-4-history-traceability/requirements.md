# Requisitos: historial y trazabilidad de consultas

## Alcance

Esta fase conecta `/chat` con historial persistente por usuario local autenticado y conserva fuentes estructuradas. Depende de la fase 3. No implementa localización completa, auditoría empresarial ni cambio de modelo.

## Decisiones

- Persistencia en MongoDB.
- Propietario: `OwnerUserId`, obtenido del `ClaimsPrincipal` y de `ApplicationActor`.
- El correo se muestra como dato descriptivo, no como clave de propiedad.
- Una sesión continua hasta que el usuario elija una nueva.
- Consultas fallidas se conservan como intentos no reutilizables.
- El historial no concede acceso a documentos que el usuario no pueda consultar.
- La eliminación será transaccional si MongoDB está configurado como replica set; si no, se utilizará estado lógico y no borrado parcial.

## Requisitos funcionales

### R1. Registrar una consulta

1. Una consulta de 1 a 2000 caracteres, no vacía, debe crear o seleccionar una sesión del usuario antes de llamar al RAG.
2. La consulta original debe conservarse exactamente.
3. La respuesta válida debe guardar texto, fecha UTC, modelo, estado y fuentes.
4. Un fallo, cancelación o timeout debe guardar el intento como inválido y no reutilizable.
5. Una consulta inválida no debe llamar al RAG ni crear una sesión.

### R2. Recuperar sesiones

1. El usuario debe ver únicamente sus sesiones autorizadas por `OwnerUserId`.
2. Las sesiones deben ordenarse por `LastActive` descendente y paginarse entre 1 y 50.
3. Al abrir una sesión, los mensajes deben aparecer en orden cronológico.
4. Cargar historial no debe llamar a Ollama.
5. Una sesión inexistente o de otro usuario debe devolver cero mensajes y un estado no disponible.

### R3. Reutilizar y buscar

1. La búsqueda debe cubrir título, consulta y respuesta, sin distinguir mayúsculas.
2. El texto buscado debe tratarse literalmente, no como expresión regular.
3. Reutilizar una consulta solo debe llenar el editor.
4. El nuevo envío debe ejecutar una recuperación nueva y generar una entrada diferente.
5. Las fuentes deben mostrar nombre, página, sección, cita y relevancia cuando existan.

### R4. Administrar y exportar

1. Archivar debe excluir una sesión activa sin eliminar sus mensajes.
2. Restaurar debe recuperar la sesión sin modificar su contenido.
3. Exportar debe producir JSON y Markdown con consulta, respuesta, idioma, fechas y fuentes.
4. Eliminar debe eliminar o marcar la sesión y sus mensajes de forma coherente.
5. Un error administrativo no debe dejar borrado parcial ni informar éxito.

### R5. Contrato RAG

El RAG debe aceptar una solicitud tipada y devolver una respuesta tipada. No se deben extraer fuentes analizando Markdown.

```text
RagQueryRequest: Query, MaxResults, CorrelationId
RagResponse: Status, AnswerText, Sources, ModelMetadata, FailureCode
```

## Fuera de alcance

- Traducción de interfaz.
- Roles nuevos.
- Estadísticas globales.
- Búsqueda vectorial indexada.
- Cambio de proveedor LLM.

## Criterio de finalización

Un usuario local autenticado puede consultar, recargar, buscar, reutilizar, exportar, archivar y eliminar su historial sin acceder al de otro usuario. Las fuentes se conservan estructuradas y existe una prueba de integración con MongoDB y Ollama ejecutados mediante Docker.
