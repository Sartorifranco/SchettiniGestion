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
| **2** | Flujo compras: checkbox recepcionar + selector OC en factura | `cursor/sprint2-flujo-compras-d53a` | [#9](https://github.com/Sartorifranco/SchettiniGestion/pull/9) | ✅ Hecho |
| **3** | Informes faltantes: valorización, vendedor, faltantes pedidos, cta.cte. proveedor | `cursor/sprint3-informes-d53a` | [#10](https://github.com/Sartorifranco/SchettiniGestion/pull/10) | ✅ Hecho |
| **4** | Pulido: PDF export, NC/ND impactan saldo, hub Informes unificado | `cursor/sprint4-pulido-d53a` | [#11](https://github.com/Sartorifranco/SchettiniGestion/pull/11) | ✅ Hecho |
| **Merge → main** | Integración Sprints 1–4 (v2.2 propuesta) | `cursor/avances-v2.2-d53a` | [#12](https://github.com/Sartorifranco/SchettiniGestion/pull/12) | ✅ Listo para merge |

## Criterio de “listo para merge a main”

- [ ] Guía de pruebas del sprint completada y ejecutada
- [ ] Sin regresiones en smoke test v2.1.9
- [ ] Informe para socio actualizado
- [ ] Versión bump (ej. 2.2.0) y Setup regenerado
- [ ] Acuerdo explícito con socio antes de publicar

## Pendiente de publicación comercial

- [ ] Actualizar la web de licenciamiento con los módulos **Compras**, **Proveedores** e **Informes**.
- [ ] Incorporar la importación asistida de facturas de compra: PDF con texto y foto mediante OCR local (Tesseract) o Azure.
- [ ] Explicar en la web que Azure usa una cuenta/clave propia de cada comercio y que el cupo/costo pertenece a esa cuenta.
- [ ] Definir si el OCR de compras se incluye en un plan existente o se comercializa como módulo adicional.
