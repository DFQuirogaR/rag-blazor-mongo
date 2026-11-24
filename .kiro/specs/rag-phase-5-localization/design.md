# Diseño: localización y respuestas multilingües

## Componentes

- `LocalizationService` scoped: locale actual, normalización, preferencia y fallback.
- Recursos compilados `es`, `en` y `pt`.
- `LanguageSelector`: selector único reutilizable en el layout.
- `RagQueryRequest.ResponseLocale`: idioma de la respuesta.
- `SourceReference`: datos originales que no se traducen.

## Flujo

```text
Preferencia local o es
  -> LocalizationService
  -> Chat.razor
  -> HistoryService
  -> RagQueryRequest.ResponseLocale
  -> prompt RAG
  -> respuesta explicativa en locale
  -> fuentes originales
```

El locale de interfaz y el locale de respuesta pueden compartir valor en la primera versión, pero deben estar representados como campos distintos para permitir una evolución posterior.

## Recursos

Las claves deben cubrir como mínimo:

```text
common.loading
common.error
chat.title
chat.placeholder
chat.send
chat.newSession
history.empty
history.search
history.reuse
history.archive
history.restore
history.export
history.delete
sources.title
sources.unavailable
validation.queryRequired
validation.queryTooLong
errors.persistenceUnavailable
errors.unauthorized
```

El servicio debe registrar una clave ausente una vez por locale y devolver el texto español. Los argumentos dinámicos deben pasar por formato cultural, no por concatenación insegura.

## Prompt RAG

El prompt debe indicar que:

- la explicación use el locale solicitado;
- no se inventen datos fuera del contexto;
- nombres de documentos, identificadores y citas permanezcan sin traducir;
- la ausencia de evidencia se declare en el locale solicitado.

No se debe traducir el contenido antes de generar embeddings ni modificar el texto original guardado.

## Pruebas

- Pruebas de recursos y claves faltantes.
- Pruebas de normalización y fallback.
- Pruebas de formato de fechas/números.
- Pruebas del contrato RAG con un fake de chat.
- Smoke test navegando entre las tres rutas principales.
