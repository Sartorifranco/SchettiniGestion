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

| Fase | Entregable | Dependencias | Duración estimada (1 dev) |
|------|------------|--------------|---------------------------|
| **0 — Diseño** | ADR, modelo de datos cloud, contrato API OpenAPI, plan de migración licencias | Ninguna | 2–3 semanas |
| **1 — API + tenant** | Auth JWT, CRUD productos/clientes/stock read, multi-tenant básico | Fase 0 | 6–10 semanas |
| **2 — Agente sync** | Servicio Windows: push ventas/stock, pull catálogo y precios | Fase 1 | 8–12 semanas |
| **3 — Panel web MVP** | Login, dashboard ventas, listado productos/stock, usuarios | Fase 1 | 6–8 semanas |
| **4 — E-commerce (1 canal)** | Conector Tienda Nube **o** Mercado Libre: stock + importación pedidos | Fases 1–2 | 6–10 semanas |
| **5 — Pulido comercial** | Licencias cloud, facturación SaaS, monitoreo, documentación cliente | Fases 1–4 | 4–6 semanas |

Las fases 3 y 4 pueden solaparse parcialmente si hay dos desarrolladores.

**Orden sugerido para MVP comercial:** 0 → 1 → 2 → 3 (panel básico) → 4 (un solo marketplace según encuesta de clientes).

---

## 5. Costos de desarrollo (rangos aproximados)

Cifras en **USD**, mercado **Argentina/LATAM** (freelance o equipo chico). Un “mes-persona” ≈ 160 h.

| Fase | Alcance mínimo | Alcance robusto | Notas |
|------|----------------|-----------------|-------|
| **0 — Diseño** | USD 2.000 – 4.000 | USD 5.000 – 8.000 | Incluye prototipo API y diagramas |
| **1 — API + tenant** | USD 8.000 – 15.000 | USD 20.000 – 35.000 | .NET 8, EF Core, tests |
| **2 — Agente sync** | USD 12.000 – 22.000 | USD 30.000 – 45.000 | La parte más delicada técnicamente |
| **3 — Panel web** | USD 6.000 – 12.000 | USD 18.000 – 28.000 | PWA responsive, sin POS web |
| **4 — E-commerce (1 canal)** | USD 5.000 – 10.000 | USD 15.000 – 25.000 | Por plataforma adicional: +40–60 % |
| **5 — Pulido** | USD 3.000 – 6.000 | USD 10.000 – 18.000 | DevOps, docs, onboarding |

### Escenarios totales de desarrollo

| Escenario | Qué incluye | Rango total USD |
|-----------|-------------|-----------------|
| **MVP mínimo** | Fases 0–2 + panel muy básico (solo lectura) | **USD 35.000 – 55.000** |
| **MVP comercial** | Fases 0–3 + un conector e-commerce | **USD 55.000 – 90.000** |
| **Producto completo v3.0** | Fases 0–5 + 2 conectores (TN + ML) | **USD 90.000 – 150.000** |

*Comparación:* reescribir todo el POS como app web desde cero suele costar **USD 150.000 – 300.000+** y demora más — por eso se recomienda evolución incremental.

### Costo de mantenimiento post-lanzamiento

| Concepto | Mensual USD (estimado) |
|----------|------------------------|
| Bugfixes y parches API/sync | USD 1.500 – 4.000 |
| Soporte N2 cloud (horas incluidas) | USD 500 – 2.000 |
| Actualizaciones e-commerce (cambios de API TN/ML) | USD 300 – 1.000 |

---

## 6. Costos de infraestructura cloud (mensuales)

Estimaciones para **Azure** (AWS/GCP son comparables ±15 %). Tipo de cambio y precios cambian — revisar calculadora oficial antes de comprometer.

### Por escala de clientes activos (tenants con sync)

| Escala | Tenants | Arquitectura | USD/mes infra |
|--------|---------|--------------|---------------|
| **Piloto** | 1–10 | App Service B1, SQL Basic, sin CDN | **USD 80 – 150** |
| **Crecimiento** | 10–50 | App Service S1, SQL Standard S0, Storage, App Insights | **USD 150 – 350** |
| **Producción media** | 50–200 | App Service S2+, SQL S1–S2, Redis cache, CDN | **USD 350 – 800** |
| **Escala** | 200+ | Kubernetes o múltiples instancias, SQL elastic pool, WAF | **USD 800 – 2.500+** |

### Desglose típico (escenario “Crecimiento”, ~30 tenants)

| Servicio | Configuración referencia | USD/mes |
|----------|-------------------------|---------|
| Azure App Service (API) | S1 Linux, 1 instancia | 55 – 75 |
| Azure SQL Database | S0 (10 DTU) o vCore mínimo | 30 – 90 |
| Azure Blob Storage | PDF, backups ligeros | 5 – 15 |
| Application Insights | Logs y métricas | 10 – 40 |
| Dominio + certificado SSL | Let's Encrypt o App Service managed | 0 – 15 |
| Azure Functions (webhooks e-commerce) | Consumo bajo | 0 – 20 |
| **Subtotal infra** | | **~USD 100 – 250** |

### Costos adicionales de operación

| Concepto | USD/mes |
|----------|---------|
| Monitoreo/alertas (PagerDuty, etc.) | 0 – 50 |
| Backups geo-redundantes | 10 – 40 |
| Ambiente staging (réplica chica) | 40 – 100 |
| Email transaccional (SendGrid, etc.) | 0 – 20 |

**Regla práctica:** presupuestar **USD 100 – 400/mes** de infra para los primeros 12–24 meses comerciales, más **USD 50 – 150/mes** por ambiente de pruebas si se mantiene staging permanente.

---

## 7. Costos e-commerce para el comerciante (no desarrollo)

Estos costos los paga **cada cliente** que quiera vender online; SCHPOS solo integra vía API.

### Tienda Nube (Argentina, referencia 2026)

| Plan | Precio aprox. | API / integración |
|------|---------------|-------------------|
| Esencial | ~ARS 8.000 – 15.000/mes | API REST disponible en planes superiores |
| A medida / Avanzado | ~ARS 15.000 – 35.000/mes | Webhooks, multi-depósito según plan |
| **Costo desarrollo API TN** | **Gratis** (documentación pública) | OAuth por tienda |

*Nota:* verificar precios actuales en [tiendanube.com](https://www.tiendanube.com) — varían por promociones.

### Mercado Libre

| Concepto | Costo |
|----------|-------|
| Publicar y vender | Comisión por venta (~13–16 % según categoría) |
| Mercado Envíos / publicidad | Variable |
| **API Developers** | Sin costo de licencia API |
| Integración SCHPOS | Desarrollo one-time (Fase 4) |

### WooCommerce (autohospedado)

| Concepto | Costo |
|----------|-------|
| Plugin WooCommerce | Gratis |
| Hosting + dominio | USD 10 – 40/mes |
| SSL, mantenimiento | Variable |
| Integración | REST API estándar; más trabajo de instalación por cliente |

### Resumen para el socio

- **No hay royalty** a pagar a Tienda Nube o ML por usar su API.
- El **ingreso recurrente** para SCHPOS sería: **módulo cloud + módulo e-commerce** en la licencia (ej. fee mensual por tenant o % sobre plan cloud propio).

---

## 8. Modelo de negocio sugerido (SaaS + módulos)

| Producto | Precio sugerido al cliente (orientativo) | Costo nuestro |
|----------|------------------------------------------|---------------|
| **SCHPOS desktop** (actual) | Licencia perpetua / anual existente | — |
| **Módulo Cloud Sync** | USD 15 – 40/mes por sucursal | Infra repartida entre tenants |
| **Panel web** | Incluido en Cloud o +USD 10/mes | Incluido en API |
| **Conector Tienda Nube** | USD 20 – 50/mes o pago único USD 200–500 | Mantenimiento API |
| **Conector Mercado Libre** | Idem | Idem |

**Punto de equilibrio infra (ejemplo):** con 30 clientes a USD 25/mes en Cloud = USD 750/mes ingreso vs ~USD 200–350/mes infra → margen bruto positivo antes de soporte.

---

## 9. Riesgos y mitigaciones

| Riesgo | Mitigación |
|--------|------------|
| Sync corrupto / stock negativo | Cola de eventos, idempotencia, reconciliación nocturna |
| Cliente no quiere depender de internet | Offline-first; cloud como valor agregado, no requisito para vender |
| Cambios API TN/ML | Abstraer conector detrás de interfaz; tests de contrato |
| Soporte multiplicado | Panel de admin interno: ver último sync, logs, reintentos |
| Competencia con POS cloud puros | Vender “lo mejor de los dos mundos”: velocidad local + visibilidad cloud |

---

## 10. Prerrequisitos antes de empezar Fase 0

- [ ] Merge y release **v2.2** estable en `main` (PR #12)
- [ ] Pruebas Windows completas (`GUIA_PRUEBAS_AVANCES.md`)
- [ ] Acuerdo con socio: presupuesto y si el MVP incluye e-commerce o solo cloud
- [ ] Definir si multi-sucursal es requisito del MVP o fase 2
- [ ] Elegir stack API: **.NET 8** (alineado al equipo) vs alternativas
- [ ] Encuesta a 5–10 clientes: ¿Tienda Nube, ML o ninguno?

---

## 11. Cronograma indicativo (sin fechas de calendario)

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

## 12. Decisión recomendada

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

*Documento preparado para evaluación estratégica. Los rangos de costo son estimaciones; solicitar cotización formal antes de comprometer presupuesto.*
