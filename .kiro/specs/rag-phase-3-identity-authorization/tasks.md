# Tareas: autenticación local y autorización

- [x] 1. Añadir Identity local
  - Añadidos `Microsoft.AspNetCore.Identity.EntityFrameworkCore`, `Microsoft.AspNetCore.Identity.UI` y `Microsoft.EntityFrameworkCore.Sqlite`.
  - Creados `ApplicationUser` y `ApplicationDbContext`.
  - Configuradas cookies, login, logout, validación de correo y contraseña.
  - Configurado el correo como `UserName` con `RequireUniqueEmail`.
  - Validado el flujo de login local en Docker con una cuenta de correo.

- [-] 2. Preparar Identity para Docker
  - Añadida `ConnectionStrings__IdentityDb`.
  - Creada la ruta `/app/data/auth.db`.
  - Añadido el volumen `auth_data` al Compose principal.
  - Verificados los permisos del usuario `appuser` sobre el archivo SQLite.
  - Se utiliza `EnsureCreatedAsync` como estrategia local validada; las migraciones EF reproducibles quedan como mejora posterior.

- [x] 3. Crear seed de desarrollo
  - Creado `DevelopmentIdentitySeeder` idempotente.
  - Creados los roles `Rag.Administrator`, `Rag.DocumentAuditor` y `Rag.N1Agent`.
  - Correos y contraseñas se leen desde variables protegidas del Compose local.
  - El seed solo se ejecuta con `Development` y `SeedDevelopmentUsers=true`.
  - Verificada la creación de `/app/data/auth.db`, las cuentas y los roles.

- [x] 4. Crear actor y políticas
  - Creados `ApplicationActor`, `CurrentUserContext` y las seis políticas.
  - Roles y políticas se centralizan en `ApplicationRoles.cs`.
  - Validada la matriz de autorización con las cuentas locales de administrador, auditor y agente N1.

- [x] 5. Proteger la aplicación
  - Protegidos `/chat`, `/documents` y `/statistics` mediante políticas.
  - Las acciones de escaneo, reprocesamiento y eliminación tienen controles de autorización en la página y el flujo de servicios.
  - `/health` sigue operativo sin datos de identidad sensibles.
  - Validado que cada rol recibe o rechaza las acciones esperadas sin mutaciones indebidas.

- [x] 6. Revisar seguridad local
  - `.env`, contraseñas y `auth.db` no se versionan mediante `.gitignore` y variables del perfil local.
  - Configurados cookies, antiforgery y logout local; las pruebas confirmaron el flujo de sesión.
  - MongoDB y Ollama permanecen en la red Docker, con puertos publicados para desarrollo local.

- [x] 7. Checkpoint F3
  - Compilación de la aplicación correcta y 26 pruebas existentes correctas.
  - El perfil local Docker creó el volumen `auth_data`, la base SQLite y las cuentas de prueba.
  - Validado el login con `admin@example.test` y las restricciones de los tres roles.
  - Validado el logout, la persistencia de cuentas y la navegación protegida.
