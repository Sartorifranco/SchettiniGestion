# Registro de sprints — bitácora

Documento vivo. Cada sprint cerrado agrega una sección con fecha, cambios técnicos y qué probó el equipo.

---

## Sprint 1 — Visibilidad Compras / Proveedores / Informes

| Campo | Valor |
|-------|-------|
| **Fecha** | 18 jul 2026 |
| **Rama** | `cursor/sprint1-compras-informes-d53a` |
| **PR** | https://github.com/Sartorifranco/SchettiniGestion/pull/8 |
| **Commit** | `b66d0e3` |
| **Versión objetivo al merge** | 2.2.0 (propuesta, no publicada) |

### Contexto

En v2.1.9 el código de Compras, Proveedores e Informes **ya existía** pero no tenía entrada en el menú lateral. El socio decidió lanzar sin esto. Sprint 1 es el primer paso para habilitarlo sin tocar aún el flujo contable/stock integrado.

### Qué se hizo

1. **Menú lateral** — Tres botones nuevos:
   - 🏭 Proveedores → `ProveedoresControl`
   - 🛒 Compras → `ComprasControl` (6 pestañas: facturas, recepciones, NC/ND, gastos, pagos, órdenes)
   - 📋 Informes → `InformesControl` (5 reportes + export CSV)

2. **Permisos y licencia**
   - `ACCESO_PROVEEDORES` y `ACCESO_COMPRAS` pasan de grupo `pendiente` a `modulo_adicional` en `ModulosCatalog.json`
   - Compras depende de Proveedores en el licenciador
   - Informes visible si hay licencia de Ventas, Facturación, Compras o Estadísticas

3. **Órdenes de compra — correcciones**
   - Combo de estado: Pendiente / Parcial / Recibida / Anulada (editable al modificar OC)
   - Estado persiste en BD (`GuardarOrdenCompra` actualizado)
   - Doble clic en grilla abre modal de edición (antes mostraba detalle con columnas incorrectas)

### Qué NO se hizo (queda para Sprint 2+)

- Checkbox «recepcionar mercadería» en factura de compra
- Vincular factura con orden de compra
- Separar factura contable de movimiento de stock
- NC/ND que impacten saldo proveedor
- Informes nuevos (valorización, comisiones, etc.)
- Unificar Informes + Estadísticas en un solo hub

### Archivos principales tocados

- `SchettiniGestion.WPF/PrincipalWindow.xaml(.cs)`
- `SchettiniGestion/ModulosCatalog.json`
- `AdminLicencias.Core/ModulosCatalog.json`
- `SchettiniGestion.WPF/ComprasControl.xaml.cs`
- `SchettiniGestion.WPF/OrdenCompraModalWindow.xaml(.cs)`
- `SchettiniGestion/DatabaseService.cs`

### Cómo probar

Ver sección **Sprint 1** en [GUIA_PRUEBAS_AVANCES.md](GUIA_PRUEBAS_AVANCES.md).

### Notas para el merge

- Requiere regenerar licencias de clientes que quieran Compras (nuevos módulos en catálogo)
- No afecta instalaciones Lite sin esos módulos en la clave
- Compatible con v2.1.9 en el resto del menú
- Commit adicional `62bbd78`: al eliminar factura con stock, revierte OC y recepciones vinculadas

---

## Sprint 2 — Flujo compras: recepción opcional y vínculo OC

| Campo | Valor |
|-------|-------|
| **Fecha** | 18 jul 2026 |
| **Rama** | `cursor/sprint2-flujo-compras-d53a` |
| **PR** | https://github.com/Sartorifranco/SchettiniGestion/pull/9 |
| **Commit** | `fe8d4f9` |
| **Versión objetivo al merge** | 2.2.0 (propuesta, no publicada) |

### Contexto

Sprint 1 dejó visible el módulo Compras, pero cada factura **siempre** sumaba stock. En la práctica a veces se registra la factura del proveedor antes de recibir la mercadería, o se quiere vincular la factura a una orden de compra existente.

### Qué se hizo

1. **Migración de esquema** (`AsegurarMigracionLite`)
   - `Compras.OrdenCompraID` — vínculo opcional con OC
   - `Compras.StockRecibido` — indica si la factura movió stock (histórico migrado según movimientos existentes)
   - `OrdenCompraDetalle.CantidadRecibida` — acumulado por ítem para estados Parcial/Recibida

2. **`GuardarCompra` ampliado**
   - Parámetros `ordenCompraId` y `recepcionarStock`
   - Si `recepcionarStock = false`: solo registra factura y detalle (deuda contable), **sin** stock, movimientos ni actualización de costos
   - Si `recepcionarStock = true` y hay OC: actualiza `CantidadRecibida`, estado OC (Parcial/Recibida) y crea recepción automática

3. **`CompraModalWindow`**
   - Checkbox «Recepcionar mercadería (sumar al stock)» — marcado por defecto
   - Selector de OC abierta del proveedor (Pendiente/Parcial)
   - Al elegir OC: opción de cargar ítems desde el detalle de la orden
   - Confirmación al guardar sin recepcionar
   - Edición de facturas existentes: solo lectura

4. **Grilla de facturas** (`ComprasControl`)
   - Columnas OC y Stock (checkbox)
   - Refresco de recepciones y órdenes tras guardar/eliminar factura
   - Mensaje de eliminación según si hubo movimiento de stock

### Qué NO se hizo (queda para Sprint 3+)

- Edición de facturas de compra ya guardadas
- NC/ND que impacten saldo proveedor
- Recepción parcial independiente de la factura
- Informes nuevos
- Cuenta corriente proveedor integrada con factura sin stock

### Archivos principales tocados

- `SchettiniGestion/DatabaseService.cs`
- `SchettiniGestion.WPF/CompraModalWindow.xaml(.cs)`
- `SchettiniGestion.WPF/ComprasControl.xaml(.cs)`

### Cómo probar

Ver sección **Sprint 2** en [GUIA_PRUEBAS_AVANCES.md](GUIA_PRUEBAS_AVANCES.md).

---

## Sprint 3 — Informes ampliados

| Campo | Valor |
|-------|-------|
| **Fecha** | 18 jul 2026 |
| **Rama** | `cursor/sprint3-informes-d53a` |
| **PR** | https://github.com/Sartorifranco/SchettiniGestion/pull/10 |
| **Commit** | `9f0734a` |
| **Versión objetivo al merge** | 2.2.0 (propuesta, no publicada) |

### Contexto

Sprint 1 habilitó el menú Informes con 5 reportes básicos. Sprint 3 agrega los informes que el roadmap identificó como faltantes para cerrar el hueco operativo.

### Qué se hizo

1. **Valorización de Stock** — listado de productos con stock > 0, costo unitario, valor a costo y valor con IVA estimado; fila TOTAL al pie
2. **Ventas por Vendedor** — agrupa facturas por `NombrePersonal` en el período
3. **Faltantes en Pedidos** — ítems de pedidos Pendiente/Confirmado donde la cantidad pedida supera el stock actual
4. **Cuenta Corriente Proveedores** — movimientos de CC proveedor en el período (fecha, proveedor, descripción, monto, saldo)

### UX

- Combo de informes ampliado (9 tipos)
- Valorización de Stock deshabilita selectores de fecha (es instantáneo «a hoy»)
- Export CSV funciona con los nuevos informes

### Qué NO se hizo (queda para Sprint 4)

- Export PDF
- Hub unificado Informes + Estadísticas
- Informe de comisiones con reglas configurables
- Resumen de saldos proveedores sin movimientos en período

### Archivos principales tocados

- `SchettiniGestion.WPF/InformesControl.xaml(.cs)`

### Cómo probar

Ver sección **Sprint 3** en [GUIA_PRUEBAS_AVANCES.md](GUIA_PRUEBAS_AVANCES.md).

---

## Conclusión de la revisión

Los Sprints 1–3 quedaron **aprobados para pruebas manuales** tras correcciones en commit `95b22ab`. Ver detalle en [REVISION_SPRINTS_1_2_3.md](REVISION_SPRINTS_1_2_3.md).

---

## Sprint 4 — Pulido: PDF, NC/ND saldo, hub Informes

| Campo | Valor |
|-------|-------|
| **Fecha** | 18 jul 2026 |
| **Rama** | `cursor/sprint4-pulido-d53a` |
| **PR** | _(se completará al abrir PR)_ |
| **Versión objetivo al merge** | 2.2.0 (propuesta) |

### Qué se hizo

1. **NC/ND compras impactan cuenta corriente proveedor**
   - `GuardarNotaCreditoDebitoCompra`: NC reduce saldo, ND lo aumenta + movimiento CC
   - `EliminarNotaCreditoDebitoCompra` revierte el impacto

2. **Export PDF en informes tabulares**
   - `PdfInformeGenerator` + botón PDF en `InformesControl`

3. **Hub Informes unificado**
   - Pestañas: «Informes tabulares» + «Gráficos y KPIs» (si licencia Estadísticas)
   - Botón menú **Estadísticas** oculto (evita duplicado)

### Archivos principales

- `SchettiniGestion/DatabaseService.cs`
- `SchettiniGestion.WPF/InformesControl.xaml(.cs)`
- `SchettiniGestion.WPF/PdfInformeGenerator.cs`
- `SchettiniGestion.WPF/PrincipalWindow.xaml.cs`

---

```markdown
## Sprint N — Título

| Campo | Valor |
|-------|-------|
| **Fecha** | |
| **Rama** | |
| **PR** | |
| **Commit** | |

### Qué se hizo
-

### Qué NO se hizo
-

### Cómo probar
Ver GUIA_PRUEBAS_AVANCES.md § Sprint N
```
