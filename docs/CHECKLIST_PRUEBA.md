# Checklist de prueba — SCHPOS 2.4.2

Probar con el instalador `SCHPOS-Setup-2.4.2.exe` (actualización sobre un local que ya tiene 2.4.1, y si se puede una PC limpia).

## Usuarios y poder

- [ ] Recordar logins:
  - Técnico: usuario `9999` / clave `TEC195U71`
  - Super admin: usuario `schadmin` / clave `SA195SCH71`
  - Local: `admin` / `123456` (o la clave que ya hayan cambiado)
- [ ] `admin` no aparece como “poder total”: opera el local, no identidad fiscal ni herramientas técnicas.
- [ ] `9999` y `schadmin` no se listan en Usuarios y no se pueden crear con esos nombres.
- [ ] No se puede eliminar el usuario `admin`.
- [ ] Con `9999` y `schadmin` aparece la pestaña **Soporte técnico**. Con `admin` no.

## Configuración → Negocio y ARCA

- [ ] Con `admin`: se editan nombre de fantasía, mensaje del ticket y logo.
- [ ] Con `admin`: CUIT, razón social, IVA, domicilio, teléfono, email, punto de venta y certificados ARCA se ven pero no se editan.
- [ ] El texto de ayuda no vende un servicio: aclara que esos datos los actualiza soporte técnico si cambian.
- [ ] Guardar con `admin` no pisa los datos fiscales.
- [ ] Con `9999` o `schadmin` esos campos fiscales y ARCA sí se editan y se guardan.
- [ ] El slogan sale en el ticket (debajo del nombre) y en A4/PDF si está cargado.

## Configuración → Licencia

- [ ] Con `admin`: entra, ve estado, vencimiento, extras e ID de máquina, y puede pegar una clave que ya le dieron.
- [ ] Al reemplazar una licencia existente, pide confirmación.
- [ ] Con `9999` / `schadmin`: también puede pegar y activar.
- [ ] No aparece el correo `info@schettini.com.ar` ni `info@casaschettini.com`.
- [ ] El texto aclara que el técnico genera la clave y el local puede pegarla.

## Etiquetas

- [ ] Etiquetas → Configurar etiqueta abre el popup.
- [ ] En modo Góndola se ve completa la ayuda y el botón «¿Qué modo debo usar?» (scroll si hace falta).
- [ ] Auto-corte solo aparece en modo Rollo; Góndola/A4/Cartel no cortan rollo.
- [ ] En Rollo, con auto-corte marcado, al terminar de imprimir manda el comando de la marca (ESC/POS, TSPL, ZPL o EPL).

## Configuración → Red / Mantenimiento

- [ ] Con `admin`: no puede cambiar la conexión SQL ni restaurar un backup.
- [ ] Con `9999` / `schadmin`: sí puede.

## Usuarios (grilla)

- [ ] **+ Nuevo usuario**, **Editar** y **Eliminar** (ACEPTAR/CANCELAR).
- [ ] Eliminar pregunta `¿Eliminar usuario X?`.

## Cajón de efectivo

- [ ] Configuración → Impresoras: tilde «Abrir el cajón…» marcado por defecto. Guardar y **Probar cajón** abre el cajón (cable RJ11 en la térmica).
- [ ] Cobro 100 % efectivo: el cajón se abre al guardar la venta (aunque no se imprima).
- [ ] Cobro mixto con parte en efectivo: también abre.
- [ ] Cobro solo tarjeta / QR / Point / cuenta corriente: **no** abre.
- [ ] Abrir turno (POS o pestaña Apertura) y cerrar turno: abre.
- [ ] Ingreso/egreso de caja, gasto rápido en efectivo y pago a proveedor en efectivo: abre. Gasto/pago con tarjeta o transferencia: no.
- [ ] Reimprimir un ticket: no vuelve a abrir.
- [ ] Local sin cajón: desmarcar el tilde, guardar; las ventas siguen igual.

## Táctil / ticket (si hay all-in-one)

- [ ] Scroll con el dedo en listas (sin depender solo de la barrita).
- [ ] No aparece teclado virtual de SCHPOS (se usa el de Windows si hace falta).
- [ ] Corte de papel en ticket 80/58 mm al terminar ticket / Z / prueba. A4 no se tocó.

## 2.4.1 (regresión rápida)

- [ ] Listas de precios (auto-asignar, masiva, lista por cliente en POS).
- [ ] QR MP Pantalla / Impreso / Ambos + QR de caja.
- [ ] Recargo/descuento por medio de pago en cobro rápido.
- [ ] Presupuesto → venta.
- [ ] Aviso de stock mínimo.

## Panel de licencias (https://licencias.schpos.com.ar/)

- [ ] Pestaña Actualizaciones muestra **2.4.2** y el archivo `SCHPOS-Setup-2.4.2.exe`.
- [ ] No hay módulo nuevo que tildar: cajón, slogan y candado fiscal van en la base. Etiquetas sigue siendo el extra `ACCESO_ETIQUETAS`.
- [ ] Al generar una licencia se siguen viendo Compras, Proveedores, Informes, Cta. cte., ARCA, Red, MP QR/Point, Visor y Etiquetas.
