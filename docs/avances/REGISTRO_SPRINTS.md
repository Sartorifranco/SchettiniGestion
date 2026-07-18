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

---

## Sprint 2 — (pendiente)

_Placeholder. Se completará al iniciar el sprint._

**Objetivo previsto:** Flujo compras usable — factura con recepción opcional y selector de OC.

---

## Plantilla para próximos sprints

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
