# Merge de avances → `main` (v2.2.0 propuesta)

Rama lista para merge: **`cursor/avances-v2.2-d53a`**  
**PR:** https://github.com/Sartorifranco/SchettiniGestion/pull/12

Incluye **Sprints 1 a 4** completos sobre `main` v2.1.9.

---

## Qué trae esta rama

| Sprint | Entregable |
|--------|--------------|
| **1** | Menú Compras, Proveedores, Informes; fix órdenes de compra |
| **2** | Factura con recepción opcional + vínculo OC |
| **3** | 4 informes nuevos (valorización, vendedor, faltantes, CC proveedores) |
| **4** | Export PDF, NC/ND impactan saldo proveedor, hub Informes unificado |

**No modifica** el alcance congelado de v2.1.9 en `docs/lanzamiento/` — esos documentos siguen siendo la referencia del build de lanzamiento.

---

## Cómo mergear (GitHub)

1. Abrir el PR de `cursor/avances-v2.2-d53a` → `main`
2. Revisar el diff (principalmente WPF, `DatabaseService`, docs en `docs/avances/`)
3. Ejecutar pruebas en Windows (ver abajo)
4. Merge cuando estén conformes

```bash
git fetch origin
git checkout main
git merge origin/cursor/avances-v2.2-d53a
git push origin main
```

O usar el botón **Merge pull request** en GitHub.

---

## Pruebas recomendadas antes del merge

```powershell
git checkout cursor/avances-v2.2-d53a
msbuild SchettiniGestion.sln /p:Configuration=Release
.\SchettiniGestion.Tester\bin\Release\SchettiniGestion.Tester.exe
```

Checklist manual: `docs/avances/GUIA_PRUEBAS_AVANCES.md`  
Revisión técnica: `docs/avances/REVISION_SPRINTS_1_2_3.md`

---

## Después del merge (publicación)

- [ ] Bump versión a **2.2.0** en proyecto e instalador
- [ ] Regenerar `SCHPOS-Setup-2.2.0.exe`
- [ ] Actualizar licencias clientes que compren Compras/Proveedores
- [ ] Comunicar al socio / clientes piloto

---

## Ramas históricas (opcional)

Los sprints se desarrollaron en ramas incrementales (`cursor/sprint1-*` … `cursor/sprint4-*`).  
**Para mergear a `main` usar solo `cursor/avances-v2.2-d53a`** — las demás pueden cerrarse como referencia.
