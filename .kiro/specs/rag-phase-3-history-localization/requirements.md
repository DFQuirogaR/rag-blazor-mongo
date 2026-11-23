# Requirements Document

## Introduction

La fase 3 añadirá historial reutilizable de consultas y respuestas a la aplicación RAG existente, junto con selección de idioma para la interfaz y las respuestas del asistente. El objetivo es permitir que una persona encuentre rápidamente información consultada anteriormente sin repetir una búsqueda y que use la aplicación principalmente en español, inglés o portugués.

La fase 2 deja una base parcial compatible con esta funcionalidad: `ChatSessionService` persiste sesiones y mensajes en MongoDB, `ChatSession` ya contiene metadatos de idioma opcionales, `ChatMessage` conserva contenido y fuentes, y `Chat.razor` usa actualmente `RagService` para una consulta aislada. La fase 3 debe conectar esas capacidades sin asumir que el historial ya está activo en la interfaz. La fase 3 no modifica la ingesta de documentos ni sustituye la búsqueda RAG existente.

## Glossary

- **Aplicación_RAG**: Aplicación Blazor Server que consulta documentos procesados, genera embeddings con Ollama y produce respuestas basadas en fuentes almacenadas en MongoDB.
- **RAG_Service**: Servicio que recibe una consulta, recupera contexto documental y solicita una respuesta al modelo de lenguaje; en la ruta activa corresponde a `RagService`.
- **Chat_UI**: Página `/chat` implementada en `BlazorApp/Components/Pages/Chat.razor` y sus componentes visuales asociados.
- **History_Service**: Componente funcional responsable de crear, guardar, consultar, reutilizar, exportar y eliminar registros del historial.
- **History_Entry**: Registro de una consulta y su respuesta asociada, con fecha, idioma, estado y referencias a fuentes.
- **Chat_Session**: Agrupación ordenada de `History_Entry` perteneciente a una conversación; corresponde al modelo `ChatSession` existente.
- **Chat_Message**: Mensaje individual persistido dentro de una sesión; corresponde al modelo `ChatMessage` existente.
- **Source_Reference**: Identificador y metadatos de un documento o fragmento utilizado para generar una respuesta.
- **Idioma_de_Interfaz**: Idioma de textos, etiquetas, botones, estados, mensajes de error y ayudas mostrados por `Chat_UI`.
- **Idioma_de_Respuesta**: Idioma solicitado para el texto generado por `RAG_Service`.
- **Locale**: Código normalizado de idioma y región, como `es`, `en` o `pt`; la primera versión usará códigos de idioma sin exigir una región específica.
- **Localización**: Adaptación de textos y formatos de `Chat_UI` a un `Locale` seleccionado.
- **Persistencia_Servidor**: Almacenamiento de historial en MongoDB mediante las colecciones `chat_sessions` y `chat_messages`.
- **Persistencia_Navegador**: Almacenamiento de historial en el navegador de una persona mediante un mecanismo local del navegador.
- **Historial_Compartido**: Historial accesible por más de una persona o sesión de navegador desde un almacenamiento común.
- **Consulta_Reutilizada**: Consulta recuperada del historial y enviada otra vez a `RAG_Service` sin volver a escribirla.
- **Retención**: Regla que determina durante cuánto tiempo una `Chat_Session` o un `History_Entry` permanece disponible.
- **Respuesta_Parcial**: Resultado incompleto o error producido antes de que `RAG_Service` genere una respuesta válida.
- **Sistema_de_Registros**: Componentes de logging estructurado que registran operaciones, errores, duración e identificadores de correlación.
- **Almacén_de_Historial**: Medio de persistencia seleccionado en D1 desde el que se recuperan las sesiones y los mensajes.
- **Índice_Documental**: Conjunto actual de documentos y fragmentos disponibles para la recuperación de contexto de `RAG_Service`.
- **Suite_de_Pruebas**: Conjunto reproducible de pruebas unitarias, de integración y de interfaz que verifica los criterios de aceptación.

## Contexto y compatibilidad con la fase 2

- La página `/chat` conserva actualmente una sola consulta y una sola respuesta visible, con envío mediante Enter, salto de línea mediante Shift+Enter, límite de 2000 caracteres y renderizado Markdown.
- `ChatSessionService` ya ofrece creación de sesiones, almacenamiento de mensajes, búsqueda, archivado, eliminación, exportación JSON/Markdown y limpieza automática con una antigüedad predeterminada de 30 días.
- `ChatSession` ya contiene `UserId`, `LastActive` y `SessionMetadata.Language`, pero la aplicación no tiene autenticación ni autorización verificable en la fase 2.
- `ChatMessage` ya contiene `Role`, `Content`, `Timestamp`, `SourceDocuments` y metadatos de modelo, tokens y similitud.
- `Program.cs` registra `ChatSessionService` como singleton, `RagService` como servicio activo y MongoDB como base de datos `RagDatabase`.
- La aplicación no muestra actualmente recursos de localización ni una selección de idioma visible.
- Los errores de compilación documentados en la fase 2 no forman parte de esta especificación; la implementación futura deberá comprobar la construcción del proyecto antes de validar la fase 3.

## Alternativas de producto para confirmar

### Decisión D1: ubicación del historial

- **Alternativa A, recomendada:** `Persistencia_Servidor` con `ChatSessionService`, `chat_sessions` y `chat_messages`. Permite recuperar historial desde varios navegadores y reutiliza la base existente.
- **Alternativa B:** `Persistencia_Navegador`. Reduce cambios en MongoDB y mantiene el historial limitado al navegador, pero no permite recuperar sesiones desde otro dispositivo y puede perder datos al limpiar el almacenamiento local.
- **Alternativa C:** modalidad híbrida. Mantiene un historial local inmediato y sincroniza sesiones seleccionadas con MongoDB. Aumenta la complejidad y exige resolver conflictos y privacidad.

**Propuesta para la primera versión:** Alternativa A, siempre que se confirme el alcance de identidad de D2.

### Decisión D2: alcance de identidad y privacidad

- **Alternativa A, recomendada para una instalación sin autenticación:** historial privado de la instalación, con acceso común dentro de la aplicación y sin presentar `UserId` como control de seguridad.
- **Alternativa B:** historial por persona autenticada. Requiere incorporar o reutilizar autenticación antes de exponer `Historial_Compartido`.
- **Alternativa C:** historial anónimo por navegador. Cada navegador conserva un identificador local y solo recupera sus sesiones.

**Propuesta para la primera versión:** Alternativa C si la aplicación continúa sin autenticación; Alternativa B si se incorpora autenticación antes de esta fase. La especificación no considera seguro usar `UserId` opcional como autorización.

### Decisión D3: selección de idioma

- **Alternativa A, recomendada:** selector explícito en `Chat_UI` con `es`, `en` y `pt`, guardado como preferencia de sesión y como preferencia local de interfaz.
- **Alternativa B:** idioma inicial derivado del navegador, con selector manual posterior.
- **Alternativa C:** detección automática del idioma de cada consulta. Puede fallar en consultas cortas o mezcladas y no sustituye un selector manual.

**Propuesta para la primera versión:** Alternativa A, con español como idioma inicial cuando no existe una preferencia válida.

### Decisión D4: idioma de las respuestas y de las fuentes

- **Alternativa A, recomendada:** traducir la respuesta generada al `Idioma_de_Respuesta` seleccionado y conservar el texto de las `Source_Reference` en el idioma original.
- **Alternativa B:** generar la respuesta en el idioma de la consulta y usar el selector solo para la interfaz.
- **Alternativa C:** traducir también extractos de fuentes. Requiere distinguir claramente traducción y cita original para no alterar evidencia documental.

**Propuesta para la primera versión:** Alternativa A; los identificadores, títulos originales y citas documentales deben conservarse para trazabilidad.

### Decisión D5: retención

- **Alternativa A, recomendada:** retención configurable con valor inicial de 30 días para sesiones inactivas, alineada con la limpieza existente de `ChatSessionService`.
- **Alternativa B:** conservación indefinida hasta eliminación manual.
- **Alternativa C:** retención por cantidad máxima de sesiones o mensajes, con eliminación de los registros más antiguos.

**Propuesta para la primera versión:** Alternativa A, con aviso visible y eliminación manual de una sesión completa.

## Requirements

### Requisito 1: Registrar consultas y respuestas

**Historia de usuario:** Como persona que consulta la documentación, quiero conservar cada consulta y su respuesta asociada, para volver a revisar el resultado sin repetir la búsqueda.

#### Criterios de aceptación

1. WHEN una persona envía desde `Chat_UI` una consulta de entre 1 y 2000 caracteres que no contiene únicamente espacios, THE `History_Service` SHALL crear y seleccionar una `Chat_Session` activa antes de registrar la consulta cuando no exista una sesión seleccionada.
2. WHEN `RAG_Service` devuelve una respuesta válida para una consulta registrada, THE `History_Service` SHALL guardar un `History_Entry` que contenga la consulta, la respuesta, la fecha y hora en UTC, el `Idioma_de_Respuesta` y todas las `Source_Reference` disponibles en la respuesta.
3. WHEN `RAG_Service` devuelve una `Respuesta_Parcial` o informa un error durante el procesamiento, THE `History_Service` SHALL guardar un `History_Entry` con el estado del intento y el motivo disponible, y SHALL marcar la entrada como no válida para impedir que se presente como una respuesta completa.
4. IF una consulta contiene únicamente espacios o supera los 2000 caracteres, THEN THE `Chat_UI` SHALL impedir el envío, SHALL conservar el texto introducido y SHALL mostrar el estado de validación existente sin crear ni registrar una consulta.
5. WHEN una persona abre una `Chat_Session` guardada, THE `Chat_UI` SHALL mostrar sus `Chat_Message` en orden cronológico ascendente según su fecha y hora de creación, y SHALL mostrar una etiqueta o indicador visual distinto para las consultas, las respuestas válidas y los estados de error.

### Requisito 2: Persistir y recuperar el historial

**Historia de usuario:** Como persona que vuelve a la aplicación, quiero recuperar mis sesiones anteriores, para acceder rápidamente a información consultada previamente.

#### Criterios de aceptación

1. WHERE `Persistencia_Servidor` esté habilitada, THE `History_Service` SHALL persistir cada sesión y cada mensaje mediante las capacidades existentes de `ChatSessionService` y las colecciones `chat_sessions` y `chat_messages`.
2. WHERE `Persistencia_Navegador` esté habilitada, THE `History_Service` SHALL recuperar únicamente los registros disponibles en el navegador actual y SHALL mostrar una indicación localizada cuando una sesión o mensaje solicitado no esté disponible.
3. WHERE esté habilitada una modalidad híbrida, THE `History_Service` SHALL mostrar para cada sesión uno de los estados de sincronización `sincronizada`, `pendiente de sincronización` o `solo local`, y SHALL conservar la copia local de cada sesión cuando la persistencia del servidor no esté disponible.
4. WHEN una persona solicite la lista de sesiones, THE `History_Service` SHALL devolver las sesiones ordenadas por `LastActive` descendente y SHALL aplicar paginación con un tamaño de página configurable entre 1 y 50 sesiones.
5. IF la fuente de persistencia no responde, THEN THE `Chat_UI` SHALL mostrar un mensaje localizado que indique la indisponibilidad de la fuente y SHALL permitir iniciar una consulta nueva sin eliminar el historial local disponible.
6. WHEN una sesión se cargue mediante su identificador, THE `History_Service` SHALL verificar que la sesión pertenece al alcance de identidad confirmado en D2 antes de devolver sus mensajes.
7. IF la sesión solicitada no pertenece al alcance de identidad confirmado en D2, THEN THE `History_Service` SHALL rechazar la carga, SHALL indicar que la sesión no está disponible y SHALL devolver cero mensajes de esa sesión.

### Requisito 3: Navegar, buscar y reutilizar historial

**Historia de usuario:** Como persona que necesita consultar de nuevo un tema, quiero buscar y reutilizar consultas anteriores, para obtener información con menos pasos.

#### Criterios de aceptación

1. WHEN una persona introduce un texto de búsqueda que contiene al menos un carácter no considerado espacio en blanco en el panel de historial, THE `History_Service` SHALL devolver las sesiones cuyo título, contenido de la consulta o contenido de la respuesta contenga el texto buscado, sin distinguir entre mayúsculas y minúsculas.
2. WHEN el texto de búsqueda está vacío o contiene únicamente espacios en blanco, THE `Chat_UI` SHALL mostrar las sesiones ordenadas de la más reciente a la más antigua, hasta el número máximo definido por la configuración de paginación.
3. WHEN una persona selecciona una `Chat_Session`, THE `Chat_UI` SHALL cargar todos los mensajes almacenados de esa sesión en orden cronológico y SHALL notificar a `RAG_Service` cero veces durante la carga.
4. WHEN una persona selecciona una `Consulta_Reutilizada`, THE `Chat_UI` SHALL colocar su texto completo en el editor de consulta, SHALL permitir que la persona confirme el nuevo envío y SHALL notificar a `RAG_Service` únicamente después de dicha confirmación.
5. WHEN una persona selecciona una `Source_Reference` disponible, THE `Chat_UI` SHALL mostrar el nombre almacenado del documento y todos los metadatos de ubicación almacenados para esa fuente.
6. IF una búsqueda con texto no produce coincidencias, THEN THE `Chat_UI` SHALL mostrar un estado vacío en el idioma activo de la interfaz, SHALL indicar que no existen coincidencias y SHALL mantener visible y habilitada la opción de iniciar una consulta nueva.

### Requisito 4: Administrar sesiones y datos almacenados

**Historia de usuario:** Como persona que controla sus datos, quiero archivar, exportar y eliminar sesiones, para administrar el ciclo de vida de mi historial.

#### Criterios de aceptación

1. WHEN una persona solicita archivar una `Chat_Session`, THE `History_Service` SHALL marcar la sesión como archivada, conservar todos sus `Chat_Message` asociados y excluirla de la lista de sesiones activas.
2. WHEN una persona solicita restaurar una `Chat_Session` archivada, THE `History_Service` SHALL marcar la sesión como activa, incluirla en la lista de sesiones activas y conservar sin cambios todos sus `Chat_Message` y las fechas almacenadas.
3. WHEN una persona solicita eliminar una `Chat_Session`, THE `History_Service` SHALL eliminar la sesión y todos sus `Chat_Message` asociados como una única operación lógica, de modo que una eliminación completada no deje disponible la sesión ni sus mensajes asociados.
4. WHEN una persona solicita exportar una `Chat_Session`, THE `History_Service` SHALL ofrecer un archivo JSON y un archivo Markdown que incluyan, cuando estén disponibles, las consultas, las respuestas, las fechas, el idioma y cada `Source_Reference` de la sesión.
5. WHILE una `Chat_Session` no registra actividad durante un periodo superior al valor de `Retención` configurado y la política de limpieza automática de D5 está habilitada, THE `History_Service` SHALL marcar la sesión como elegible para limpieza automática sin eliminarla únicamente por haber sido marcada.
6. IF una operación de eliminación falla, THEN THE `Chat_UI` SHALL mostrar un error en el idioma activo de la interfaz y THE `History_Service` SHALL conservar la `Chat_Session` y todos sus `Chat_Message` asociados. IF una operación de exportación falla, THEN THE `Chat_UI` SHALL mostrar un error en el idioma activo de la interfaz y THE `History_Service` SHALL conservar sin cambios la sesión almacenada.

### Requisito 5: Seleccionar y conservar el idioma

**Historia de usuario:** Como persona que usa la aplicación en español, inglés o portugués, quiero elegir un idioma, para leer la interfaz y recibir respuestas en el idioma que prefiera.

#### Criterios de aceptación

1. WHEN una persona abre el selector de idioma, THE `Chat_UI` SHALL mostrar exactamente los tres valores seleccionables de `Locale`: `es`, `en` y `pt`, sin valores adicionales.
2. WHEN una persona selecciona uno de los `Locale` soportados (`es`, `en` o `pt`), THE `Localización` SHALL actualizar todos los textos localizados visibles de `Chat_UI` al idioma seleccionado sin reiniciar la aplicación.
3. WHEN una persona selecciona un `Locale` soportado, THE `History_Service` SHALL guardar el `Idioma_de_Interfaz` seleccionado en la preferencia local y SHALL asociarlo con la `Chat_Session` activa cuando la persistencia de la sesión esté habilitada.
4. WHEN una persona inicia una nueva sesión y no existe una preferencia local cuyo valor sea exactamente `es`, `en` o `pt`, THE `Chat_UI` SHALL establecer `es` como `Idioma_de_Interfaz` inicial.
5. IF una preferencia de idioma contiene un código distinto de `es`, `en` o `pt`, THEN THE `Chat_UI` SHALL establecer `es` como `Idioma_de_Interfaz` y SHALL reemplazar la preferencia no válida por `es`.
6. WHEN una persona navega desde cualquiera de las rutas `/chat`, `/documents` o `/statistics` hacia cualquiera de esas rutas, THE `Localización` SHALL conservar el mismo `Idioma_de_Interfaz` seleccionado durante toda la sesión de navegación.

### Requisito 6: Localizar la interfaz y sus estados

**Historia de usuario:** Como persona que no domina el idioma predeterminado de la aplicación, quiero leer etiquetas, ayudas y errores traducidos, para usar todas las funciones sin interpretar textos mezclados.

#### Criterios de aceptación

1. THE `Localización` SHALL proporcionar, para cada uno de los `Locale` `es`, `en` y `pt`, un recurso con un valor traducido para cada clave correspondiente a encabezados, botones, marcadores de posición, instrucciones, estados de carga, estados vacíos, mensajes de validación y mensajes de error.
2. WHEN `Chat_UI` cambia el `Idioma_de_Interfaz`, THE `Localización` SHALL renderizar, en la siguiente actualización visible de `Chat_UI`, todos los textos localizados visibles usando el `Locale` seleccionado.
3. WHEN falta el valor de una clave de traducción en el `Locale` seleccionado, THE `Localización` SHALL renderizar para esa clave el valor definido en el recurso `es` y SHALL registrar la clave ausente.
4. WHEN un texto localizado contiene un valor dinámico, THE `Localización` SHALL formatear ese valor conforme al `Locale` seleccionado y SHALL renderizar el texto completo sin marcadores de formato sin resolver.
5. IF un mensaje localizado no está disponible mientras `Chat_UI` muestra un error, THEN `Chat_UI` SHALL mostrar el mensaje alternativo estable definido en español para ese tipo de error, SHALL conservar el error original en registros estructurados y SHALL omitir de la interfaz cualquier detalle sensible de conexión.

### Requisito 7: Generar respuestas en el idioma seleccionado

**Historia de usuario:** Como persona que selecciona inglés o portugués, quiero que el asistente responda en el idioma seleccionado, para aprovechar el contenido de la documentación sin cambiar manualmente cada consulta.

#### Criterios de aceptación

1. WHEN una persona envía una consulta con un `Idioma_de_Respuesta` seleccionado, THE `RAG_Service` SHALL recibir el valor exacto del `Locale` seleccionado dentro del contexto de esa solicitud.
2. WHEN el `RAG_Service` genera una respuesta válida, THE `RAG_Service` SHALL redactar todo el texto explicativo de la respuesta en el `Idioma_de_Respuesta` seleccionado.
3. WHEN una consulta contiene texto en un idioma distinto del `Idioma_de_Respuesta` seleccionado, THE `RAG_Service` SHALL generar el texto explicativo en el `Idioma_de_Respuesta` seleccionado y SHALL conservar sin traducción ni modificación los identificadores técnicos y los nombres de documentos incluidos en la respuesta.
4. WHEN una respuesta incluye una `Source_Reference`, THE `Chat_UI` SHALL mostrar la identificación de la fuente en el `Idioma_de_Interfaz` seleccionado, SHALL conservar sin modificación el título original del documento y SHALL conservar sin modificación el texto de la cita.
5. IF el `Locale` seleccionado no está disponible para el modelo de lenguaje configurado, THEN THE `RAG_Service` SHALL devolver un mensaje de limitación en el `Idioma_de_Interfaz` seleccionado, SHALL indicar que no puede generar la respuesta en el idioma solicitado y SHALL no presentar la respuesta como generada o traducida al `Idioma_de_Respuesta` seleccionado; la consulta original SHALL permanecer sin cambios.
6. WHEN una respuesta se guarda, THE `History_Service` SHALL conservar el `Idioma_de_Respuesta` seleccionado y SHALL almacenar el texto original de la consulta exactamente como fue enviado, sin reemplazarlo por una traducción.

### Requisito 8: Mantener trazabilidad entre historial, fuentes y RAG

**Historia de usuario:** Como persona que revisa una respuesta anterior, quiero conocer qué fuentes y configuración produjeron el resultado, para valorar su vigencia y repetir la consulta con contexto.

#### Criterios de aceptación

1. WHEN `RAG_Service` devuelve metadatos de fuentes, THE `History_Service` SHALL guardar junto con el `History_Entry` todos los identificadores de documentos, ubicaciones de fuentes, fechas y horas de recuperación y valores de relevancia que `RAG_Service` haya proporcionado.
2. IF una respuesta guardada no contiene metadatos de fuentes, THEN THE `Chat_UI` SHALL mostrar una indicación visible de que no hay referencias de fuentes disponibles.
3. WHEN una persona reutiliza una consulta guardada, THE `RAG_Service` SHALL ejecutar una recuperación nueva contra el estado actual del `Índice_Documental` y SHALL generar el resultado reutilizado a partir de esa recuperación, sin usar la respuesta guardada como resultado vigente.
4. WHEN una persona abre una respuesta guardada, THE `Chat_UI` SHALL mostrar la fecha y hora asociadas a esa respuesta y SHALL identificarla de forma visible como resultado guardado, diferenciándola de cualquier resultado generado recientemente.
5. IF un documento referenciado en una respuesta guardada ya no existe en el `Índice_Documental`, THEN THE `Chat_UI` SHALL mostrar los datos de cita conservados en el historial y SHALL indicar que el documento actual no está disponible para abrirse.
6. WHEN una respuesta guardada incluye metadatos del modelo o del contexto, THE `History_Service` SHALL conservar todos los metadatos proporcionados junto con el `History_Entry`, aunque la `Chat_UI` no tenga que mostrarlos de forma predeterminada.

### Requisito 9: Proteger datos y operar con límites verificables

**Historia de usuario:** Como responsable de la aplicación, quiero límites y controles claros para el historial y los idiomas, para evitar crecimiento indefinido, exposición accidental de datos y respuestas ambiguas.

#### Criterios de aceptación

1. THE `History_Service` SHALL validar cada consulta nueva contra un límite configurable de entre 1 y 2000 caracteres, incluidos los espacios, antes de invocar a `RAG_Service`; si la consulta supera el límite configurado, SHALL rechazarla, indicar que excede el límite y no SHALL invocar a `RAG_Service`.
2. THE `History_Service` SHALL aplicar a cada `Chat_Session` un límite configurable de entre 1 y 10000 mensajes almacenados, SHALL indicar cuando la sesión alcance el límite y SHALL rechazar cualquier intento posterior de almacenar mensajes sin modificar los mensajes ya almacenados.
3. WHEN se habilita `Persistencia_Servidor` sin una identidad autenticada, THE `History_Service` SHALL aplicar exclusivamente el alcance de privacidad confirmado en D2 para autorizar las operaciones del historial y SHALL tratar el campo opcional `UserId` únicamente como dato descriptivo, nunca como prueba de autorización.
4. IF una solicitud intenta cargar, exportar, archivar o eliminar una sesión fuera del alcance de privacidad confirmado, THEN THE `History_Service` SHALL rechazar la operación, SHALL indicar al solicitante que la operación no está autorizada, SHALL conservar sin cambios la sesión y SHALL registrar el acceso rechazado sin registrar el contenido de la sesión.
5. WHEN la aplicación registra una operación del historial, THE `Sistema_de_Registros` SHALL registrar el tipo de operación, el identificador de sesión, la duración en milisegundos, el resultado como `éxito`, `rechazo` o `fallo`, y el identificador de correlación, y no SHALL registrar por defecto el contenido completo de la consulta ni de la respuesta.
6. WHEN la aplicación procesa una operación del historial, THE `History_Service` SHALL aceptar y respetar una cancelación recibida antes de completar la operación, SHALL indicar que la operación fue cancelada y SHALL conservar los datos previamente almacenados; para cada operación de persistencia, SHALL devolver un resultado o una indicación de tiempo agotado dentro del tiempo de espera configurable de entre 1 y 120 segundos, sin informar éxito si la operación no fue completada.

### Requisito 10: Validar la funcionalidad de extremo a extremo

**Historia de usuario:** Como responsable del proyecto, quiero pruebas reproducibles de historial y localización, para confirmar que la fase 3 funciona sobre la arquitectura real de la fase 2.

#### Criterios de aceptación

1. WHEN una prueba completa una consulta mediante la ruta `/chat` configurada para la ejecución y recibe una respuesta generada, THE `Suite_de_Pruebas` SHALL verificar que la consulta y la respuesta se pueden recuperar posteriormente desde el `Almacén_de_Historial` configurado.
2. WHEN una prueba selecciona `en` o `pt` como `Idioma_de_Interfaz`, THE `Suite_de_Pruebas` SHALL verificar que el selector y todos los textos visibles de los encabezados, el estado de carga, el estado vacío y el estado de error corresponden al `Locale` seleccionado.
3. WHEN una prueba selecciona una consulta guardada para reutilizarla, THE `Suite_de_Pruebas` SHALL verificar que `RAG_Service` recibe una solicitud adicional e independiente y que `History_Service` crea una `History_Entry` diferente de la entrada original.
4. WHEN una prueba completa la eliminación de una sesión, THE `Suite_de_Pruebas` SHALL verificar que ninguna recuperación posterior mediante `History_Service` devuelve la sesión eliminada ni ninguno de sus mensajes asociados dentro del alcance de persistencia configurado.
5. WHEN una prueba exporta una sesión, THE `Suite_de_Pruebas` SHALL verificar que cada archivo de salida JSON y Markdown contiene la consulta original, la respuesta generada y el idioma de la sesión, y que contiene todas las referencias de fuentes disponibles en la sesión en el momento de la exportación.
6. IF MongoDB u Ollama no están disponibles durante una prueba de integración, THEN THE `Suite_de_Pruebas` SHALL identificar la dependencia no disponible, marcar la prueba como no ejecutable por dependencia externa y excluirla de los fallos de comportamiento del producto.

## Decisiones requeridas antes del diseño

La siguiente fase deberá comenzar después de confirmar estas decisiones:

1. **D1, almacenamiento:** MongoDB, navegador o modalidad híbrida.
2. **D2, identidad y privacidad:** instalación común, usuario autenticado o navegador anónimo.
3. **D3, selección de idioma:** selector manual, idioma del navegador como valor inicial y/o detección automática.
4. **D4, alcance de traducción:** interfaz, respuesta, extractos de fuentes y tratamiento de citas originales.
5. **D5, retención:** 30 días configurables, conservación indefinida o límite por cantidad.
6. **D6, sesiones:** una sesión continua por persona, sesión explícita creada por la persona o agrupación automática por tema.
7. **D7, respuestas fallidas:** guardar intentos fallidos en el historial o conservar únicamente consultas con respuestas válidas.
8. **D8, modelo lingüístico:** usar un único modelo Ollama actual con instrucciones de idioma o permitir un modelo configurable por idioma.

## Criterio de finalización de requisitos

Los requisitos estarán listos para diseño cuando las decisiones D1-D8 tengan una respuesta, los criterios de aceptación hayan sido aprobados y la solución seleccionada mantenga compatibilidad explícita con `ChatSessionService`, `ChatSession`, `ChatMessage`, `RagService`, MongoDB y la página `/chat` existentes.
