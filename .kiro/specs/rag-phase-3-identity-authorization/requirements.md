# Requisitos: autenticación local y autorización

## Alcance

Esta fase incorpora cuentas locales con ASP.NET Core Identity, ejecutadas completamente mediante Docker Compose. Es la primera fase posterior a la base RAG. No implementa historial, localización ni proveedores externos de identidad.

## Decisiones

- **Proveedor:** ASP.NET Core Identity local.
- **Base de autenticación:** SQLite en `/app/data/auth.db`, persistida en un volumen Docker.
- **Login:** el correo electrónico es el nombre de usuario y debe ser único.
- **Confirmación:** se puede omitir únicamente en `Development`; no se debe asumir esa configuración para otro entorno.
- **Roles iniciales:** `Rag.Administrator`, `Rag.DocumentAuditor`, `Rag.N1Agent`.
- **Propietario de datos:** `ApplicationUser.Id`; el correo no autoriza ni identifica propiedad.
- **Cuentas iniciales:** seed controlado por variables de entorno y limitado a `Development`.

## Requisitos funcionales

### R1. Cuentas y login

1. Una persona debe poder crear o recibir una cuenta cuyo nombre de usuario sea un correo electrónico válido.
2. El correo debe ser único sin distinguir mayúsculas y minúsculas.
3. Una persona no autenticada que acceda a una ruta protegida debe ser enviada al login local.
4. Una persona autenticada debe poder cerrar sesión y volver a iniciar sesión.
5. Las contraseñas deben almacenarse únicamente mediante los mecanismos de hash de ASP.NET Core Identity.
6. La aplicación no debe enviar correos ni requerir un servidor SMTP en el perfil local.

### R2. Base SQLite en Docker

1. La base SQLite debe estar en una ruta configurable dentro del contenedor.
2. La ruta debe montarse en un volumen o carpeta persistente de Docker Compose.
3. Reiniciar el contenedor no debe eliminar usuarios, roles ni sesiones de autenticación.
4. El usuario no root del contenedor debe poder leer y escribir el archivo SQLite.
5. Las migraciones locales deben poder ejecutarse de forma reproducible.
6. El archivo SQLite no debe estar dentro de una capa efímera de la imagen.

### R3. Seed de desarrollo

1. El seed debe ejecutarse solo cuando el entorno sea `Development` y una opción explícita esté habilitada.
2. Debe poder crear los tres roles iniciales.
3. Debe poder crear cuentas de prueba mediante variables como `LOCAL_ADMIN_EMAIL` y `LOCAL_ADMIN_PASSWORD`.
4. Las contraseñas no deben quedar escritas en código, documentación versionada ni imágenes Docker.
5. El seed debe ser idempotente y no duplicar usuarios ni roles.
6. Producción debe iniciar sin cuentas de prueba.

### R4. Autorización por políticas

La aplicación debe definir y probar como mínimo:

- `Documents.View`
- `Documents.Process`
- `Documents.Delete`
- `Chat.Use`
- `Statistics.View`
- `Administration.Manage`

| Rol | Capacidades iniciales |
|---|---|
| `Rag.Administrator` | Todas las políticas |
| `Rag.DocumentAuditor` | `Documents.View`, `Documents.Process`, `Statistics.View` |
| `Rag.N1Agent` | `Chat.Use`, `Documents.View` |

La relación entre roles y políticas debe centralizarse en la aplicación y no repetirse en cada componente.

### R5. Protección de rutas y acciones

1. `/chat` debe exigir `Chat.Use`.
2. `/documents` debe exigir al menos `Documents.View`.
3. Reprocesar y eliminar documentos deben exigir sus políticas específicas.
4. `/statistics` debe exigir `Statistics.View`.
5. Los servicios deben volver a comprobar autorización antes de mutar datos.
6. Ocultar un botón no puede ser el único control de seguridad.
7. Una acción no autorizada no debe mutar datos ni revelar información sensible.

### R6. Identidad de aplicación

1. Los servicios deben obtener el usuario actual desde el `ClaimsPrincipal` validado.
2. El identificador interno de `ApplicationUser` debe estar disponible para las fases de historial y auditoría.
3. La aplicación no debe aceptar `UserId`, correo o rol desde la UI como prueba de identidad.
4. El contrato de actor debe conservar `UserId`, `Email`, `DisplayName` y `Roles`.

## Fuera de alcance

- Proveedores externos de identidad.
- Administración avanzada de usuarios desde la interfaz.
- Recuperación de contraseña por correo.
- Historial de consultas.
- Permisos por documento individual.

## Criterio de finalización

La aplicación ejecutada con Docker Compose permite iniciar sesión con cuentas locales por correo, conserva usuarios y roles al reiniciar, protege rutas y acciones, y pasa pruebas con usuarios autorizados y no autorizados.
