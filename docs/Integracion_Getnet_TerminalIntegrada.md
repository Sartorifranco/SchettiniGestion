# Integración con posnet vía Getnet (Integrated POS) — Hoja de ruta

Notas de investigación + plan técnico para sumar a SCHPOS el cobro con tarjeta **automático**
(sin tipear el importe en la terminal) usando el ecosistema **Getnet** (Grupo Santander).

Se eligió Getnet como primer proveedor a integrar porque, a diferencia de Fiserv/PosNet,
publica documentación técnica pública y tiene un sandbox de autoservicio, sin pedir de entrada
un trámite de homologación como "casa de software".

---

## 1. Qué encontramos

Portal de documentación: https://docs.globalgetnet.com/es

Getnet ofrece varias formas de integración bajo "In-Store Payments":

| Modo | Cuándo usarlo |
|------|----------------|
| **App2App** | Solo Android (Intents). No aplica: SCHPOS es una app de escritorio Windows (WPF/.NET). |
| **Integrated POS** | El sistema de gestión (SCHPOS) habla directo con la terminal física, por HTTP (LAN/WiFi), USB/Serie, o **Cloud2Cloud** (a través de la nube de Getnet, sin red local). Es el modo que necesitamos. |
| **Host-to-Host** / **Cloud-to-Cloud** | Pagos sin terminal física en el mismo lugar (más pensado para e-commerce o cobro remoto). |
| **Global API (online)** | Pagos web/e-commerce con datos de tarjeta tipeados. No es nuestro caso (nosotros SÍ tenemos terminal física). |

### Ciclo de vida de "Integrated POS" (documentado)

1. **Crear un Connector**: `CreateHttp` (WiFi/Ethernet), `CreateUsb` (cable serie) o `CreateCloud` (Cloud2Cloud, sin red local).
2. **Polling**: confirmar que la terminal está conectada y lista.
3. **Operaciones de negocio**: `Sale`, `Refund`, `GetReports`, `PreAuthorization`.
4. **Close**: liberar recursos.

Es decir: arquitectónicamente es muy parecido a lo que ya hicimos con Mercado Pago Point
(ver `MercadoPagoPointService.cs` y `PointCobroWindow.xaml.cs`), pero acá la comunicación es
con la terminal en la misma PC/red (o vía Cloud2Cloud), no con una cuenta de Mercado Pago.

### Lo que NO está claro todavía (hay que preguntarlo a soporte de Getnet)

- La doc pública describe el modelo pero la comunicación real se hace mediante una
  **librería/SDK descargable** ("Integrate the Integrated POS Library"), no queda claro si
  hay una versión **.NET/Windows** o si solo hay SDKs para Android/Java/JS. **Esto es lo primero
  que hay que confirmar con soporte técnico de Getnet antes de programar nada más.**
- Si no hay SDK para Windows, la alternativa es el modo **Cloud2Cloud**, que probablemente sea
  una API REST pura (más fácil de integrar desde .NET sin depender de una librería nativa).
  Hay que pedir la documentación/Postman de ese modo específico.
- Registro de cuenta: el signup 100% autoservicio (`gsmart.com.mx/i/devsignup.jsp`) que
  encontramos es de **Getnet México**. Para Argentina, la Integration Journey oficial dice que
  el paso 1 ("Sandbox exploration") es autoservicio para *explorar documentación*, pero para
  obtener `client_id`/`client_secret` reales hay que contactar al equipo de Integration Support.

---

## 2. Plan de acción (para hacer nosotros, sin depender de un comercio)

1. **Explorar el portal ya mismo** (no requiere cuenta): https://docs.globalgetnet.com/es —
   leer a fondo "In-Store Payments → Integrated POS" y "Cloud-to-Cloud".
2. **Contactar a Integration Support de Getnet Argentina** y pedir puntualmente:
   - Acceso Sandbox (`client_id` / `client_secret`) para Argentina.
   - Confirmar si el SDK de "Integrated POS" tiene versión **Windows/.NET** (o si conviene ir
     directo por Cloud2Cloud vía REST).
   - La colección de Postman / referencia de API para Card Present (no la de e-commerce).
   - Aclarar que somos una casa de software desarrollando un sistema de gestión (SCHPOS), no
     un comercio — el registro es como integrador/ISV.
3. **Confirmar con el socio/clientes reales** (pendiente para mañana) qué terminal/procesadora
   usan hoy. Si alguno ya tiene una terminal Getnet, es el candidato ideal para las pruebas de
   campo una vez que tengamos el SDK.
4. Recién ahí programamos la integración real (paso 5).

---

## 3. Arquitectura prevista en SCHPOS (cuando tengamos SDK/credenciales)

Mismo patrón que Mercado Pago Point, para no romper flujos existentes:

- `GetnetTerminalService.cs` (nuevo, en `SchettiniGestion.WPF`):
  - `AutenticarAsync()` → obtiene token (Bearer) con `client_id`/`client_secret`.
  - `ConectarAsync()` → crea el Connector (HTTP/USB/Cloud2Cloud según configuración).
  - `RealizarVentaAsync(decimal monto, int cuotas)` → dispara `Sale` en la terminal.
  - `ConsultarEstadoAsync(...)` / `Polling` → sondea hasta aprobado/rechazado.
  - `CerrarAsync()` → libera recursos (Close).
- Config nueva en tabla `Configuracion` (mismo patrón que `MPAccessToken`/`MPPointTerminalId`):
  `GetnetClientId`, `GetnetClientSecret`, `GetnetAmbiente` (Sandbox/Producción),
  `GetnetModoConexion` (Http/Usb/Cloud2Cloud), `GetnetIpTerminal`/`GetnetPuertoTerminal`
  (solo si el modo es Http).
- UI en `ConfiguracionControl.xaml`: nueva tarjeta "Getnet (terminal integrada)" en la pestaña
  de Mercado Pago/medios electrónicos, igual de estructura que la de Point.
- UI en `CobroModalWindow`: nuevo botón "Cobrar con terminal Getnet" (mismo lugar que
  "Enviar cobro al Point"), que abre una ventana de espera tipo `PointCobroWindow.xaml.cs`
  mientras se hace polling del resultado.
- Licencia: nuevo módulo/abono `ACCESO_GETNET_TERMINAL` (igual criterio que
  `ACCESO_MERCADOPAGO_POINT`), para que sea un servicio opcional facturable aparte.
- El cobro manual con cualquier posnet sigue disponible siempre como respaldo, tal como pasa
  hoy con Point.

---

## 4. Estado actual

- [x] Investigación de mercado (Fiserv/PosNet vs. Getnet) — Getnet elegido para arrancar.
- [ ] Contacto con Integration Support de Getnet Argentina (pendiente, lo hace el usuario).
- [ ] Confirmar si existe SDK Windows/.NET o si conviene Cloud2Cloud vía REST.
- [ ] Confirmar con socio/clientes qué terminal usan hoy (pendiente, se averigua mañana).
- [ ] Programar `GetnetTerminalService.cs` con los endpoints/SDK reales.
- [ ] Agregar columnas de configuración + UI en `ConfiguracionControl`.
- [ ] Agregar botón de cobro + ventana de polling en el flujo de venta.
- [ ] Pruebas en sandbox.
- [ ] Homologación / pruebas con terminal real.
- [ ] Producción.
