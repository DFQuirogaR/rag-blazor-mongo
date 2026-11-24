# Diseño: autenticación local y autorización

## Arquitectura

```text
Navegador
  -> ASP.NET Core Identity UI
  -> cookie de autenticación
  -> ClaimsPrincipal validado
  -> políticas ASP.NET Core
  -> páginas y servicios protegidos

BlazorApp
  -> EF Core SQLite
  -> /app/data/auth.db
  -> volumen Docker auth_data
```

La aplicación seguirá siendo Blazor Server. La autenticación se añadirá a ASP.NET Core y no se implementará manualmente dentro de los componentes Razor.

## Componentes

- `ApplicationUser`: usuario local basado en `IdentityUser`.
- `ApplicationDbContext`: contexto EF Core para Identity y SQLite.
- Identity UI scaffolded: login, logout y validaciones.
- `CurrentUserContext`: proyecta `UserId`, correo, nombre y roles.
- `ApplicationPolicies`: declara políticas y requisitos de roles.
- `DevelopmentIdentitySeeder`: crea roles y cuentas de prueba solo con configuración explícita.

El contexto de usuario no debe ser singleton. El cliente SQLite se utilizará únicamente para Identity y MongoDB seguirá siendo el almacén del RAG.

## Configuración

```json
"Authentication": {
  "Provider": "Local",
  "SeedDevelopmentUsers": false,
  "DatabasePath": "/app/data/auth.db"
}
```

La cadena real puede configurarse como:

```text
ConnectionStrings__IdentityDb=Data Source=/app/data/auth.db
```

Las credenciales de seed deben llegar por variables protegidas de Docker Compose o por un archivo `.env` local no versionado. No deben estar en `appsettings.json`, `Dockerfile` ni scripts públicos.

## Docker

El servicio `blazor-app` debe montar un volumen separado:

```yaml
volumes:
  - auth_data:/app/data
```

La imagen debe crear `/app/data` con permisos de escritura para el usuario no root que ejecuta ASP.NET Core. El `docker-compose.yml` debe permitir activar el seed de desarrollo sin activar cuentas de prueba en otros perfiles.

La migración inicial de Identity debe ejecutarse de manera controlada en el perfil local. No se debe borrar el volumen para aplicar cambios.

## Cuentas de prueba

Ejemplos de configuración local, nunca para producción:

```text
LOCAL_ADMIN_EMAIL=admin@example.test
LOCAL_ADMIN_PASSWORD=...
LOCAL_AUDITOR_EMAIL=auditor@example.test
LOCAL_AUDITOR_PASSWORD=...
LOCAL_N1_EMAIL=n1@example.test
LOCAL_N1_PASSWORD=...
```

El seed debe normalizar el correo para comprobar unicidad, pero debe conservar una representación apropiada para mostrarla.

## Roles y políticas

Los roles son datos de Identity. Las políticas son el contrato usado por páginas y servicios:

```text
Documents.View
Documents.Process
Documents.Delete
Chat.Use
Statistics.View
Administration.Manage
```

Cada acción administrativa debe comprobar la política en servidor. `AuthorizeView` solo controla la presentación.

## Identidad futura de datos

La fase 3 prepara este contrato sin depender del proveedor:

```text
ApplicationActor
  Provider: Local
  UserId: string
  Email: string?
  DisplayName: string?
  Roles: string[]
```

Las fases 4 y 6 usarán `UserId` como propietario. El correo será descriptivo y no se utilizará como clave de autorización.

## Pruebas y checkpoint

- Crear, iniciar sesión y cerrar sesión con una cuenta por correo.
- Rechazar correo duplicado o inválido.
- Verificar seed idempotente.
- Verificar persistencia al reiniciar el contenedor.
- Probar cada rol contra cada política.
- Probar rutas y acciones con usuario sin permiso.
- Ejecutar todo mediante Docker Compose.
