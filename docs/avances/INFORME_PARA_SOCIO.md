# Informe de avances — para el socio

**Borrador vivo** — se actualiza al cerrar cada sprint.  
**Versión de lanzamiento acordada:** SCHPOS v2.1.9 (sin estos avances).  
**Última actualización:** 18 jul 2026

---

## 1. Por qué hay dos líneas de trabajo

| Línea | Qué es | Estado |
|-------|--------|--------|
| **Lanzamiento v2.1.9** | Lo que salimos a vender/probar con clientes ahora | Congelado en `main` |
| **Avances (sprints)** | Mejoras que estamos adelantando sin retrasar el lanzamiento | En ramas separadas |

**Decisión del socio:** lanzar con v2.1.9 para no demorar más.  
**Decisión técnica:** seguir desarrollando Compras e Informes en paralelo, documentado y probado, para una **v2.2.0** (o similar) cuando ustedes den el OK.

---

## 2. Qué tiene el cliente hoy (v2.1.9) — resumen

- POS y facturación operativos
- Productos, stock, clientes, caja, promociones
- Gráficos y estadísticas (módulo adicional)
- AFIP, Mercado Pago (según licencia)
- Instalador y licenciamiento AdminLicencias

**No prometido en v2.1.9:** Compras en menú, Informes tabulares en menú, flujo completo de compras.

Detalle: `docs/lanzamiento/v2.1.9_ALCANCE.md`

---

## 3. Avances completados

### Sprint 1 (18 jul 2026) — «Poner en el menú lo que ya estaba hecho»

**Problema:** Compras, Proveedores e Informes estaban programados pero el usuario no los veía. Parecía que «no existían».

**Solución entregada:**
- Tres ítems nuevos en el menú (con licencia y permisos)
- Compras y Proveedores pasan a ser módulos vendibles en el licenciador
- Órdenes de compra: estados editables y corrección de errores al ver/editar

**Valor para el negocio:**
- Podemos **cobrar** Compras y Proveedores como módulos adicionales cuando decidamos publicar
- Base lista para Sprint 2 (flujo real de compra)

**Riesgo / limitación honesta:**
- La factura de compra **sigue moviendo stock siempre** (igual que antes)
- No hay aún enlace OC ↔ factura ↔ recepción
- Informes sigue siendo la pantalla básica de 5 reportes (no el módulo completo que planificamos)

**Esfuerzo:** 1 sprint corto (visibilidad + fixes), no el módulo Compras terminado.

**PR:** https://github.com/Sartorifranco/SchettiniGestion/pull/8  
**¿Merge a producción?** Pendiente de acuerdo y pruebas (guía en `GUIA_PRUEBAS_AVANCES.md`).

---

## 4. Próximos pasos (plan acordado)

| Sprint | Entregable | Impacto comercial |
|--------|------------|-------------------|
| **2** | Factura de compra con «recepcionar sí/no» y elegir OC | Compras usable en el día a día |
| **3** | Informes: valorización stock, vendedor, faltantes, cta.cte. proveedor | Cierra hueco vs. competencia en reportes |
| **4** | Pulido + un solo menú Informes | Producto más vendible |

Roadmap completo: `docs/avances/ROADMAP.md`

---

## 5. Cuándo publicar una nueva versión

Recomendación: **no mergear sprints a `main`** hasta:

1. Checklist de pruebas del sprint ejecutado
2. Setup `SCHPOS-Setup-2.2.x.exe` generado y probado en PC limpia
3. Acuerdo explícito socio + técnico
4. Comunicación a clientes piloto si cambia licenciamiento

---

## 6. Documentos de soporte

| Audiencia | Documento |
|-----------|-----------|
| Socio / gerencia | Este informe |
| QA / pruebas lanzamiento | `docs/lanzamiento/v2.1.9_GUIA_PRUEBAS.md` |
| QA / pruebas avances | `docs/avances/GUIA_PRUEBAS_AVANCES.md` |
| Bitácora técnica | `docs/avances/REGISTRO_SPRINTS.md` |

---

## 7. Resumen en una frase (para reunión)

> «Salimos con v2.1.9 como acordamos. En paralelo adelantamos Sprint 1: Compras, Proveedores e Informes ya visibles en menú y OC corregidas; el flujo de compras completo viene en Sprint 2. Nada de esto está en el build de lanzamiento hasta que lo aprueben y publiquemos v2.2.»

---

_Este documento se irá ampliando al cerrar Sprint 2, 3 y 4._
