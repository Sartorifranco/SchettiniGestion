# Roadmap — Avances post v2.1.9

Plan acordado para completar Compras e Informes sin bloquear el lanzamiento.

## Visión

| Área | Objetivo final |
|------|----------------|
| **Compras** | OC → factura con recepción opcional → stock y cuenta corriente proveedor |
| **Informes** | Hub unificado: ventas, compras, stock, IVA, métricas |

## Sprints

| Sprint | Objetivo | Rama | PR | Estado |
|--------|----------|------|-----|--------|
| **1** | Hacer visible Compras, Proveedores e Informes; fix OC básico | `cursor/sprint1-compras-informes-d53a` | [#8](https://github.com/Sartorifranco/SchettiniGestion/pull/8) | ✅ Hecho |
| **2** | Flujo compras: checkbox recepcionar + selector OC en factura | `cursor/sprint2-flujo-compras-d53a` | — | ✅ Hecho |
| **3** | Informes faltantes: valorización, vendedor, faltantes pedidos, cta.cte. proveedor | — | — | ⏳ Pendiente |
| **4** | Pulido: PDF export, NC/ND impactan saldo, hub Informes unificado | — | — | ⏳ Pendiente |

## Criterio de “listo para merge a main”

- [ ] Guía de pruebas del sprint completada y ejecutada
- [ ] Sin regresiones en smoke test v2.1.9
- [ ] Informe para socio actualizado
- [ ] Versión bump (ej. 2.2.0) y Setup regenerado
- [ ] Acuerdo explícito con socio antes de publicar
