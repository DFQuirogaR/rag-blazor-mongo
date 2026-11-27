# Roadmap de especificaciones

## Objetivo

Organizar la evolución del RAG para desarrollo local con Docker, cuentas de prueba, entregas pequeñas y pruebas reproducibles entre fases. La fase 2 existente es la base técnica, aunque conserva pendientes documentados en `rag-phase-2-functional/tasks.md`.

## Estado actual del roadmap

**Línea actual: Fase 5, localización y respuestas multilingües.**

El escaneo del repositorio deja este estado:

- **Fase 2:** cerrada como línea base funcional. Sus tareas de salida obligatoria están marcadas como completadas; las optimizaciones RAG permanecen diferidas.
- **Fase 3:** checkpoint F3 cerrado. Queda una mejora no bloqueante: sustituir `EnsureCreatedAsync` por migraciones EF reproducibles para Identity.
- **Fase 4:** funcional y en cierre de checkpoint. El contrato RAG tipado, `HistoryService`, persistencia por usuario, integración de `/chat`, búsqueda, archivado/restauración, exportación, eliminación y pruebas están implementados. La suite C# pasó 33/33 y la prueba de aislamiento MongoDB pasó 1/1. Docker Compose levantó Blazor, MongoDB y Ollama saludables; queda como pendiente opcional automatizar el smoke test completo de flujo antes de cerrar la tarea 9.
- **Fase 5:** siguiente fase, bloqueada hasta registrar el cierre formal de Fase 4.
- **Fase 6:** todavía no iniciada; depende de las fases 3 a 5.
- **Fix `sidebar-collapse-fix`:** trabajo transversal pendiente de validación real del componente. Solo la limpieza de CSS y la verificación de restricciones de `MainLayout` aparecen completadas; no confundirlo con la línea principal de fases RAG.
- **Archivo `archive/rag-phase-3-history-localization-original`:** histórico, no ejecutable y fuera del estado actual.

Por tanto, el siguiente paso es registrar el cierre formal de Fase 4. Después puede iniciarse **Fase 5, tarea 1: crear los recursos completos para `es`, `en` y `pt`**.

## Secuencia aprobada

```text
Fase 2: base RAG y documentos
    |
    v
Fase 3: autenticación local y autorización
    |
    v
Fase 4: historial y trazabilidad de consultas
    |
    v
Fase 5: localización y respuestas multilingües
    |
    v
Fase 6: auditoría, estadísticas y operación multiusuario
```

Cada fase debe terminar con compilación, pruebas automatizadas y un smoke test con Docker Compose antes de comenzar la siguiente. La fase 2 tiene un conjunto reducido de tareas de salida obligatoria; sus optimizaciones avanzadas quedan como backlog y no bloquean la fase 3.

## Especificaciones vigentes

| Fase | Carpeta | Resultado | Dependencia principal |
|---|---|---|---|
| 3 | `rag-phase-3-identity-authorization` | Cuentas locales por correo, roles, políticas y protección de rutas | Fase 2 compilable y desplegable |
| 4 | `rag-phase-4-history-traceability` | Historial por usuario local, fuentes tipadas y reutilización | Fase 3 autenticada |
| 5 | `rag-phase-5-localization` | Interfaz `es`/`en`/`pt` y locale de respuesta | Fase 4 integrada |
| 6 | `rag-phase-6-audit-statistics` | Auditoría persistente, estadísticas y retención operativa | Fases 3-5 |

## Decisiones de desarrollo local

- Autenticación: ASP.NET Core Identity con cuentas locales.
- Base de autenticación: SQLite en un volumen Docker separado de MongoDB.
- Login: el campo de usuario es un correo electrónico único.
- Cuentas de prueba: se crean solo en el perfil `Development` mediante variables protegidas.
- Roles iniciales: `Rag.Administrator`, `Rag.DocumentAuditor` y `Rag.N1Agent`.
- Propietario de datos: `UserId` interno de ASP.NET Identity; el correo es descriptivo, no una clave de autorización.
- MongoDB continúa almacenando documentos, chunks, embeddings, historial y métricas RAG.
- No se incorpora ningún proveedor externo de identidad en estas fases.

## Docker obligatorio

La aplicación, MongoDB, Ollama y la base SQLite de autenticación deben ejecutarse mediante Docker Compose. El archivo SQLite debe persistir en un volumen o carpeta montada y el usuario no root del contenedor debe tener permisos de escritura sobre ella.

## Criterio de separación

- La fase 3 no implementa historial ni traducción.
- La fase 4 no cambia el modelo de lenguaje ni la estrategia de recuperación.
- La fase 5 no cambia el proveedor de autenticación ni reescribe MongoDB.
- La fase 6 no debe convertirse en una reescritura del RAG; mide y gobierna el flujo existente.

## Especificación histórica

`.kiro/specs/archive/rag-phase-3-history-localization-original` fue el plan consolidado inicial. Queda conservado como referencia, fuera de la lista de fases activas, y no se debe ejecutar.
