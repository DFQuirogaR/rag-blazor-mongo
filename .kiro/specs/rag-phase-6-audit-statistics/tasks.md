# Tareas: auditoría, estadísticas y operación

- [ ] 1. Definir eventos
  - Crear modelos `RagUsageEvent` y `AuditEvent`.
  - Definir acciones, estados y códigos de fallo.
  - Añadir `UserId`, correlación y fecha UTC.

- [ ] 2. Instrumentar consultas y acciones
  - Medir embedding, recuperación y generación.
  - Registrar documentos y chunks usados sin duplicar contenido sensible.
  - Auditar reprocesamiento, eliminación, exportación y accesos rechazados.

- [ ] 3. Persistir y consultar métricas
  - Crear colecciones e índices por fecha y usuario.
  - Añadir agregados diarios si el volumen lo requiere.
  - Aplicar paginación y límites de rango.

- [ ] 4. Completar dashboard
  - Sustituir métricas exclusivamente en memoria.
  - Mostrar latencia, estados, uso por idioma/modelo y documentos consultados.
  - Proteger la vista con `Statistics.View` y auditoría con una política específica.

- [ ] 5. Retención y privacidad
  - Configurar retención separada para historial, uso, auditoría y agregados.
  - Implementar limpieza verificable y registrar el resultado.
  - Revisar que no se borren eventos sujetos a conservación del proyecto.

- [ ] 6. Operación Docker
  - Añadir health checks reales de MongoDB, SQLite/Identity y Ollama.
  - Revisar backups de MongoDB y del volumen `auth_data`.
  - Documentar restauración en un entorno aislado.
  - Verificar permisos y persistencia de volúmenes.

- [ ] 7. Pruebas
  - Probar eventos de éxito, rechazo y fallo.
  - Probar aislamiento por `UserId`.
  - Probar acceso no autorizado al dashboard.
  - Probar retención, health checks, reinicio y restauración.

- [ ] 8. Checkpoint F6
  - Compilar y ejecutar suite completa.
  - Generar un informe de estadísticas de prueba.
  - Reiniciar los contenedores y verificar usuarios, historial y métricas.
  - Ejecutar restauración en entorno aislado.
  - Ejecutar smoke test con Docker Compose y registrar health checks, estadísticas y permisos.
