# Pendientes SCHPOS (después de 2.4.2)

Lista de lo que quedó afuera a propósito: o tiene riesgo (plata / fiscal / homologación) o es un módulo nuevo. Abordar en el próximo ciclo.

Instalador de esta entrega: `Instalador\Output\SCHPOS-Setup-2.4.2.exe`

## Qué salió en 2.4.2 (no rehacer)

- Cajón de efectivo por pulso ESC/POS en la impresora de tickets (cobro efectivo, turno, movimientos de caja).
- Popup Configurar etiqueta restaurado; auto-corte solo en modo Rollo.
- Corte de papel en ticket 80/58 mm.
- Candado fiscal: admin edita fantasía/slogan/logo; CUIT/ARCA solo técnico o super admin.
- Licencia: el local puede pegar una clave que ya le dieron. Usuarios `9999` y `schadmin` ocultos.

## Qué salió en 2.4.1 (no rehacer)

- Listas de precios: al crear una lista se asigna a todos los productos; asignación masiva (todos / marca / categoría + excepciones); lista por cliente y cambio automático en el POS.
- QR Mercado Pago: modo Pantalla / Impreso / Ambos; descargar e imprimir el QR de la caja; se guarda el ID de operación de MP.
- Recargo o descuento por medio de pago en cobro rápido (el % se carga en Medios de pago).
- Pasar presupuesto a venta (se respetan los precios cotizados).
- Aviso de stock mínimo al vender y atajo desde Inicio.
- Teclado virtual de SCHPOS eliminado: en táctil se usa el teclado nativo de Windows.

## Cobro con tarjeta / posnet

- Integración Getnet (terminal Santander): hay hoja de ruta en `docs/Integracion_Getnet_TerminalIntegrada.md`. Falta contacto con soporte, sandbox y desarrollo.
- Prisma / LaPos / Fiserv / Naranja: cada uno pide desarrollo y homologación propia. El cobro manual (posnet + registrar en SCHPOS) sigue como respaldo.
- Recargo/descuento en **pago mixto**: hoy se aplica en cobro rápido (un medio, total completo). En mixto no se recalcula el ticket.

## Mercado Pago

- Devolución automática desde nota de crédito (QR y Point). Hoy hay que devolver a mano en MP si se anula después de cobrar.
- Webhooks en lugar de polling cada 3 segundos.
- El cliente elige el medio de pago en la pantalla secundaria (`PantallaPagos` existe y no está conectada al POS).

## Presupuestos

- Usar listas de precios (y lista del cliente) al armar el presupuesto, no solo `PrecioVenta`.
- Al pasar a venta se respetan los precios cotizados; no se vuelven a calcular.

## Stock / operación

- Conteo / inventario físico (recorrer góndola y ajustar diferencias).
- Enviar PDF del comprobante por WhatsApp con un clic (hoy se guarda el archivo).

## No hacer por ahora

- Integrar “cualquier posnet” con un conector genérico: no existe en el mercado argentino.
- E-commerce, app del cliente o sucursales complejas.
