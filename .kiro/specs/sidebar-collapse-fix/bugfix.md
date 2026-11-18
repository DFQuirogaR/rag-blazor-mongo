# Requisitos del bugfix del sidebar

**Última sincronización:** 2026-11-13

## Estado actual

El problema de anchura está **parcialmente corregido** en escritorio: el estado cambia y `MainLayout.razor.css` controla el padre con 250px/70px. Sin embargo, el cambio de color previsto no existe, no hay una prueba real del DOM de la aplicación y el comportamiento móvil no cumple todos los criterios originales.

## Introducción

El bug original indicaba que el estado `collapsed` cambiaba internamente, pero el ancho visible del menú lateral no se actualizaba. El diseño actual reparte la responsabilidad entre:

- `BlazorApp/Components/Layout/MainLayout.razor`, que contiene el estado y el botón de toggle.
- `BlazorApp/Components/Layout/MainLayout.razor.css`, que controla el ancho del contenedor padre en escritorio.
- `BlazorApp/Components/Layout/NavMenu.razor`, que recibe el parámetro `Collapsed`, aplica la clase interna y oculta las etiquetas.
- `BlazorApp/Components/Layout/NavMenu.razor.css`, que contiene los estilos del menú interno.

El toggle no vive dentro de `NavMenu.razor`. La documentación anterior que lo situaba allí no coincide con la implementación actual.

## Comportamiento comprobado

### Escritorio

1. `MainLayout` cambia `sidebarCollapsed` al pulsar el botón.
2. El contenedor padre recibe la clase `sidebar-collapsed`.
3. `MainLayout.razor.css` cambia el `flex-basis` y el ancho entre 250px y 70px.
4. El contenedor interno ocupa el ancho disponible del padre.
5. `NavMenu` recibe `Collapsed` y oculta las etiquetas cuando está colapsado.

Esto hace que el cambio de anchura sea razonable en escritorio, aunque no está validado por una prueba de navegador o renderizado real.

### Móvil

El contenedor padre conserva un ancho del 100% y el estado colapsado oculta el menú mediante `max-height: 0`. Este comportamiento no equivale al requisito de mantener los iconos visibles en un sidebar de 70px.

### Color y transición

El color de fondo no cambia entre estados. `NavMenu.razor.css` mantiene el mismo gradiente y la transición actual solo cubre `width`; no existe una transición efectiva de `background-color` ni una regla de color distinta para el estado colapsado.

## Requisitos funcionales

### RF1: Estado visual del toggle

- **[-] RF1.1:** al pulsar el toggle, el estado booleano cambia y el ancho de escritorio se propaga al contenedor padre.
- **[-] RF1.2:** el estado expandido usa aproximadamente 250px y el colapsado aproximadamente 70px en escritorio. La prueba visual real aún no existe.
- **[-] RF1.3:** el cambio de anchura puede animarse, pero la transición y el color no cumplen el diseño completo.

### RF2: Estados CSS

- **[x] RF2.1:** existen clases y reglas para los estados expandido/colapsado en el layout y en el menú.
- **[-] RF2.2:** el padre controla correctamente el ancho en escritorio, pero las reglas del componente hijo no son la única fuente de tamaño.
- **[ ] RF2.3:** no hay evidencia de una prueba automatizada que inspeccione el CSS compilado y el DOM de Blazor.

### RF3: Conservación de navegación

- **[x] RF3.1:** los enlaces de navegación siguen presentes y el layout conserva el contenido principal.
- **[-] RF3.2:** los iconos y etiquetas se controlan según el estado en escritorio; el comportamiento móvil oculta el menú completo.
- **[-] RF3.3:** la persistencia del estado al navegar entre páginas o recargar no está demostrada.

## Comportamiento esperado pendiente

1. El toggle debe cambiar visiblemente entre 250px y 70px en escritorio.
2. El cambio debe tener una transición de 300 ms.
3. El fondo debe cambiar de forma explícita entre los estados, si se conserva este requisito visual.
4. Las etiquetas deben ocultarse en estado colapsado y los iconos deben permanecer utilizables.
5. Los enlaces deben seguir funcionando en ambos estados.
6. El comportamiento móvil debe definirse explícitamente: sidebar de 70px con iconos o menú oculto. Actualmente mezcla ambos modelos.
7. La prueba debe ejecutarse contra el componente real, no contra diccionarios hardcodeados.

## Evidencia disponible

- `SidebarWidthBugConditionTest.cs` compara estructuras hardcodeadas y no renderiza el DOM de Blazor.
- `NavMenuPreservationTests.cs` también usa estructuras estáticas y no comprueba navegación real.
- `sidebar-width-test.html` prueba un `div` aislado, no la aplicación.
- `NavMenu.razor.test.cs.bak` espera un `ToggleSidebar()` en `NavMenu`, pero el toggle actual está en `MainLayout`.

Por lo tanto, los archivos de prueba sirven como documentación exploratoria, pero no justifican afirmar que todo el bugfix está verificado.
