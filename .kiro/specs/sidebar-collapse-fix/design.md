# Diseño del fix del sidebar

**Última sincronización:** 2026-11-13

## Objetivo

Hacer que el estado de colapso del menú lateral se refleje en la interfaz sin alterar los enlaces de navegación ni el contenido principal.

El estado actual es parcial: el ancho de escritorio funciona mediante el contenedor padre, mientras que el cambio de color, las pruebas de componente real y parte del comportamiento móvil siguen pendientes.

## Arquitectura actual del componente

```text
MainLayout.razor
  ├─ sidebarCollapsed (estado y toggle)
  ├─ class="sidebar ... sidebar-collapsed" (estado del padre)
  └─ NavMenu Collapsed="sidebarCollapsed"
       ├─ class="sidebar-container collapsed" (estado del hijo)
       └─ etiquetas condicionadas por Collapsed
```

### Responsabilidades

- `MainLayout.razor`: mantiene el estado `sidebarCollapsed`, renderiza el botón y pasa el valor a `NavMenu`.
- `MainLayout.razor.css`: controla el tamaño del elemento padre, especialmente en escritorio.
- `NavMenu.razor`: renderiza enlaces e iconos y aplica la clase interna `collapsed`.
- `NavMenu.razor.css`: define el fondo, ancho y estilos visuales del menú interno.

## Diagnóstico actualizado

### Problema original

El diagnóstico original suponía que solo `NavMenu.razor.css` controlaba el tamaño y que la clase interna no se aplicaba visualmente. La implementación actual muestra que el layout padre también participa en el tamaño. Por eso, la corrección efectiva de escritorio está en las reglas del padre y en que el hijo ocupe el 100% disponible.

### Lo que funciona

- El valor booleano cambia al pulsar el botón.
- La clase del padre cambia con el estado.
- En escritorio, el padre usa aproximadamente 250px expandido y 70px colapsado.
- El menú interno recibe el parámetro `Collapsed`.
- Las etiquetas se ocultan cuando el menú está colapsado.
- Las reglas antiguas `.sidebar-expanded` y `.sidebar-collapsed` del componente interno no son necesarias para el cambio actual.

### Lo que sigue sin resolverse

- El color de fondo es el mismo en ambos estados.
- La transición actual cubre el ancho, pero no el color.
- No se han añadido de forma comprobable `overflow-y: auto` y `white-space: nowrap` en el contenedor interno.
- En móvil, el layout oculta el menú mediante `max-height` en lugar de conservar un sidebar de 70px con iconos.
- Las pruebas existentes no renderizan `MainLayout`/`NavMenu` ni inspeccionan el DOM generado.
- No se ha demostrado que el estado persista tras navegar o recargar.

## Propiedades de corrección

### Propiedad 1: actualización visual del ancho

Para cada pulsación válida del toggle:

```text
sidebarCollapsed = false -> padre aproximadamente 250px
sidebarCollapsed = true  -> padre aproximadamente 70px
```

**Estado:** parcialmente satisfecho en escritorio; falta validación real mediante navegador o componente renderizado.

### Propiedad 2: cambio visual del fondo

Para cada transición de estado, el fondo debe cambiar según las reglas definidas y animarse junto con el ancho.

**Estado:** no satisfecho. Actualmente se mantiene el mismo gradiente y no se transiciona `background-color`.

### Propiedad 3: preservación de navegación

Los enlaces, iconos, estado activo y contenido principal deben continuar funcionando al alternar el menú.

**Estado:** la estructura de navegación está presente, pero solo existe evidencia estática y no una prueba de interacción real.

## Diseño objetivo

### Escritorio

1. Mantener el estado en `MainLayout`.
2. Mantener `flex: 0 0 auto` o equivalente en el padre.
3. Definir explícitamente 250px y 70px en el mismo nivel que controla el layout.
4. Hacer que `NavMenu` ocupe `width: 100%` del padre.
5. Usar `transition: width 0.3s ease, background-color 0.3s ease` si el cambio de color forma parte del requisito final.
6. Mantener iconos visibles y ocultar solo etiquetas en el estado colapsado.

### Móvil

Elegir y documentar una sola interacción:

- **Opción A:** sidebar estrecho de 70px con iconos visibles.
- **Opción B:** menú completamente oculto/expandible mediante el toggle.

El código actual implementa principalmente la opción B, mientras que los requisitos originales describen la opción A.

### CSS del contenedor interno

La regla objetivo, pendiente de verificar contra la implementación actual, es conceptualmente:

```css
.sidebar-container {
    width: 100%;
    overflow-y: auto;
    white-space: nowrap;
    transition: width 0.3s ease, background-color 0.3s ease;
}
```

El tamaño 250px/70px debe permanecer en el padre si ese elemento es el que participa en el flex layout.

## Estrategia de validación

### Exploración

- Inspeccionar el DOM real después de pulsar el botón.
- Comprobar la clase del padre y del hijo.
- Medir el ancho computado antes y después.
- Verificar el fondo computado y la transición.
- Repetir en escritorio y móvil.

### Pruebas de preservación

- Renderizar `MainLayout`/`NavMenu` o ejecutar una prueba de navegador.
- Comprobar enlaces Inicio, Chat y Documentos.
- Comprobar iconos y etiquetas en ambos estados.
- Alternar y navegar varias veces.
- Verificar el comportamiento después de cambiar de página y recargar.

Las pruebas actuales basadas en diccionarios y el HTML aislado no sustituyen estas comprobaciones.

## Criterio de finalización

El fix podrá marcarse completo cuando:

- La anchura se compruebe en el componente real.
- La transición de color esté implementada o se retire explícitamente del requisito.
- El comportamiento móvil esté decidido y validado.
- Las pruebas de navegación y preservación pasen sobre la aplicación real.
- No haya errores de consola durante varios toggles y cambios de página.
