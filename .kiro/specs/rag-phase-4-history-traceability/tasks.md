# Tareas: historial y trazabilidad

- [ ] 1. Crear modelos y configuración
  - Añadir opciones para límite de consulta, mensajes, página y timeout.
  - Extender sesiones y mensajes con `OwnerUserId`, estado, `EntryId` y metadatos.
  - Crear `SourceReference` y contratos de exportación.

- [ ] 2. Adaptar persistencia
  - Aplicar `OwnerUserId` como filtro obligatorio.
  - Obtener el propietario desde `CurrentUserContext`, nunca desde la UI.
  - Crear índices de sesiones y mensajes.
  - Verificar replica set/transacciones o activar eliminación lógica.
  - Probar aislamiento entre dos usuarios locales.

- [ ] 3. Crear contrato RAG
  - Añadir `RagQueryRequest`, `RagResponse` y estados de resultado.
  - Adaptar `RagService` para devolver fuentes tipadas y metadatos.
  - Mantener un adaptador temporal para consumidores legacy.

- [ ] 4. Implementar `HistoryService`
  - Validar consulta antes de invocar RAG.
  - Crear/seleccionar sesión y guardar mensaje de usuario.
  - Guardar respuesta válida o intento inválido.
  - Propagar cancelación y timeout.
  - Evitar contenido completo en logs.

- [ ] 5. Integrar `/chat`
  - Sustituir dependencia directa de `RagService` por `HistoryService`.
  - Mostrar mensajes en orden y distinguir estados.
  - Cargar una sesión sin llamar a RAG.
  - Reutilizar consulta sin llamar a RAG hasta confirmar.

- [ ] 6. Añadir administración
  - Búsqueda y paginación.
  - Archivado y restauración.
  - Exportación JSON y Markdown.
  - Eliminación transaccional o lógica según capacidad de MongoDB.
  - Resolver disponibilidad actual de fuentes sin perder la cita guardada.

- [ ] 7. Pruebas
  - Validar entradas y límites.
  - Probar aislamiento por `OwnerUserId`.
  - Probar respuestas fallidas no reutilizables.
  - Probar exportación y orden cronológico.
  - Probar que cargar/reutilizar no llama a RAG.

- [ ] 8. Checkpoint F4
  - Compilar solución y pruebas.
  - Levantar todo con Docker Compose.
  - Validar con dos cuentas locales autenticadas.
  - Confirmar que no se exponen sesiones entre usuarios.
  - Registrar evidencia de MongoDB, SQLite de Identity y Ollama persistentes.
