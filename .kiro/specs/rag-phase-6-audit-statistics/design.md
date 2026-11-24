# Diseño: auditoría, estadísticas y operación

## Separación de datos

```text
chat_sessions / chat_messages
  -> historial de usuario local

rag_usage_events
  -> métricas de consultas

audit_events
  -> acciones administrativas y de seguridad

statistics_daily
  -> agregados para dashboard
```

El historial puede conservar contenido. `audit_events` y `rag_usage_events` deben guardar referencias y métricas, no duplicar automáticamente toda la conversación.

## Evento de uso RAG

```text
UserId
SessionId?
CorrelationId
CreatedAtUtc
Locale
Model
EmbeddingModel
DurationMs
EmbeddingDurationMs
RetrievalDurationMs
GenerationDurationMs
RetrievedChunkCount
DocumentIds[]
Status
FailureCode?
```

## Evento de auditoría

```text
UserId
Action
ResourceType
ResourceId?
CreatedAtUtc
Result: success | denied | failure
CorrelationId
ReasonCode?
```

No debe incluir tokens, cookies, contraseñas, connection strings ni contenido completo por defecto.

## Estadísticas

La primera versión puede consultar agregados diarios y no recalcular toda la colección en cada carga. Se deben definir índices por fecha y usuario, además de límites de rango. El dashboard debe aplicar autorización en servidor.

## Retención

Las políticas deben ser independientes:

- historial de usuario;
- eventos de uso;
- auditoría;
- agregados estadísticos.

Los eventos de auditoría pueden requerir conservación más larga que el historial. La fase debe incluir la decisión explícita de qué datos se eliminan y cuáles se conservan.

## Health y backups Docker

Añadir health checks de MongoDB, SQLite/Identity y Ollama con timeouts. Mantener el endpoint sin secretos. Documentar backup y restauración de:

- volumen MongoDB;
- volumen SQLite `auth_data`;
- volumen de modelos Ollama, cuando corresponda.

## Pruebas

- Evento por éxito, rechazo y fallo.
- Métricas con tiempos y modelo.
- Aislamiento por `UserId`.
- Restricción del dashboard para usuarios no autorizados.
- Retención con reloj controlado.
- Health check con dependencia caída.
- Reinicio de contenedores conservando usuarios, historial y métricas.
- Restauración de backup en una base aislada.
