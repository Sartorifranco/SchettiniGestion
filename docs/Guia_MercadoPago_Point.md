# Guía técnica: Mercado Pago Point en SCHPOS

Manual paso a paso para dejar un cliente cobrando con **Point Smart / Smart 2** desde SCHPOS.
Pensado para el técnico que instala o configura el sistema en el comercio.

> **Tiempo estimado:** 25 a 40 minutos (sin contar la compra/vinculación del Point con Mercado Pago).
> **Versión mínima de SCHPOS:** 2.1.8 (recomendada 2.1.9+)
> **Requisito comercial:** el abono **Mercado Pago Point** debe estar habilitado en la licencia.

---

## Qué es y cómo se conecta (resumen)

SCHPOS **no** habla por Bluetooth ni por cable con el Point. Todo pasa por internet:

1. El cajero cobra en SCHPOS y elige **Enviar cobro al Point Smart**.
2. SCHPOS llama a la **API de Mercado Pago** (Orders API, tipo `point`) con el Access Token del comercio.
3. Mercado Pago empuja el importe a la terminal Point (ya vinculada a esa cuenta).
4. El cliente paga con tarjeta en el Point.
5. SCHPOS consulta el estado cada 2 segundos hasta aprobar, rechazar o cancelar.
6. Si aprueba, registra la cobranza y sigue el flujo normal de la venta (AFIP si aplica, etc.).

```
SCHPOS (PC)  --HTTPS-->  Mercado Pago API  --nube-->  Point Smart (terminal)
```

**Importante:** el cobro manual con cualquier posnet/tarjeta sigue disponible aunque Point no esté configurado o falle.

| Dato | Para qué sirve |
|------|----------------|
| Production Access Token (`APP_USR-…`) | Autenticación de la API del comercio |
| Terminal ID | Identifica qué Point recibe el cobro |
| Modo PDV | La terminal debe estar en modo integración (no solo “standalone”) |
| Licencia `ACCESO_MERCADOPAGO_POINT` | Habilita la sección en Configuración y el botón en cobro |

Modelos admitidos por la API de MP: **Point Smart (NEWLAND N950)** y **Smart 2 (PAX A910)**.

---

## PARTE 0 — Antes de ir al local

### 0.1 Checklist comercial / licencia

1. El cliente compró el abono **Mercado Pago Point** (es un abono mensual, aparte del QR).
2. En el **panel web de licencias** (AdminLicencias):
   - Plan **Pro** → Point viene incluido con el resto.
   - Plan **Lite** → Point **no** viene; hay que elegir plan **Personalizado** y tildar **Mercado Pago Point (abono)**.
   - Plan **Personalizado** → tildar manualmente **Mercado Pago Point (abono)** (código interno: `ACCESO_MERCADOPAGO_POINT`).
3. Generar / renovar la licencia y entregársela al cliente (pegar clave o `licencia.key`).
4. Confirmar en SCHPOS que la licencia está activa y vigente.

Sin este abono, la sección Point en Configuración **no se muestra** y el botón de cobro Point **no aparece**.

### 0.2 Checklist del comercio (Mercado Pago)

El comercio debe tener, **antes** de configurar SCHPOS:

1. Cuenta de **Mercado Pago** del negocio (producción, no sandbox).
2. Un **Point Smart / Smart 2** comprado o alquilado y **vinculado** a esa misma cuenta.
3. La terminal encendida, con Wi‑Fi/datos y sincronizada con Mercado Pago.
4. En el panel de desarrolladores de Mercado Pago: un **Access Token de producción** (empieza con `APP_USR-`).

> El User ID y el Pos ID (caja QR) **no son obligatorios** para Point. Sirven para el cobro por QR. Point solo necesita token + terminal en modo PDV.

---

## PARTE 1 — Obtener el Access Token (panel Mercado Pago)

1. Entrar a [https://www.mercadopago.com.ar/developers](https://www.mercadopago.com.ar/developers) con la cuenta del comercio.
2. Ir a **Tus integraciones** → aplicación de producción del negocio (o crear una).
3. Copiar el **Production Access Token** (formato `APP_USR-…`).
4. Guardarlo en un lugar seguro: se pega una sola vez en SCHPOS → Configuración.

**Errores típicos**

| Error | Causa |
|-------|--------|
| Token de prueba / test | Empieza distinto o es de sandbox; Point en producción no responde |
| Token de otra cuenta | Las terminales listadas no coinciden con el Point del local |
| Token revocado | Regenerar en el panel de MP y volver a pegar en SCHPOS |

---

## PARTE 2 — Instalar / actualizar SCHPOS

1. Instalar o actualizar con el Setup correspondiente (**≥ 2.1.8**, ideal **2.1.9+**).
2. Activar la licencia que incluye Point.
3. Iniciar sesión (usuario `admin` u otro con acceso a Configuración).
4. Verificar internet en la PC (sin internet no hay listado de terminales ni cobros).

---

## PARTE 3 — Configurar Point dentro de SCHPOS

1. Abrir **Configuración**.
2. Ir a la sección **Integración Mercado Pago (QR)** y pegar el **Access Token** de producción.
3. (Opcional) Completar User ID / Pos ID solo si también usan QR.
4. Bajar a **Mercado Pago Point Smart (terminal automática)**.  
   Si no ves esta tarjeta: la licencia no tiene el abono Point → volver a Parte 0.
5. Pulsar **Buscar terminales**.
   - Debe aparecer al menos una terminal (ID + modo de operación).
   - Si la lista viene vacía: Point no vinculado a esa cuenta, apagado, o token incorrecto.
6. Seleccionar la terminal correcta en el combo.
7. Pulsar **Activar modo PDV**.
   - Esto le dice a Mercado Pago que esa terminal recibe órdenes desde un sistema (SCHPOS).
   - Si falla, anotar el mensaje y reintentar con la terminal online.
8. Marcar **Ofrecer cobro automático con Point en la ventana de cobro**.
9. Pulsar **Guardar** en Configuración.

Sin Guardar, el botón de cobro Point no queda habilitado.

---

## PARTE 4 — Probar un cobro de punta a punta

1. Ir a **Ventas / POS**.
2. Cargar un producto de monto chico (prueba).
3. Cobrar → en la ventana de medios de pago debe aparecer **Enviar cobro al Point Smart**.
4. Pulsarlo: se abre la ventana de espera.
5. En el Point físico debe aparecer el importe.
6. Pagar con tarjeta (o cancelar para probar el rechazo/cancelación).
7. En SCHPOS, al aprobar:
   - Se registra la cobranza como tarjeta / Mercado Pago Point.
   - Continúa el guardado de la venta (y AFIP si está activo).

Si el botón Point **no aparece**:

1. Licencia sin `ACCESO_MERCADOPAGO_POINT`.
2. No hay Terminal ID guardada.
3. El checkbox “Ofrecer cobro automático…” está desmarcado.
4. No se guardó la configuración.

---

## PARTE 5 — Uso diario (para capacitar al cajero)

1. Armar la venta como siempre.
2. Al cobrar:
   - **Point:** botón “Enviar cobro al Point Smart” → el cliente paga en la terminal.
   - **QR:** (si tiene abono QR) otro botón; es un flujo distinto.
   - **Manual:** efectivo, transferencia o tarjeta en otro posnet, como siempre.
3. Si el Point falla o el cliente cancela: cerrar/cancelar la espera y cobrar por otro medio.
4. No hace falta reconfigurar Point en cada venta.

---

## PARTE 6 — Licenciador web (referencia rápida)

| Acción en el panel | Efecto |
|--------------------|--------|
| Plan Lite | Point **apagado** |
| Plan Pro | Point **encendido** (junto al resto) |
| Personalizado + tildar “Mercado Pago Point (abono)” | Point **encendido** solo para ese cliente |
| Solo QR tildado, Point destildado | Puede cobrar QR, no Point automático |

Código de módulo: `ACCESO_MERCADOPAGO_POINT`  
Grupo: abono mensual (igual que QR y Soporte).

Después de generar la clave, hay que **activarla en el SCHPOS del cliente**. Una licencia vieja sin ese módulo no habilita Point aunque la app esté actualizada.

---

## Diferencia rápida: QR vs Point

| | Mercado Pago QR | Mercado Pago Point |
|--|-----------------|--------------------|
| Licencia | `ACCESO_MERCADOPAGO_QR` | `ACCESO_MERCADOPAGO_POINT` |
| Hardware | Celular / lector QR | Terminal Point Smart |
| Config extra | Pos ID (caja) | Terminal ID + modo PDV |
| En cobro | Muestra QR al cliente | Envía importe al Point |
| Pueden convivir | Sí, son abonos independientes | Sí |

---

## Solución de problemas

| Problema | Qué revisar |
|----------|-------------|
| No aparece sección Point en Configuración | Licencia sin abono Point; regenerar licencia y reactivar |
| “Buscar terminales” vacío | Token de otra cuenta; Point no vinculado; Point offline |
| Falla “Activar modo PDV” | Terminal apagada / sin red; reintentar; ver mensaje de la API |
| Botón Point no sale al cobrar | Guardar config; checkbox automático; terminal seleccionada |
| Orden queda “esperando” | Point sin señal; cliente no completa; cancelar y reintentar |
| Pago aprobado en Point pero no en SCHPOS | Revisar internet de la PC; no cerrar la ventana de espera a mitad |
| Error de API / 401 | Token inválido o revocado; pegar token nuevo y Guardar |

---

## Datos que guarda SCHPOS

En la configuración del sistema (base local):

- `MPAccessToken` — token de producción  
- `MPPointTerminalId` — ID de la terminal elegida  
- `MPPointAutomatico` — si se ofrece el botón en la ventana de cobro  

No se guarda la tarjeta del cliente. El detalle (marca, últimos dígitos, cuotas, ID de operación) llega desde Mercado Pago al aprobar y se asocia a la cobranza de la venta.

---

## Entrega al cliente (checklist final)

- [ ] Licencia con abono Point activa en SCHPOS  
- [ ] Access Token de producción cargado y guardado  
- [ ] Terminal listada, seleccionada y en modo PDV  
- [ ] Checkbox de cobro automático marcado  
- [ ] Prueba de cobro real o con monto mínimo exitosa  
- [ ] Cajero capacitado: Point / QR (si aplica) / cobro manual  
- [ ] Dejar claro: si Point falla, se puede cobrar igual con posnet manual  
