# Diseño: historial y trazabilidad

## Flujo principal

```text
Chat.razor
  -> HistoryService
  -> CurrentUserContext / ApplicationActor
  -> ChatSessionService / MongoDB
  -> RagService tipado
  -> Ollama
  -> respuesta y fuentes
  -> persistencia de assistant
```

La UI no debe llamar directamente a `RagService` después de esta fase.

## Componentes

- `HistoryService`: coordina validación, sesión, RAG y persistencia.
- `HistoryRepository`: aplica siempre `OwnerUserId`.
- `RagQueryRequest` y `RagResponse`: separan texto de fuentes.
- `SourceReference`: conserva documento, ubicación, cita y relevancia.
- `HistoryEntryMetadata`: une consulta y respuesta mediante `EntryId`.
- `CurrentUserContext`: obtiene el usuario autenticado desde el servidor.

Los servicios de persistencia pueden ser singleton si son stateless y usan el cliente MongoDB singleton. El estado de UI no debe vivir en singleton.

## Modelos mínimos

```text
ChatSession
  Id
  OwnerUserId
  OwnerEmailSnapshot?
  Title
  Status: active | archived | deleted
  CreatedAtUtc
  LastActiveUtc

ChatMessage
  SessionId
  EntryId
  Role: user | assistant | status
  Content
  TimestampUtc
  EntryStatus
  IsReusable
  ResponseLocale?
  Sources[]
  ModelMetadata
```

`OwnerEmailSnapshot` es informativo y no autoriza operaciones. Todas las lecturas y mutaciones deben usar `OwnerUserId`.

## Fuentes

`SourceReference` debe ser un objeto persistido, no texto generado:

```text
DocumentId
StoredDocumentName
Page?
Section?
Location?
Quote?
RetrievedAtUtc
Relevance?
```

Al abrir una fuente se comprueba si el documento actual existe, pero nunca se borra la cita histórica.

## Eliminación

Al iniciar la fase se debe comprobar si MongoDB soporta transacciones. Con replica set se usa una transacción para sesión y mensajes. Sin replica set se usa `Status = deleted` y filtros obligatorios; no se simulan dos borrados independientes como si fueran atómicos.

## Índices

- `chat_sessions`: `{ OwnerUserId: 1, Status: 1, LastActiveUtc: -1 }`.
- `chat_messages`: `{ SessionId: 1, TimestampUtc: 1 }`.
- Índices de texto o proyección normalizada para búsquedas, según el volumen.

## Pruebas

- Unitarias con fakes para límites, correlación y aislamiento.
- Integración con MongoDB para persistencia, índices y eliminación.
- Integración con Ollama para una consulta conocida.
- Smoke test Docker que confirme que cargar y reutilizar no llaman al modelo hasta que el usuario confirma.
