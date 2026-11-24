# Requisitos: auditoría, estadísticas y operación multiusuario local

## Alcance

Esta fase añade medición persistente y auditoría sobre las fases anteriores. Todo el sistema continúa ejecutándose con Docker Compose y utiliza el usuario local de ASP.NET Core Identity. No reemplaza el RAG ni cambia automáticamente de modelo, proveedor o base de datos.

## Requisitos

### R1. Auditoría de acciones

1. La aplicación debe registrar acciones relevantes con `UserId`, fecha UTC, resultado y correlation ID.
2. Debe auditarse como mínimo: login relevante, consulta RAG, acceso a documento, reprocesamiento, eliminación, exportación y cambios de sesión.
3. La auditoría no debe guardar por defecto la consulta ni la respuesta completas en logs operativos.
4. La auditoría debe distinguir éxito, rechazo y fallo.
5. Los registros deben ser consultables solo mediante una política administrativa/auditoría.

### R2. Métricas persistentes

Cada consulta debe poder producir métricas como:

- tiempo total;
- tiempo de embedding;
- tiempo de recuperación;
- tiempo del modelo;
- cantidad de chunks recuperados;
- documentos usados;
- modelo y embedding utilizados;
- locale;
- estado;
- `UserId` local.

Las métricas deben persistir o agregarse de forma que sobrevivan al reinicio de los contenedores.

### R3. Estadísticas

El dashboard debe poder mostrar, según permisos:

- consultas por periodo;
- consultas exitosas y fallidas;
- latencia media y percentiles básicos;
- documentos más consultados;
- usuarios activos agregados;
- uso por idioma y modelo;
- errores de ingesta y consulta.

No debe mostrar el contenido sensible de las consultas salvo una política y una vista explícitas de auditoría.

### R4. Retención y operación

1. Deben existir políticas configurables para historial, auditoría y métricas.
2. La limpieza debe ser verificable y no borrar registros antes del periodo configurado.
3. `/health` debe comprobar las dependencias necesarias para operar.
4. Los fallos de MongoDB, SQLite u Ollama deben diferenciarse en health checks y logs.
5. Deben existir backups y una prueba de restauración documentada.
6. El archivo SQLite de Identity debe incluirse en el procedimiento de backup local.

## Fuera de alcance

- Proveedores externos de identidad.
- Reescritura del RAG.
- Búsqueda vectorial avanzada.
- Data warehouse o Power BI.
- Exportación masiva sin autorización.

## Criterio de finalización

Un administrador local autorizado puede consultar métricas y acciones agregadas, los datos sobreviven a reinicios de Docker, existe una política de retención y el equipo puede verificar backup, restauración y salud de MongoDB, SQLite y Ollama.
