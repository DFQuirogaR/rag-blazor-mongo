# Plan de implementación del fix del sidebar

**Última sincronización:** 2026-11-13

## Criterio de estado

- **[x]** Implementación comprobable.
- **[-]** Implementación parcial o evidencia insuficiente.
- **[ ]** Pendiente.

Los estados reflejan el código actual y no presuponen que las pruebas estáticas hayan verificado el componente Blazor real.

## Dependencias

```text
1, 2 -> 3
3 -> 3.1, 3.2, 3.3
3.1, 3.2, 3.3 -> 3.4, 3.5
3.4, 3.5 -> 4
```

## Tareas

- [-] **1. Explorar la condición del bug**
  - **Objetivo:** demostrar el comportamiento defectuoso antes del fix.
  - **Estado real:** existe `SidebarWidthBugConditionTest.cs`, pero compara diccionarios hardcodeados con valores de 250px y 70px. No ejecuta el código sin corregir ni inspecciona el DOM de Blazor.
  - **Pendiente:** registrar una línea base reproducible del componente real.
  - **Requisitos:** 1.1, 1.2, 1.3.

- [-] **2. Crear pruebas de preservación**
  - **Objetivo:** comprobar enlaces, iconos, etiquetas y navegación.
  - **Estado real:** `NavMenuPreservationTests.cs` contiene comprobaciones estáticas, pero no renderiza `NavMenu` ni prueba navegación. Además, no cubre todos los enlaces actuales.
  - **Pendiente:** prueba de interacción sobre el componente real.
  - **Requisitos:** 3.1, 3.2, 3.3.

- [-] **3. Aplicar el fix visual**

  - [-] **3.1 Actualizar el CSS del sidebar**
    - El ancho de 250px/70px está cubierto principalmente por `MainLayout.razor.css` y el hijo ocupa el espacio del padre.
    - La transición actual no cubre `background-color`.
    - No existe un cambio de fondo distinto para el estado colapsado.
    - `overflow-y: auto` y `white-space: nowrap` no están demostrados como parte de la regla actual.
    - **Archivos:** `BlazorApp/Components/Layout/MainLayout.razor.css`, `NavMenu.razor.css`.
    - **Requisitos:** 2.1, 2.2, 2.3.

  - [x] **3.2 Limpiar reglas duplicadas y obsoletas**
    - Las clases internas antiguas `.sidebar-expanded` y `.sidebar-collapsed` no son necesarias para el mecanismo actual.
    - El CSS del componente está más limpio que la versión descrita en el plan original.
    - **Archivo:** `BlazorApp/Components/Layout/NavMenu.razor.css`.
    - **Requisitos:** 3.1, 3.2, 3.3.

  - [x] **3.3 Verificar las restricciones de `MainLayout`**
    - El padre controla el ancho con reglas de flex/layout y permite que el contenedor interno ocupe el espacio disponible.
    - El toggle real y la clase del estado están en `MainLayout.razor`, no en `NavMenu.razor`.
    - **Archivos:** `BlazorApp/Components/Layout/MainLayout.razor`, `MainLayout.razor.css`.
    - **Requisitos:** 3.1, 3.2, 3.3.

  - [-] **3.4 Verificar la actualización visual**
    - La anchura de escritorio parece cambiar mediante el padre.
    - No existe una prueba de navegador o renderizado real que confirme el ancho computado.
    - El cambio de color previsto no está implementado.
    - **Requisitos:** 2.1, 2.2, 2.3.

  - [-] **3.5 Verificar la preservación**
    - La estructura de enlaces e iconos está presente.
    - No hay evidencia de ejecución de pruebas reales sobre toggles, navegación y estados móvil/escritorio.
    - La persistencia tras navegación o recarga no está demostrada.
    - **Requisitos:** 3.1, 3.2, 3.3.

- [-] **4. Checkpoint de validación**
  - No se puede marcar completo porque las tareas 1, 2, 3.1, 3.4 y 3.5 no tienen evidencia suficiente sobre la aplicación real.
  - La validación pendiente debe incluir escritorio, móvil, varios toggles, navegación, ancho computado, fondo y consola.

## Rutas actuales

- Layout y toggle: `BlazorApp/Components/Layout/MainLayout.razor`.
- CSS del layout: `BlazorApp/Components/Layout/MainLayout.razor.css`.
- Menú y parámetro `Collapsed`: `BlazorApp/Components/Layout/NavMenu.razor`.
- CSS del menú: `BlazorApp/Components/Layout/NavMenu.razor.css`.
- Pruebas exploratorias estáticas: `SidebarWidthBugConditionTest.cs` y `NavMenuPreservationTests.cs`.

## Notas

- `NavMenu.razor.test.cs.bak` espera un método `ToggleSidebar()` en `NavMenu`, pero el toggle actual está en `MainLayout`; no debe tratarse como prueba vigente.
- `sidebar-width-test.html` solo prueba un `div` aislado y no valida el CSS generado por Blazor.
- Antes de cerrar el fix hay que decidir si el estado móvil debe mostrar iconos en 70px o esconder completamente el menú.
