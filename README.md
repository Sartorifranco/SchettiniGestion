# SchettiniGestion

Sistema de gestión comercial WPF (.NET Framework 4.7.2) para facturación, ventas, compras, stock, tesorería e informes.

## Requisitos

- Windows 10/11
- .NET Framework 4.7.2
- SQL Server Express (o SQL Server completo)
- Visual Studio 2022 (para desarrollo)

## Compilar y ejecutar

```powershell
cd c:\SchettiniGestion
& "${env:ProgramFiles}\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe" SchettiniGestion.sln /p:Configuration=Release /p:Platform=x64 /v:minimal

# Ejecutar
Start-Process "SchettiniGestion.WPF\bin\x64\Release\SchettiniGestion.WPF.exe" -WorkingDirectory "SchettiniGestion.WPF\bin\x64\Release"
```

## Estructura del menú

| Sección | Módulos |
|---------|---------|
| **OPERACIONES** | Ventas, Presupuestos, Historial Presup. |
| **TESORERÍA** | Cobranzas, Ingresos y Egresos, Movimientos, Cupones Tarjetas, Caja (Apertura/Cierre/Consulta/Planilla) |
| **GESTIÓN** | Actualizar Precios, Listas de Precios, Compras, Stock |
| **TABLAS** | Productos, Clientes, Proveedores |
| **ADMINISTRACIÓN** | Informes, Usuarios, Permisos, Configuración |

## Módulos implementados

### Informes (Administración > Informes)
- **General:** Estado de resultados, detalle de ventas
- **Clientes:** Cuentas corrientes
- **Ventas:** Reportes de ventas
- **Compra:** Cta cte proveedores, detalle compras, gastos
- **Stock:** Valorización de stock (sin IVA / con IVA)
- **Tesorería:** Detalle de cobros, flujo de caja
- **Contabilidad:** Libro IVA

### Tesorería
- **Movimientos:** Movimientos de caja por rango de fechas
- **Cupones de Tarjetas:** Pantalla informativa
- **Consulta Caja:** Resumen por medio de pago, movimientos del día
- **Planilla Diaria:** Planilla diaria de caja

### Compras
- Facturas, Recepciones, Notas Crédito/Débito, Gastos, Pagos, Órdenes
- **Editar factura:** Botón Editar o doble clic en la grilla

## Pruebas

Ver **[README_CAMBIOS.md](README_CAMBIOS.md)** para:
- Registro detallado de todos los cambios
- Instrucciones de prueba por módulo
- Checklist de pruebas al finalizar

## Instalador (testing / clientes)

```powershell
.\Instalador\build-release.ps1 -BuildInstaller
```

Guía: [Instalador/README-INSTALADOR.md](Instalador/README-INSTALADOR.md). Licencias: `dotnet run --project LicenseGenerator`.
