# Tareas: localización y respuestas multilingües

- [ ] 1. Crear recursos
  - Crear recursos completos para `es`, `en` y `pt`.
  - Definir español como fallback.
  - Revisar que no haya texto de interfaz incrustado sin localizar.

- [ ] 2. Implementar `LocalizationService`
  - Normalizar estrictamente los tres locales.
  - Guardar y recuperar preferencia local.
  - Notificar cambios al circuito Blazor.
  - Formatear valores dinámicos y registrar claves ausentes.

- [ ] 3. Integrar selector
  - Crear `LanguageSelector`.
  - Integrarlo en `MainLayout`, `/chat`, `/documents` y `/statistics`.
  - Copiar el idioma a la sesión activa.

- [ ] 4. Integrar idioma en RAG
  - Pasar `ResponseLocale` en `RagQueryRequest`.
  - Ajustar prompt y estados de respuesta.
  - Conservar consulta, títulos, identificadores y citas originales.
  - Manejar locale no disponible sin marcar respuesta válida.

- [ ] 5. Pruebas
  - Probar recursos completos y fallback.
  - Probar `es`, `en` y `pt`.
  - Probar formato dinámico.
  - Probar que la consulta no se traduce ni se modifica.
  - Probar preservación de fuentes.

- [ ] 6. Checkpoint F5
  - Compilar y ejecutar pruebas.
  - Navegar entre las tres rutas con cada idioma.
  - Ejecutar consulta RAG en los tres locales.
  - Confirmar que el historial conserva el idioma y las citas originales.
  - Ejecutar smoke test con Docker Compose y registrar la evidencia del flujo localizado.
