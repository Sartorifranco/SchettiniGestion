# Revisión integral — Sprints 1, 2 y 3

**Fecha:** 18 jul 2026  
**Rama revisada:** `cursor/sprint3-informes-d53a`  
**Revisor:** agente (revisión de código + pruebas automáticas BD)

---

## Resumen ejecutivo

| Sprint | Estado | Hallazgos |
|--------|--------|-----------|
| **1** — Menú Compras/Proveedores/Informes | ✅ OK | Sin bloqueantes |
| **2** — Flujo compras (recepción + OC) | ✅ OK tras fix | 1 bug corregido (edición OC) |
| **3** — Informes ampliados | ✅ OK | Mejoras menores de UX |

**Conclusión:** Los tres sprints están listos para pruebas manuales en Windows y para avanzar al Sprint 4.

---

## Sprint 1 — Checklist técnico

| Ítem | Verificado | Notas |
|------|------------|-------|
| Botones menú `Proveedores`, `Compras`, `Informes` en `PrincipalWindow` | ✅ | XAML + handlers con `PuedeModulo` / `PuedeInformes` |
| `ModulosCatalog.json` — `ACCESO_PROVEEDORES` y `ACCESO_COMPRAS` en `modulo_adicional` | ✅ | Ambos archivos (app + AdminLicencias) |
| Licencia DEBUG incluye módulos compras | ✅ | `LicenseManager.cs` |
| OC: estado editable al modificar | ✅ | `GuardarOrdenCompra(..., estado)` |
| OC: doble clic abre edición | ✅ | `ComprasControl.dgvOrdenes_MouseDoubleClick` |
| Compras: 6 pestañas cargan sin excepción | ✅ | `UserControl_Loaded` |
| Informes: 5 reportes originales | ✅ | SQL en `InformesControl` |

**Limitaciones conocidas (no bloquean):** Cuentas corrientes sigue en grupo `pendiente` del catálogo; no es parte del Sprint 1.

---

## Sprint 2 — Checklist técnico

| Ítem | Verificado | Notas |
|------|------------|-------|
| Migración `OrdenCompraID`, `StockRecibido`, `CantidadRecibida` | ✅ | `AsegurarMigracionLite` |
| `GuardarCompra` stock condicional | ✅ | Parámetro `recepcionarStock` |
| Vínculo OC + actualización estado Parcial/Recibida | ✅ | `ActualizarEstadoOrdenTrasRecepcion` |
| Recepción automática al facturar con OC | ✅ | `CrearRecepcionDesdeCompra` |
| Modal: checkbox + selector OC + carga ítems | ✅ | `CompraModalWindow` |
| Grilla facturas: columnas OC y Stock | ✅ | `ComprasControl.xaml` |
| Eliminar factura revierte stock + OC + recepciones | ✅ | Fix `62bbd78` |
| Vista factura existente solo lectura | ✅ | Mejorado en revisión |

### Bug corregido en esta revisión

**Edición de OC borraba `CantidadRecibida`:** al guardar una orden existente, `GuardarOrdenCompra` eliminaba y reinsertaba el detalle sin preservar cantidades recibidas. Ahora conserva `CantidadRecibida` (acotada a la nueva cantidad del ítem).

### Pruebas automáticas añadidas

`SchettiniGestion.Tester/Program.SprintQA.cs` — ejecutar en Windows con SQL Server:

```powershell
# Desde Visual Studio o:
.\SchettiniGestion.Tester\bin\Release\SchettiniGestion.Tester.exe
```

Cubre: factura sin stock, factura+OC con recepción, preservar `CantidadRecibida` al editar OC, eliminar factura revierte OC.

### Limitaciones conocidas (Sprint 4)

- Modal de compra siempre usa condición «Contado» (caja); cuenta corriente solo vía API directa
- No se puede editar facturas ya guardadas
- Eliminar factura Contado no revierte egreso de caja (comportamiento preexistente)

---

## Sprint 3 — Checklist técnico

| Ítem | Verificado | Notas |
|------|------------|-------|
| Valorización de Stock + fila TOTAL | ✅ | Fechas deshabilitadas al seleccionar |
| Ventas por Vendedor | ✅ | Agrupa por `NombrePersonal` |
| Faltantes en Pedidos | ✅ | Requiere tabla `Pedidos`; mensaje amigable si falta |
| Cuenta Corriente Proveedores | ✅ | Movimientos en período |
| Export CSV | ✅ | Sin cambios, compatible |
| 5 informes originales sin regresión | ✅ | SQL intacto |

### Mejoras en esta revisión

- Fechas se sincronizan al cargar el control (valorización deshabilitada desde el inicio)
- Mensaje claro si la tabla `Pedidos` no existe en la BD

---

## Cómo ejecutar la revisión completa en Windows

1. `git checkout cursor/sprint3-informes-d53a`
2. Compilar solución en **Release**
3. Ejecutar `SchettiniGestion.Tester` (pruebas BD automáticas)
4. Seguir `docs/avances/GUIA_PRUEBAS_AVANCES.md` § Sprint 1, 2 y 3 (checklist manual UI)

---

## Siguiente paso

**Sprint 4** — según roadmap: export PDF, NC/ND impactan saldo proveedor, hub Informes unificado.
