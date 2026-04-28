# AFIP: ambiente (prueba vs producción), `AfipService` y checklist de entrega

## ¿Entendiste bien? ¿Se configura desde el frontend?

**Sí.** No hace falta recompilar ni tocar código para cambiar entre **homologación (prueba)** y **producción (facturación real)**.

- En la aplicación WPF: **Configuración** → pestaña donde está **«Negocio»** → sección **«Facturación Electrónica (AFIP)»**.
- Ahí existe el checkbox: **«Ambiente AFIP: producción (no homologación)»** (`chkAfipProduccion`).
  - **Desmarcado** (valor por defecto en base de datos **0/false**): se usa **homologación** (URLs de prueba WSAA / WSFE / padrón).
  - **Marcado**: se usa **producción** (servicios reales AFIP).

El valor se persiste en la tabla **`Configuracion`**, columna **`AfipProduccion`** (tipo `BIT`). Al guardar la configuración desde la pantalla, se actualiza esa columna junto al resto de datos del negocio.

Si la columna no existe en bases antiguas, la aplicación puede crearla mediante la migración ligera (`AsegurarMigracionLite` / scripts en arranque).

---

## ¿Qué cambió respecto del `ES_PRODUCCION` fijo en código?

Antes existía una constante interna **`ES_PRODUCCION`** en `AfipService.cs`: el ambiente **no** dependía de la configuración del usuario; había que editar código.

Ahora, en tiempo de ejecución, `AfipService` consulta:

```text
DatabaseService.GetAfipAmbienteProduccion()
```

que lee **`Configuracion.AfipProduccion`**. Ese booleano (`prod`) determina:

- **WSAA**: URL de login de certificado (homologación vs producción).
- **WSFE**: URL del servicio de facturación electrónica y obtención del último comprobante autorizado.
- **Envío del SOAP** de autorización de comprobantes.
- **Padrón A4** (consulta CUIT): URL homologación vs producción.
- Comportamiento ligado al entorno: por ejemplo, **condición IVA receptor** con CUIT en producción vs consumidor final en prueba (la lógica fiscal detallada sigue evolucionando; revisar comentarios en el código si hace falta).

**Resumen:** un solo origen de verdad: **la base de datos**, expuesta en **Configuración** en el frontend.

---

## Neto e IVA del comprobante AFIP

Para armar los importes que pide AFIP (`ImpNeto`, `ImpIVA`, totales coherentes con el XML), el servicio **no** asume un IVA fijo global del 21 % sobre el total.

Recorre los ítems de la factura y, por cada línea, usa **`AlicuotaIvaPct`** (porcentaje de IVA asociado al producto en el carrito, p. ej. 21, 10,5, 0 según corresponda):

- Si el tipo de comprobante es **11** (según la lógica actual del servicio), se trata como **exento** para ese cálculo (neto = total, IVA = 0).
- En otros casos, por cada ítem se descompone el subtotal en **neto + IVA** según la alícuota de esa línea.

**Nota:** el bloque XML de AFIP con **`<Iva><AlicIva>…`** puede seguir usando un esquema simplificado (p. ej. un código de alícuota unificado en el nodo). Si en el futuro se requieren **varias alícuotas distintas en un mismo comprobante** con múltiples nodos `<AlicIva>`, habría que extender el armado del XML; los totales agregados ya se calculan por línea en el servicio.

---

## Checklist rápido antes de entregar / ir a producción AFIP

1. **Certificado y CUIT**
   - Certificado **.pfx** correcto para el CUIT del emisor.
   - **Contraseña** del certificado guardada en configuración.
   - **CUIT** del negocio cargado y coherente con el certificado.

2. **Ambiente**
   - Probar siempre primero con **homologación** (checkbox **desmarcado**).
   - Solo cuando los comprobantes de prueba sean aceptados, marcar **«Ambiente AFIP: producción»** y volver a probar con montos bajos en un entorno controlado.

3. **Punto de venta**
   - **Punto de venta** en configuración alineado con lo dado de alta en AFIP para ese CUIT.

4. **Flujo de venta**
   - Venta **contado** con cobranzas y medios de pago.
   - Venta **cuenta corriente** si aplica, y verificar que no se dupliquen movimientos de caja.

5. **Build**
   - Compilar la solución en **Release** y desplegar el ejecutable y dependencias esperadas.

---

*Documento generado para la entrega del producto Schettini Gestión POS; puede actualizarse si cambian pantallas de configuración o reglas AFIP.*
