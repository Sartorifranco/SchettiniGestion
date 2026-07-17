# Guía de activación de Facturación Electrónica (AFIP / ARCA) en SCHPOS

Esta guía explica, paso a paso, cómo dejar habilitado un cliente para emitir facturas
electrónicas desde SCHPOS. Está pensada para que la persona encargada del trámite pueda
hacerlo sola, sin conocimientos técnicos previos.

> **Tiempo estimado:** 20 a 30 minutos.
> **Se hace UNA SOLA VEZ por comercio (por CUIT).** No hay que repetirlo por cada venta
> ni por cada empleado. Si el comercio tiene varias cajas con la misma base de datos,
> tampoco hay que repetirlo en cada caja.

---

## Qué se necesita antes de empezar

1. **CUIT del comercio** y su **Clave Fiscal** de ARCA (nivel de seguridad 3 o superior).
2. El comercio debe estar dado de alta como **Monotributo** o **Responsable Inscripto**.
3. La PC donde se va a facturar, con **SCHPOS instalado** (versión 2.1.5 o superior).
4. Acceso a internet.

---

## PARTE 1 — En SCHPOS: generar el pedido de certificado (CSR)

1. Abrí SCHPOS y andá a **Configuración → Negocio y AFIP**.
2. En **Datos de la Empresa**, completá y verificá:
   - **CUIT** (11 dígitos).
   - **Razón Social**.
   - **Nombre de Fantasía**.
   - **Condición IVA** (Monotributo o Responsable Inscripto).
3. Presioná **Guardar** (importante: guardá antes de continuar).
4. En la sección **Activación Fiscal AFIP/ARCA**, presioná **Generar Pedido de Certificado (CSR)**.
5. El sistema te va a pedir dónde guardar un archivo que termina en **`.csr`**.
   Guardalo en un lugar fácil de encontrar (por ejemplo, el Escritorio).
   - La **clave privada (.key) queda guardada de forma segura en la PC.** No se toca ni se comparte.

> El archivo `.csr` es un "pedido de certificado". Se lo vamos a entregar a ARCA en la Parte 2.

---

## PARTE 2 — En ARCA: obtener el certificado (.crt)

1. Entrá a **[www.arca.gob.ar](https://www.arca.gob.ar)** e ingresá con **CUIT + Clave Fiscal**.
2. Buscá y entrá al servicio **"Administración de Certificados Digitales"**
   (si no aparece, hay que agregarlo desde *Administrador de Relaciones de Clave Fiscal* → Adherir servicio).
3. Presioná **"Agregar alias"**.
4. Completá:
   - **Alias:** un nombre para identificar esta PC/comercio (por ejemplo `SCHPOS-NOMBRECOMERCIO`).
   - **Archivo CSR:** presioná *Seleccionar archivo* y elegí el `.csr` que generaste en la Parte 1.
5. Presioná **"Agregar Alias"** y confirmá.
6. ARCA genera el certificado. Presioná **"Ver"** en el alias recién creado y luego
   **"Descargar"** el archivo del certificado. Es un archivo que termina en **`.crt`** (o `.pem`/`.cer`).
   Guardalo en la misma carpeta donde tenés el `.csr`.

---

## PARTE 3 — En ARCA: autorizar el servicio de facturación (wsfe)

Este paso le da permiso al certificado para poder facturar. **Es obligatorio.**

1. Seguí en ARCA con la Clave Fiscal.
2. Entrá a **"Administrador de Relaciones de Clave Fiscal"**.
3. Presioná **"Nueva Relación"**.
4. En **"Servicio"**, presioná **Buscar** y navegá:
   **AFIP → WebServices → Facturación Electrónica**
   (elegí exactamente la que dice **"Facturación Electrónica"**; NO la de MTXCA ni la de Exportación).
5. En **"Representante"**, presioná **Buscar** y seleccioná el **Computador Fiscal**:
   es el **alias** que creaste en la Parte 2.
6. Presioná **Confirmar**.

> La autorización puede tardar unos minutos en tener efecto.

---

## PARTE 4 — En ARCA: crear el punto de venta Web Services

1. Seguí en ARCA con la Clave Fiscal.
2. Entrá al servicio **"Administración de puntos de venta y domicilios"**.
3. Elegí la empresa/CUIT y entrá a **"A/B/M de puntos de venta"**.
4. Presioná **"Agregar"** y completá:
   - **Número:** el que siga al último que exista (por ejemplo, si ya hay un punto de venta
     `1` de "Factura en Línea", poné `2`).
   - **Nombre de fantasía:** el del comercio.
   - **Sistema de facturación:** elegí
     **"Factura Electrónica - Monotributo - Web Services"**
     (o **"RECE - Web Services"** si el comercio es Responsable Inscripto).
     Es importante que diga **Web Services**.
   - **Domicilio:** seleccioná el domicilio comercial.
5. Confirmá. **Anotá el número de punto de venta** que quedó asignado (por ejemplo `2`).

> El punto de venta de Web Services debe ser **distinto** del que se use para facturar
> por la web de ARCA (Factura en Línea), si es que ya se factura por ahí.

---

## PARTE 5 — En SCHPOS: importar el certificado y configurar

1. Volvé a SCHPOS → **Configuración → Negocio y AFIP**.
2. En **Activación Fiscal AFIP/ARCA**, presioná **Subir Certificado AFIP (.crt)** y elegí
   el archivo `.crt` que descargaste de ARCA en la Parte 2.
3. Cargá el **Punto de Venta** que anotaste en la Parte 4 (por ejemplo `2`).
4. Dejá tildado **"Ambiente AFIP: producción (no homologación)"** para facturar de verdad.
   - *Homologación* es solo para pruebas y requiere un certificado distinto; para uso real, siempre **producción**.
5. Presioná **Guardar**.
6. Presioná **Probar conexión AFIP**.
   - Si dice **"Conexión exitosa con WSAA en producción"** → ¡listo!
   - Si dice que el certificado no está autorizado → falta que impacte la Parte 3, esperá
     unos minutos y volvé a probar.

---

## PARTE 6 — Verificar que todo funciona

1. Hacé una venta de prueba con un **monto chico** y tipo **Factura**.
   - Recordá: en producción, esa factura es **válida fiscalmente**.
2. Si aparece un **CAE** al pie del comprobante, la factura fue autorizada por AFIP.
3. Para confirmarla en ARCA: entrá a **"Mis Comprobantes" → "Comprobantes emitidos"**,
   filtrá por la fecha, el punto de venta y el tipo de factura.
   - Puede tardar unos minutos en aparecer.

---

## Resumen rápido (checklist)

- [ ] SCHPOS: datos de empresa completos + **Guardar**
- [ ] SCHPOS: **Generar CSR** → guardar `.csr`
- [ ] ARCA: Administración de Certificados → **Agregar alias** con el `.csr` → descargar `.crt`
- [ ] ARCA: Administrador de Relaciones → **Nueva Relación** → Facturación Electrónica → representante = alias
- [ ] ARCA: Puntos de venta → **Agregar** punto de venta **Web Services** → anotar número
- [ ] SCHPOS: **Subir .crt** + cargar punto de venta + producción tildado + **Guardar**
- [ ] SCHPOS: **Probar conexión AFIP** → éxito
- [ ] Venta de prueba con CAE

---

## Problemas frecuentes

| Mensaje / síntoma | Causa | Solución |
|---|---|---|
| "Configure un CUIT válido antes de subir el certificado" | El CUIT no está guardado | Completar CUIT y presionar **Guardar** antes de subir el `.crt` |
| "Punto de venta requerido" al facturar | Falta el punto de venta | Cargar el número de la Parte 4 y **Guardar** |
| "AFIP no pudo validar la firma del certificado" | Ambiente equivocado | Tildar **producción** y **Guardar** (el `.crt` de ARCA es de producción) |
| "AFIP rechazó la autorización del certificado" | Falta la Parte 3 o no impactó aún | Verificar la relación del servicio y esperar unos minutos |
| El comprobante no aparece en "Mis Comprobantes" | Demora de ARCA | Esperar; suele tardar algunos minutos |

---

*Documento generado para SCHPOS. Ante cualquier duda técnica, contactar a soporte.*
