# Roadmap cloud y e-commerce — SCHPOS v3.0

**Documento estratégico** (no compromete desarrollo hasta acuerdo explícito).  
**Última actualización:** 29 jul 2026  
**Baseline técnico:** SCHPOS v2.2 (rama `cursor/avances-v2.2-d53a`, PR #12) sobre v2.1.9.

---

## 1. Resumen ejecutivo

Hoy SCHPOS es una aplicación **desktop WPF** con **SQL Server local/LAN**, licenciamiento offline y integraciones puntuales (AFIP, Mercado Pago QR/Point). **No existe** API propia, sincronización en la nube, panel web ni conectores a tiendas online.

La evolución natural hacia **v3.0** implica tres capas nuevas:

| Capa | Qué aporta | Prioridad sugerida |
|------|------------|-------------------|
| **API cloud + BD central** | Multi-sucursal, backup, licencias online, datos para informes | Alta |
| **Agente de sync (desktop)** | El POS sigue operando offline; replica cambios con la nube | Alta |
| **Panel web (PWA)** | Consultas, informes, configuración remota; no reemplaza el POS al inicio | Media |
| **Conector e-commerce** | Stock y pedidos web → SCHPOS (Tienda Nube, ML, WooCommerce) | Media-baja (según cliente) |

**Recomendación:** no saltar directo a “app 100 % web”. Mantener el POS desktop como fuente de verdad en caja y agregar **cloud como espejo + panel**, luego **un conector** de e-commerce según demanda del mercado.

**Costos de implementación (solo infra y servicios, sin desarrollo):** piloto **USD 80 – 200/mes**; operación con 10–50 comercios **USD 150 – 450/mes**; dominio **USD 10 – 75/año**. Si el comercio vende online, suma su plan de Tienda Nube (~ARS 8.000 – 35.000/mes) o comisión de Mercado Libre. Detalle en §5.

---

## 2. Situación actual (inventario honesto)

### Lo que ya tenemos

- POS, facturación, stock, clientes, caja, compras (v2.2), informes ampliados
- SQL Server LocalDB o instancia LAN (`DatabaseService.cs`)
- Módulos vendibles vía `LicenseManager` / AdminLicencias
- AFIP (factura electrónica)
- Mercado Pago (QR y Point Smart vía API de MP)
- Instalador Windows y flujo de licencia local

### Lo que no tenemos

| Componente | Impacto |
|------------|---------|
| API REST/GraphQL propia | Sin base para web, móvil ni integraciones |
| Modelo multi-tenant | Cada cliente = una BD aislada hoy |
| Sync bidireccional | Sin visibilidad centralizada ni backup automático |
| Autenticación cloud (OAuth/JWT) | Login solo local |
| Webhooks e-commerce | Sin recepción de pedidos online |
| App móvil nativa | No existe |

### Riesgos técnicos a resolver antes de cloud

1. **Licenciamiento:** hoy hay rutas offline y posibles inconsistencias entre generadores y `LicenseManager` — en cloud conviene un único servicio de activación.
2. **Esquema de BD:** pensado para un solo comercio; multi-sucursal requiere `TenantId` / `SucursalId` en tablas clave o BD por tenant.
3. **Conflictos de sync:** ventas offline en dos cajas del mismo local — hace falta cola de eventos y reglas de resolución (último timestamp, stock reservado, etc.).

---

## 3. Arquitectura objetivo (v3.0)

```
┌─────────────────────────────────────────────────────────────────┐
│                        NUBE (Azure / similar)                    │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────────────┐ │
│  │  API REST    │  │  SQL Azure   │  │  Panel Web (PWA)     │ │
│  │  (.NET 8)    │──│  o Postgres  │  │  React/Blazor        │ │
│  └──────┬───────┘  └──────────────┘  └──────────────────────┘ │
│         │                                                        │
│  ┌──────┴───────┐  ┌──────────────┐  ┌──────────────────────┐ │
│  │ Servicio     │  │ Blob Storage │  │ Conector e-commerce  │ │
│  │ licencias    │  │ (PDF, imgs)  │  │ (TN / ML / Woo)      │ │
│  └──────────────┘  └──────────────┘  └──────────────────────┘ │
└────────────────────────────┬────────────────────────────────────┘
                             │ HTTPS (sync incremental)
        ┌────────────────────┼────────────────────┐
        │                    │                    │
   ┌────▼─────┐         ┌────▼─────┐         ┌────▼─────┐
   │ SCHPOS   │         │ SCHPOS   │         │ Agente   │
   │ Caja 1   │         │ Caja 2   │         │ sync     │
   │ LocalDB  │         │ LAN SQL  │         │ (servicio│
   └──────────┘         └──────────┘         │ Windows) │
                                              └──────────┘
```

### Principios de diseño

1. **Offline-first en caja:** si cae internet, el POS sigue vendiendo; el agente encola y sincroniza al volver la conexión.
2. **Eventos, no réplica ciega de tablas:** cada venta, ajuste de stock o alta de producto es un evento versionado (`EventId`, `TenantId`, `SucursalId`, `Timestamp`).
3. **API como contrato único:** el panel web y los conectores e-commerce consumen la misma API que el agente desktop.
4. **E-commerce como módulo:** mismo modelo de licencia que Compras/Informes — cobrable por separado.

---

## 4. Fases de implementación

| Fase | Entregable | Dependencias |
|------|------------|--------------|
| **0 — Diseño** | ADR, modelo de datos cloud, contrato API OpenAPI, plan de migración licencias | Ninguna |
| **1 — API + tenant** | Auth JWT, CRUD productos/clientes/stock read, multi-tenant básico | Fase 0 |
| **2 — Agente sync** | Servicio Windows: push ventas/stock, pull catálogo y precios | Fase 1 |
| **3 — Panel web MVP** | Login, dashboard ventas, listado productos/stock, usuarios | Fase 1 |
| **4 — E-commerce (1 canal)** | Conector Tienda Nube **o** Mercado Libre: stock + importación pedidos | Fases 1–2 |
| **5 — Pulido comercial** | Licencias cloud, facturación SaaS, monitoreo, documentación cliente | Fases 1–4 |

**Orden sugerido para MVP comercial:** 0 → 1 → 2 → 3 (panel básico) → 4 (un solo marketplace según encuesta de clientes).

---

## 5. Costos de implementación y operación

Solo gastos para **poner en marcha y mantener** la solución cloud/e-commerce: infraestructura, dominios, servicios de terceros y planes de las plataformas de venta online. **No incluye** honorarios de programación ni consultoría.

Cifras en **USD** salvo donde se indique **ARS**. Revisar calculadoras oficiales antes de comprometer presupuesto.

### 5.1 Puesta en marcha (pago único o primer año)

| Concepto | Cuándo aplica | Rango estimado |
|----------|---------------|----------------|
| Dominio (`.com` / `.com.ar`) | API + panel web | USD 10 – 25/año |
| Certificado SSL | Si no usa el managed del hosting | USD 0 – 50/año (Let's Encrypt = gratis) |
| Cuenta Azure / AWS / GCP | Alta sin costo; facturación por uso | USD 0 |
| DNS (Cloudflare, etc.) | Opcional | USD 0 – 20/mes |
| Cuenta desarrollador Tienda Nube / ML | Integración e-commerce | **Gratis** |
| App de terceros en Tienda Nube | Si se usa un conector ya hecho en lugar de propio | USD 0 – 30/mes según app |

**Total arranque mínimo (solo cloud, sin e-commerce):** **USD 10 – 75** el primer año (dominio + SSL básico).

### 5.2 Infraestructura cloud (mensual — quien opera SCHPOS)

Estimaciones para **Azure** (AWS/GCP comparables ±15 %).

#### Por escala de comercios con sync activo

| Escenario | Comercios (tenants) | Qué se despliega | USD/mes |
|-----------|---------------------|------------------|---------|
| **Piloto** | 1–10 | App Service B1, SQL Basic, storage mínimo | **80 – 150** |
| **Crecimiento** | 10–50 | App Service S1, SQL Standard S0, App Insights, Blob | **150 – 350** |
| **Producción media** | 50–200 | App Service S2+, SQL S1–S2, Redis, CDN | **350 – 800** |
| **Escala** | 200+ | Múltiples instancias, SQL elastic pool, WAF | **800 – 2.500+** |

#### Desglose típico — escenario “Crecimiento” (~30 comercios)

| Servicio | Configuración referencia | USD/mes |
|----------|-------------------------|---------|
| Azure App Service (API + panel) | S1 Linux, 1 instancia | 55 – 75 |
| Azure SQL Database | S0 (10 DTU) o vCore mínimo | 30 – 90 |
| Azure Blob Storage | PDF, backups ligeros | 5 – 15 |
| Application Insights | Logs y métricas | 10 – 40 |
| Azure Functions (webhooks e-commerce) | Consumo bajo | 0 – 20 |
| Dominio + SSL managed | Incluido en App Service o aparte | 0 – 15 |
| **Subtotal infra producción** | | **~100 – 250** |

#### Operación adicional (opcional pero recomendable)

| Concepto | USD/mes |
|----------|---------|
| Ambiente de pruebas (staging) | 40 – 100 |
| Backups geo-redundantes | 10 – 40 |
| Monitoreo/alertas (PagerDuty, UptimeRobot Pro, etc.) | 0 – 50 |
| Email transaccional (SendGrid, Mailgun) | 0 – 20 |

**Regla práctica para SCHPOS como operador del cloud:**

| Alcance | USD/mes total (infra + extras) |
|---------|--------------------------------|
| Piloto (1–10 clientes) | **80 – 200** |
| Operación comercial inicial (10–50 clientes) | **150 – 450** |
| Con staging permanente | sumar **40 – 150** |

### 5.3 Costo en el comercio (cliente final — sin cambios de hardware)

El POS desktop y el agente de sync corren en el **mismo PC Windows** que hoy. No hace falta servidor nuevo en el local.

| Concepto | Costo adicional para el comercio |
|----------|-----------------------------------|
| PC / caja existente | **USD 0** (requisitos actuales de SCHPOS) |
| Internet estable | Lo que ya paga el comercio |
| Módulo cloud SCHPOS (si se cobra) | A definir en licencia — ver §5.5 |
| Tienda Nube / ML / WooCommerce | Ver §5.4 si vende online |

### 5.4 Plataformas e-commerce (paga cada comercio que venda online)

SCHPOS no paga royalty por usar las APIs. El costo lo asume el comerciante según el canal.

#### Tienda Nube (Argentina, referencia 2026)

| Plan | Precio aprox. | Integración con SCHPOS |
|------|---------------|------------------------|
| Esencial | ~ARS 8.000 – 15.000/mes | API en planes que la incluyan |
| A medida / Avanzado | ~ARS 15.000 – 35.000/mes | Webhooks, multi-depósito según plan |
| Acceso API / OAuth por tienda | — | **Sin costo extra** de licencia API |

Verificar precios en [tiendanube.com](https://www.tiendanube.com).

#### Mercado Libre

| Concepto | Costo |
|----------|-------|
| Publicar y vender | Comisión por venta (~13–16 % según categoría) |
| Mercado Envíos / publicidad | Variable |
| API Developers | **Gratis** |

#### WooCommerce (autohospedado)

| Concepto | Costo |
|----------|-------|
| Plugin WooCommerce | Gratis |
| Hosting + dominio | USD 10 – 40/mes |
| SSL | Gratis (Let's Encrypt) o incluido en hosting |

### 5.5 Resumen: cuánto sale implementar esto

#### A) Solo cloud (sync + panel) — operado por SCHPOS

| Ítem | Una vez | Mensual |
|------|---------|---------|
| Dominio + SSL | USD 10 – 75/año | — |
| Infra Azure (piloto) | — | **USD 80 – 200** |
| Infra Azure (10–50 comercios) | — | **USD 150 – 450** |
| Staging (opcional) | — | **USD 40 – 150** |

**Ejemplo piloto con 5 comercios:** ~**USD 100 – 200/mes** de infra + **USD 10 – 25/año** de dominio.

#### B) Cloud + un canal e-commerce (ej. Tienda Nube)

| Ítem | Quién paga | Mensual |
|------|------------|---------|
| Infra SCHPOS (escenario crecimiento) | SCHPOS | **USD 150 – 350** |
| Plan Tienda Nube | Comercio | **~ARS 8.000 – 35.000** |
| API Tienda Nube | — | **USD 0** |

#### C) Por comercio con tienda online (vista del cliente)

| Concepto | Rango mensual |
|----------|---------------|
| Solo POS + sync cloud (si se cobra módulo) | A definir en licencia |
| + Tienda Nube | + ~ARS 8.000 – 35.000 |
| + Mercado Libre | Comisión por venta (sin cuota fija de API) |
| + WooCommerce | + USD 10 – 40 hosting |

### 5.6 Punto de equilibrio infra (solo gastos, sin desarrollo)

Ejemplo: **30 comercios** con módulo cloud a **USD 25/mes** cada uno → **USD 750/mes** de ingreso recurrente.

| Gasto | USD/mes |
|-------|---------|
| Infra Azure (crecimiento) | ~200 – 350 |
| Staging + backups + email | ~50 – 100 |
| **Margen bruto antes de soporte humano** | **~300 – 500** |

*El soporte al cliente (llamadas, capacitación) no está cuantificado aquí porque depende de cómo lo organicen ustedes.*

---

## 6. Modelo comercial orientativo (ingreso vs gasto infra)

Referencia para decidir cuánto cobrar el módulo cloud; no es un costo de implementación.

| Producto | Precio sugerido al comercio | Gasto infra asociado (SCHPOS) |
|----------|----------------------------|-------------------------------|
| **SCHPOS desktop** (actual) | Licencia existente | — |
| **Módulo Cloud Sync + panel** | USD 15 – 40/mes por sucursal | Repartido entre todos los tenants (~USD 3 – 15/sucursal según escala) |
| **Conector e-commerce** | USD 20 – 50/mes o incluido en plan superior | Sin costo API; misma infra que cloud |

---

## 7. Riesgos y mitigaciones

| Riesgo | Mitigación |
|--------|------------|
| Sync corrupto / stock negativo | Cola de eventos, idempotencia, reconciliación nocturna |
| Cliente no quiere depender de internet | Offline-first; cloud como valor agregado, no requisito para vender |
| Cambios API TN/ML | Abstraer conector detrás de interfaz; tests de contrato |
| Soporte multiplicado | Panel de admin interno: ver último sync, logs, reintentos |
| Competencia con POS cloud puros | Vender “lo mejor de los dos mundos”: velocidad local + visibilidad cloud |

---

## 8. Prerrequisitos antes de empezar Fase 0

- [ ] Merge y release **v2.2** estable en `main` (PR #12)
- [ ] Pruebas Windows completas (`GUIA_PRUEBAS_AVANCES.md`)
- [ ] Acuerdo con socio: si el MVP incluye e-commerce o solo cloud
- [ ] Definir si multi-sucursal es requisito del MVP o fase 2
- [ ] Elegir stack API: **.NET 8** (alineado al equipo) vs alternativas
- [ ] Encuesta a 5–10 clientes: ¿Tienda Nube, ML o ninguno?

---

## 9. Cronograma indicativo

```
v2.2 merge ──► Fase 0 (diseño) ──► Fase 1 (API)
                                        │
                    ┌───────────────────┼───────────────────┐
                    ▼                   ▼                   ▼
              Fase 2 (sync)      Fase 3 (panel)     (opcional paralelo)
                    │                   │
                    └─────────┬─────────┘
                              ▼
                        Fase 4 (e-commerce)
                              ▼
                        Fase 5 (comercial)
                              ▼
                         SCHPOS v3.0
```

---

## 10. Decisión recomendada

1. **Corto plazo:** cerrar v2.2 en desktop (sin bloquear por cloud).
2. **Mediano plazo:** invertir en **Fases 0–2** (diseño + API + sync) como **v3.0-alpha** con 3–5 clientes piloto.
3. **E-commerce:** lanzar **un solo conector** según demanda (Tienda Nube suele ser el más pedido en retail AR).
4. **No hacer aún:** reescribir el POS en Blazor/WebAssembly ni app móvil nativa — ROI bajo frente a panel + sync.

---

## Referencias internas

- `docs/avances/ROADMAP.md` — sprints v2.2 (completados)
- `docs/avances/MERGE_A_MAIN.md` — integración a main
- `docs/Guia_MercadoPago_Point.md` — integración MP existente (referencia de patrón API)
- `SchettiniGestion/DatabaseService.cs` — modelo de datos actual
- `SchettiniGestion/LicenseManager.cs` — licenciamiento a evolucionar

## Referencias externas (precios e APIs)

- [Azure Pricing Calculator](https://azure.microsoft.com/pricing/calculator/)
- [Tienda Nube — Documentación API](https://tiendanube.github.io/api-documentation/)
- [Mercado Libre — Developers](https://developers.mercadolibre.com/)

---

*Documento preparado para evaluación estratégica. Los rangos de costo son estimaciones de infraestructura y servicios; revisar calculadoras oficiales antes de comprometer.*
