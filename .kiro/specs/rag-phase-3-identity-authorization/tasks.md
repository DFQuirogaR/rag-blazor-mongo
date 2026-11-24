# Tareas: autenticación local y autorización

- [ ] 1. Añadir Identity local
  - Añadir paquetes de ASP.NET Core Identity, EF Core y SQLite compatibles con el target del proyecto.
  - Crear `ApplicationUser` y `ApplicationDbContext`.
  - Configurar cookies, login, logout, validación de correo y contraseña.
  - Usar correo electrónico como `UserName` con `RequireUniqueEmail`.

- [ ] 2. Preparar Identity para Docker
  - Añadir `ConnectionStrings__IdentityDb`.
  - Crear la ruta `/app/data/auth.db`.
  - Añadir volumen `auth_data` al Compose principal.
  - Verificar permisos para el usuario no root.
  - Crear/aplicar migraciones de forma reproducible.

- [ ] 3. Crear seed de desarrollo
  - Crear `DevelopmentIdentitySeeder` idempotente.
  - Crear los roles `Rag.Administrator`, `Rag.DocumentAuditor` y `Rag.N1Agent`.
  - Leer correos y contraseñas desde variables protegidas.
  - Impedir el seed cuando no sea `Development` o no esté habilitado.
  - Probar reinicio sin duplicados.

- [ ] 4. Crear actor y políticas
  - Crear `ApplicationActor`/`CurrentUserContext`.
  - Crear `Documents.View`, `Documents.Process`, `Documents.Delete`, `Chat.Use`, `Statistics.View` y `Administration.Manage`.
  - Mapear roles a políticas en un único punto.
  - Añadir pruebas de la matriz de autorización.

- [ ] 5. Proteger la aplicación
  - Proteger `/chat`, `/documents` y `/statistics`.
  - Proteger reprocesamiento y eliminación en los servicios.
  - Mantener `/health` operativo sin datos sensibles.
  - Revisar que rechazos no muten datos.

- [ ] 6. Revisar seguridad local
  - Verificar que no se versionan contraseñas ni bases SQLite.
  - Revisar cookies seguras, antiforgery y logs sin credenciales.
  - Mantener MongoDB y Ollama en la red Docker y publicar solo lo necesario para desarrollo.

- [ ] 7. Checkpoint F3
  - Ejecutar compilación y pruebas.
  - Levantar todo con Docker Compose.
  - Iniciar sesión con las cuentas de prueba.
  - Probar cada rol contra las rutas y acciones.
  - Reiniciar `blazor-app` y comprobar que las cuentas siguen disponibles.
  - Registrar evidencia del smoke test Docker.
