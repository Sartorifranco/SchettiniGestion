# Guía de pruebas — Avances (post v2.1.9)

Probar **solo** con la rama de avances, no con `main` v2.1.9.

```powershell
git fetch origin
git checkout cursor/sprint1-compras-informes-d53a
# Compilar Release y ejecutar
```

**Licencia:** incluir `ACCESO_PROVEEDORES` y `ACCESO_COMPRAS` (en Debug ya vienen en licencia de desarrollo).

---

## Sprint 1 — Menú Compras / Proveedores / Informes

### 1.1 Visibilidad en menú

| # | Paso | Esperado |
|---|------|----------|
| 1 | Login como admin con licencia que incluye Compras y Proveedores | Entra sin error |
| 2 | Revisar menú lateral | Aparecen **Proveedores**, **Compras** e **Informes** |
| 3 | Licencia Lite sin esos módulos | Esos tres botones **no** aparecen |
| 4 | Usuario sin permiso `ACCESO_COMPRAS` en rol | Compras oculto o mensaje sin permiso |

### 1.2 Proveedores

| # | Paso | Esperado |
|---|------|----------|
| 1 | Menú → Proveedores | Abre ABM |
| 2 | Alta proveedor (CUIT, razón social) | Guarda y lista |
| 3 | Editar / eliminar | Funciona sin excepción |

### 1.3 Compras — pestañas

| # | Paso | Esperado |
|---|------|----------|
| 1 | Menú → Compras | Hub con 6 pestañas visibles |
| 2 | **Facturas de compras** → Nueva factura | Modal abre, permite ítems |
| 3 | Guardar factura | Aparece en grilla; stock sube (comportamiento actual v2.1.9) |
| 4 | **Recepciones** → Nueva | CRUD básico |
| 5 | **Notas NC/ND** → Nueva | CRUD básico |
| 6 | **Gastos rápidos** → Nuevo | Registra egreso en caja |
| 7 | **Pagos** → Nuevo pago a proveedor | Actualiza saldo proveedor |

### 1.4 Órdenes de compra

| # | Paso | Esperado |
|---|------|----------|
| 1 | Pestaña **Órdenes de Compras** → Nueva | Modal con proveedor, ítems, fecha entrega |
| 2 | Guardar | Estado inicial **Pendiente** (combo deshabilitado en alta) |
| 3 | Editar orden existente | Carga ítems con descripción correcta |
| 4 | Cambiar estado a **Parcial** o **Recibida** → Guardar | Grilla muestra nuevo estado |
| 5 | **Doble clic** en fila | Abre modal de edición (no mensaje roto) |
| 6 | Eliminar orden | Confirma y quita de grilla |

### 1.5 Informes

| # | Paso | Esperado |
|---|------|----------|
| 1 | Menú → Informes | Abre `InformesControl` |
| 2 | Ventas por período + rango fechas → Generar | Grilla con datos |
| 3 | Libro IVA Ventas | Columnas neto/IVA |
| 4 | Libro IVA Compras | Datos de compras registradas |
| 5 | Productos más vendidos / Ranking clientes | Listados |
| 6 | Exportar CSV | Archivo descargable |

### 1.6 Regresión (no romper v2.1.9)

| # | Paso | Esperado |
|---|------|----------|
| 1 | Venta POS rápida | Igual que antes |
| 2 | Caja apertura/cierre | Sin cambios |
| 3 | Estadísticas (gráficos) | Sigue funcionando aparte de Informes |

### Checklist Sprint 1

- [ ] Proveedores visible y operativo
- [ ] Compras visible con 6 pestañas
- [ ] OC: crear, editar, estado, doble clic
- [ ] Informes visible y genera reportes
- [ ] Sin regresión en POS y Caja

---

## Sprint 2 — (se completará al cerrar el sprint)

_Pendiente: factura con checkbox recepcionar, selector OC, stock condicional._

---

## Sprint 3 — (pendiente)

_Pendiente: informes valorización, vendedor, faltantes pedidos._

---

## Sprint 4 — (pendiente)

_Pendiente: pulido y hub Informes unificado._
