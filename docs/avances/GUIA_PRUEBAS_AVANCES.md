# Guía de pruebas — Avances (post v2.1.9)

Probar **solo** con la rama de avances, no con `main` v2.1.9.

```powershell
git fetch origin
git checkout cursor/sprint3-informes-d53a
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
| 3 | Guardar factura (checkbox recepcionar **marcado**) | Aparece en grilla; stock sube; columna Stock ✓ |
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

## Sprint 2 — Factura con recepción opcional y OC

### 2.1 Factura sin recepcionar stock

| # | Paso | Esperado |
|---|------|----------|
| 1 | Compras → Facturas → Nueva factura | Modal con checkbox «Recepcionar mercadería» marcado |
| 2 | Elegir proveedor, agregar ítems | Detalle OK |
| 3 | **Desmarcar** «Recepcionar mercadería» → Guardar | Pide confirmación |
| 4 | Confirmar | Factura en grilla; columna **Stock** sin tilde |
| 5 | Verificar stock del producto | **No** cambió |
| 6 | Doble clic en la factura | Solo lectura; checkbox deshabilitado |

### 2.2 Factura vinculada a orden de compra

| # | Paso | Esperado |
|---|------|----------|
| 1 | Crear OC con proveedor X (estado Pendiente) e ítems | OC en grilla |
| 2 | Nueva factura → proveedor X | Combo OC muestra la orden abierta |
| 3 | Seleccionar OC → confirmar cargar ítems | Detalle se llena desde la OC |
| 4 | Con recepcionar **marcado** → Guardar | Factura con OC#; stock sube |
| 5 | Pestaña Órdenes de Compras | OC pasa a **Parcial** o **Recibida** según cantidades |
| 6 | Pestaña Recepciones | Aparece recepción vinculada a la factura |

### 2.3 Factura con OC sin recepcionar

| # | Paso | Esperado |
|---|------|----------|
| 1 | Nueva factura + OC seleccionada | Ítems cargados |
| 2 | Desmarcar recepcionar → Guardar | Confirma «sin mover stock» |
| 3 | Grilla facturas | OC# visible; Stock sin tilde |
| 4 | Grilla OC | Estado OC **no** cambia |

### 2.4 Eliminar factura

| # | Paso | Esperado |
|---|------|----------|
| 1 | Eliminar factura **con** stock recibido | Mensaje advierte reversión de stock; stock baja |
| 2 | Eliminar factura **sin** stock recibido | Mensaje indica que no hubo movimiento; stock intacto |

### Checklist Sprint 2

- [ ] Checkbox recepcionar funciona (sí/no stock)
- [ ] Selector OC carga ítems del proveedor correcto
- [ ] OC actualiza estado Parcial/Recibida al recepcionar
- [ ] Recepción automática al facturar con OC + stock
- [ ] Grilla muestra columnas OC y Stock
- [ ] Sin regresión Sprint 1 (menú, OC manual, Informes)

---

## Sprint 3 — Informes ampliados

### 3.1 Valorización de Stock

| # | Paso | Esperado |
|---|------|----------|
| 1 | Informes → **Valorización de Stock** → Generar | Fechas deshabilitadas |
| 2 | Revisar grilla | Productos con stock > 0, columnas Costo, ValorCosto, ValorConIVA |
| 3 | Última fila | Fila **TOTAL** con sumas |
| 4 | Exportar CSV | Archivo con totales |

### 3.2 Ventas por Vendedor

| # | Paso | Esperado |
|---|------|----------|
| 1 | Registrar ventas con distintos usuarios (NombrePersonal en factura) | — |
| 2 | Informes → **Ventas por Vendedor** + rango fechas → Generar | Grilla por vendedor con comprobantes y total |
| 3 | Ventas sin vendedor | Aparecen como «(Sin vendedor)» |

### 3.3 Faltantes en Pedidos

| # | Paso | Esperado |
|---|------|----------|
| 1 | Crear pedido Pendiente con cantidad > stock del producto | — |
| 2 | Informes → **Faltantes en Pedidos** → Generar | Lista pedido, producto, cant pedida, stock, faltante |
| 3 | Pedido con stock suficiente | **No** aparece en el informe |
| 4 | Pedido Confirmado con faltante | **Sí** aparece |

### 3.4 Cuenta Corriente Proveedores

| # | Paso | Esperado |
|---|------|----------|
| 1 | Registrar compra en cuenta corriente o pago a proveedor | Movimiento en CC |
| 2 | Informes → **Cuenta Corriente Proveedores** + fechas → Generar | Movimientos con proveedor, monto y saldo histórico |
| 3 | Exportar CSV | Archivo descargable |

### Checklist Sprint 3

- [ ] Valorización con totales
- [ ] Ventas por vendedor en período
- [ ] Faltantes en pedidos pendientes/confirmados
- [ ] Movimientos CC proveedores
- [ ] Export CSV en los 4 informes nuevos
- [ ] Sin regresión en los 5 informes originales

---

## Sprint 4 — (pendiente)

_Pendiente: pulido y hub Informes unificado._
