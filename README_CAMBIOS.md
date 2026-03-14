# Registro de Cambios - SchettiniGestion

Este documento registra todos los cambios realizados en el sistema y cómo probarlos.

---

## Cambios realizados

### 1. Correcciones de estabilidad (GeneradorLicencias y DatabaseService)

**Archivos modificados:**
- `GeneradorLicencias/Program.cs`
- `SchettiniGestion/DatabaseService.cs`
- `SchettiniGestion.WPF/PrincipalWindow.xaml.cs`

**Qué se cambió:**
- **GeneradorLicencias:** Protección ante `null` cuando el usuario presiona Enter sin escribir en el campo Hardware ID (`Console.ReadLine()?.Trim() ?? ""`).
- **DatabaseService:** Eliminado warning CS0168 (variable `ex` no usada en bloque catch).
- **PrincipalWindow:** Comentario actualizado de "Dashboard" a "Inicio".

**Cómo probar:**
- GeneradorLicencias: Ejecutar `GeneradorLicencias.exe`, en "ID de Hardware" presionar Enter sin escribir → no debe crashear.
- DatabaseService: Compilar sin warnings.
- PrincipalWindow: Solo cambio de comentario, sin impacto funcional.

---

### 2. Búsqueda de clientes por CUIT en AFIP (Padrón)

**Archivos modificados:**
- `SchettiniGestion.WPF/AfipService.cs`
- `SchettiniGestion.WPF/ClientesControl.xaml.cs`

**Qué se cambió:**
- Nuevo método `ObtenerPersonaPorCuitAsync()` en AfipService que consulta el padrón AFIP (ws_sr_padron_a4).
- Al presionar **Enter** en el campo CUIT de Gestión Clientes, se consulta AFIP y se autocompletan Razón Social y Condición IVA.
- ComboBox de Condición IVA precargado con opciones: Consumidor Final, Responsable Inscripto, Monotributo, Exento.

**Requisitos (usted debe configurar):**
1. Ir a **Configuración > Negocio y AFIP**.
2. Completar el **CUIT de la empresa** (ej: 20-38646611-5 o 20386466115).
3. Seleccionar el **Certificado digital (.pfx)** de AFIP y su contraseña.
4. En AFIP (afip.gob.ar), tener el servicio **ws_sr_padron_a4** autorizado para el CUIT de la empresa.

Si falta el CUIT o el certificado, aparecerá un mensaje claro indicando qué configurar.

**Cómo probar:**
1. Ir a **Clientes**.
2. Ingresar un CUIT válido (ej: `20385043539`).
3. Presionar **Enter**.
4. Verificar que se completen Razón Social y Condición IVA automáticamente.
5. Si hay error (certificado, servicio no autorizado), se mostrará mensaje descriptivo.

---

### 3. Condición IVA visible al cargar cliente (AFIP o desde grilla)

**Archivos modificados:**
- `SchettiniGestion.WPF/ClientesControl.xaml.cs`

**Qué se cambió:**
- El ComboBox de Condición IVA no mostraba el valor al asignar `Text` (comportamiento de WPF).
- Se creó el método `EstablecerCondicionIVA()` que usa `SelectedItem` para seleccionar correctamente el ítem.
- Si el valor viene de AFIP o de la grilla y no existe en la lista, se agrega y se selecciona.
- Se aplica tanto al cargar desde AFIP (Enter en CUIT) como al seleccionar un cliente en la grilla.

**Cómo probar:**
1. Cargar cliente por AFIP (CUIT + Enter) → verificar que Condición IVA se muestre en el dropdown.
2. Seleccionar un cliente existente en la grilla → verificar que Condición IVA se muestre.

---

### 4. ComboBox Condición IVA: texto visible (negro sobre blanco)

**Archivos modificados:**
- `SchettiniGestion.WPF/App.xaml` (ModuleComboBoxStyle)

**Qué se cambió:**
- El ComboBox tenía texto blanco sobre fondo blanco (hereda del tema oscuro), por lo que no se veía.
- Se agregó `Foreground="#1E293B"` y `Background="White"` al ModuleComboBoxStyle.
- Se agregó ItemContainerStyle para que los ítems del dropdown también tengan texto oscuro sobre fondo blanco.
- Afecta a todos los ComboBox que usan ModuleComboBoxStyle (Clientes, Productos, Facturación, etc.).

**Cómo probar:**
- Verificar que en cualquier ComboBox (ej: Condición IVA en Clientes) el texto sea legible (oscuro sobre fondo claro).

---

### 5. Configuración Red/Servidor: autenticación Windows y credenciales ocultas

**Archivos modificados:**
- `SchettiniGestion.WPF/ConfirguracionControl.xaml`
- `SchettiniGestion.WPF/ConfirguracionControl.xaml.cs`
- `SchettiniGestion/DatabaseService.cs`
- `Instalador/SchettiniGestion.iss` (nuevo)

**Qué se cambió:**
- **Autenticación Windows automática:** Checkbox "Usar autenticación Windows" - cuando está marcado, no se requieren Usuario/Contraseña SQL (usa la sesión de Windows).
- **Credenciales SQL ocultas:** Los campos Usuario SQL y Contraseña SQL solo son visibles para el **Administrador** (RolID=1). Los demás usuarios no los ven.
- **Soporte instancias nombradas:** Se puede usar `.\SQLEXPRESS` sin puerto.
- **Instalador:** Script Inno Setup en `Instalador/SchettiniGestion.iss` para generar Setup.exe en lugar de distribuir el .exe suelto.

**Cómo probar:**
1. Como Administrador: Configuración > Red y Servidor → desmarcar "Usar Windows" → ver Usuario/Contraseña SQL.
2. Como usuario no-admin: Los campos de credenciales no deben verse.
3. Con "Usar Windows" marcado: Guardar conexión sin ingresar usuario/contraseña.

**Generar instalador:**
- **¿Quién instala qué?** El DESARROLLADOR necesita Inno Setup (gratuito) para generar el Setup.exe. El USUARIO FINAL solo ejecuta el Setup.exe, no necesita instalar nada extra.
- Pasos: (1) Compilar Release x64, (2) Instalar Inno Setup desde https://jrsoftware.org/isinfo.php, (3) Abrir `Instalador/SchettiniGestion.iss` y presionar F9, (4) El Setup.exe queda en `OutputInstalador/`.

---

### 6. Instalación más fácil para el usuario + mensaje de error mejorado

**Archivos modificados:**
- `Instalador/SchettiniGestion.iss`
- `Instalador/RequisitosPreInstalacion.txt` (nuevo)
- `SchettiniGestion.WPF/App.xaml.cs`

**Qué se cambió:**
- **Pantalla de requisitos en el instalador:** Antes de instalar, el usuario ve los requisitos (Windows, .NET, SQL Server Express) con enlace de descarga.
- **RequisitosPreInstalacion.txt** se copia a la carpeta de instalación para consulta.
- **Mensaje de error mejorado:** Si falla la conexión a la base de datos, se muestra un mensaje claro con pasos para solucionar (instalar SQL Express, ir a Configuración, etc.).

**Cómo probar:**
- Simular error de conexión (ej: detener SQL Server) → verificar que el mensaje sea útil.
- Generar instalador → verificar que se muestre la pantalla de requisitos antes de instalar.

---

### 7. Proveedores: campos formales completos

**Archivos modificados:**
- `SchettiniGestion/DatabaseService.cs`
- `SchettiniGestion.WPF/ProveedoresControl.xaml`
- `SchettiniGestion.WPF/ProveedoresControl.xaml.cs`
- `SchettiniGestion.WPF/App.xaml.cs`

**Qué se cambió:**
- Nuevos campos: CUIT/CUIL, Razón Social, Domicilio, **Categoría Fiscal** (RI, Mono, Exento, Cons final), **Persona de contacto**, Teléfono, **Página web**, Email.
- Migración BD para columnas CategoriaFiscal, PersonaContacto, PaginaWeb.
- Grilla muestra Cat. Fiscal y Contacto.

**Cómo probar:** Ir a Proveedores → crear/editar proveedor → verificar todos los campos.

---

### 8. Roles, Permisos y Usuarios conectados + Cerrar sesión vs Salir

**Archivos modificados:**
- `SchettiniGestion/DatabaseService.cs`
- `SchettiniGestion/SesionUsuario.cs`
- `SchettiniGestion.WPF/PrincipalWindow.xaml`
- `SchettiniGestion.WPF/PrincipalWindow.xaml.cs`
- `SchettiniGestion.WPF/GestionPermisos.xaml.cs`

**Qué se cambió:**
- **Roles y permisos por módulo:** Migración que inserta todos los permisos (ACCESO_TOTAL, ACCESO_USUARIOS, ACCESO_CLIENTES, etc.) y crea roles Administrador, Encargado y Vendedor con permisos por defecto.
- **Usuarios conectados a roles:** Al iniciar sesión, los permisos se cargan desde `Roles_Permisos` según el `RolID` del usuario. Los usuarios solo ven los módulos permitidos por su rol.
- **Header dinámico:** La barra superior muestra el nombre de usuario y rol real (ej: "pepe" / "Vendedor • En línea").
- **Cerrar Sesión:** Vuelve a la pantalla de login sin cerrar la aplicación. Permite cambiar de usuario sin reiniciar.
- **Salir del Sistema:** Nuevo botón que cierra completamente la aplicación (con confirmación).

**Cómo probar:**
1. Crear usuario "pepe" con rol Vendedor en Usuarios.
2. Asignar permisos al rol Vendedor en Permisos (solo los que correspondan).
3. Cerrar sesión → debe volver al login.
4. Iniciar con "pepe" → debe ver solo los módulos permitidos y el header debe mostrar "pepe" y "Vendedor".
5. Cerrar sesión de nuevo → login. Iniciar con admin → ver todo.
6. Probar "Salir del Sistema" → debe cerrar la app por completo.

---

### 9. Clientes: Condición IVA, datos completos y cuenta corriente

**Archivos modificados:**
- `SchettiniGestion/DatabaseService.cs`
- `SchettiniGestion.WPF/ClientesControl.xaml`
- `SchettiniGestion.WPF/ClientesControl.xaml.cs`
- `SchettiniGestion.WPF/App.xaml.cs`

**Qué se cambió:**
- **Condición IVA:** ComboBox con ítems fijos en XAML (RI, Mono, Exento, Cons final) para que funcione correctamente.
- **CUIT/CUIL:** Enter para buscar AFIP (si falla, carga manual).
- **Datos de contacto:** Teléfono, Domicilio, Email.
- **Cuenta corriente:** Checkbox "Permite cuenta corriente" y campo "Monto límite".
- Migración BD: columnas PermiteCuentaCorriente, MontoLimiteCtaCte.

**Cómo probar:** Clientes → CUIT + Enter (AFIP) o carga manual → Condición IVA visible → guardar con todos los datos.

---

### 10. Filtro de búsqueda en Clientes y Proveedores

**Archivos modificados:**
- `SchettiniGestion.WPF/ClientesControl.xaml`
- `SchettiniGestion.WPF/ClientesControl.xaml.cs`
- `SchettiniGestion.WPF/ProveedoresControl.xaml`
- `SchettiniGestion.WPF/ProveedoresControl.xaml.cs`

**Qué se cambió:**
- **Clientes:** Caja de búsqueda sobre la grilla para filtrar por CUIT o Razón Social.
- **Proveedores:** Misma funcionalidad de filtro.

**Cómo probar:** Escribir en el campo de búsqueda → la lista se filtra en tiempo real.

---

### 11. Productos: modal crear/editar/duplicar + filtro avanzado

**Archivos modificados:**
- `SchettiniGestion/DatabaseService.cs`
- `SchettiniGestion.WPF/ProductosControl.xaml`
- `SchettiniGestion.WPF/ProductosControl.xaml.cs`
- `SchettiniGestion.WPF/ProductoModalWindow.xaml` (nuevo)
- `SchettiniGestion.WPF/ProductoModalWindow.xaml.cs` (nuevo)
- `SchettiniGestion.WPF/App.xaml.cs`

**Qué se cambió:**
- **Panel izquierdo eliminado:** El formulario de producto ya no está a la izquierda.
- **Modal:** Botón "➕ Nuevo Producto" abre un modal con todos los campos (código, descripción, rubro, sub-rubro, marca, proveedor, precios, imagen).
- **Editar:** Doble clic o clic derecho > Editar en un producto de la lista abre el mismo modal con los datos cargados.
- **Duplicar:** Clic derecho > Duplicar abre el modal con los datos copiados (código en blanco para guardar como nuevo).
- **Filtro de búsqueda:** Barra sobre la lista para buscar por Código, Código barras, Descripción, Rubro, SubRubro, Marca, Proveedor.
- **Nuevas columnas BD:** SubRubro, Marca, Proveedor en Productos.

**Cómo probar:**
1. Productos → "Nuevo Producto" → completar y guardar.
2. Doble clic en un producto → editar y guardar.
3. Clic derecho > Duplicar → guardar como nuevo.
4. Escribir en el filtro → ver resultados filtrados.

---

## Cambios pendientes

- **Generar el instalador (Setup.exe):** Se hará cuando se terminen todos los cambios. El script está en `Instalador/SchettiniGestion.iss`.

---

### 12. Modal Producto completo (Nuevo Producto)

**Archivos modificados:**
- `SchettiniGestion/DatabaseService.cs`
- `SchettiniGestion.WPF/ProductoModalWindow.xaml`
- `SchettiniGestion.WPF/ProductoModalWindow.xaml.cs`
- `SchettiniGestion.WPF/ConfirguracionControl.xaml` y `.xaml.cs`

**Qué se cambió:**
- **Datos:** Imagen, Código, Producto (descripción), Rubro, SubRubro, Código de barra + checkbox "Generar automático si no tiene" (EAN-13).
- **Valores:** Tipo moneda (Pesos/USD), % IVA, Costo compra, checkbox "Permitir modificar precio en venta final", precio base con % ganancia.
- **Listas de precio:** Checkbox por lista para asignar al producto, muestra precio final CON IVA en $ y USD (tipo de cambio en Configuración > Negocio).
- **Stock:** Checkbox stockeable, acepta stock negativo, variantes (color/talle/unidad), producto simple o combo (componentes: código:cantidad), cant. disponible/mínima/ideal.
- **Datos adicionales:** Proveedor, Código externo.
- **Botones:** Crear, Crear y agregar otro, Volver atrás.
- **Configuración:** Campo "Tipo de cambio USD" en pestaña Negocio y AFIP.

**Cómo probar:**
1. Configuración > Negocio > cargar Tipo de cambio USD (ej: 1050).
2. Productos > Nuevo Producto > completar secciones y guardar.
3. Probar "Crear y agregar otro" para cargar varios seguidos.
4. Editar producto existente y verificar que se carguen listas, variantes y combo.

---

### 13. Stock: menú desglosado en Consulta stock

**Archivos modificados:**
- `SchettiniGestion/DatabaseService.cs`
- `SchettiniGestion.WPF/StockControl.xaml`
- `SchettiniGestion.WPF/StockControl.xaml.cs`

**Qué se cambió:**
- **Stock** ahora tiene pestañas: **Consulta stock** (con 3 sub-pestañas) y **Registrar movimiento**.
- **Stock General:** Grilla con stock real, reservado y disponible. Filtro por código, producto, talle, color, código externo, marca, rubro, subrubro, proveedor.
- **Movimientos:** Historial con filtros: fecha desde/hasta, codigo/producto/talle/color/etc., tipo de movimiento (checkboxes: Compra, Recepcion Compra, Nota Crédito, Nota Débito, Ajuste Stock, Ingreso, Egreso, Venta).
- **Depósitos:** Vista por depósito (general) con filtros: código, producto, rubro, subrubro, proveedor, talle, color, tipo stock (sin stock, distinto a cero, bajo stock).
- **Registrar movimiento:** Formulario original para ingresar/egresar stock manualmente.
- **Migración BD:** Columna `StockReservado` en Productos (para pedidos futuros).

**Cómo probar:**
1. Gestión > Stock.
2. Consulta stock > Stock General > Buscar con filtro.
3. Consulta stock > Movimientos > Fecha desde/hasta, marcar tipos, Buscar.
4. Consulta stock > Depósitos > Tipo stock, Buscar.
5. Registrar movimiento > cargar producto, cantidad, motivo, Guardar.

---

### 14. Stock Reservado y Ajuste Stock

**Archivos modificados:**
- `SchettiniGestion/DatabaseService.cs`
- `SchettiniGestion.WPF/StockControl.xaml` y `.xaml.cs`
- `SchettiniGestion.WPF/ReservaModalWindow.xaml` (nuevo) y `.xaml.cs` (nuevo)

**Qué se cambió:**
- **Stock Reservado:** Nueva pestaña bajo Consulta stock con filtros (canal, fecha desde/hasta, ID orden, ID publicación, estado, código/producto/talle/color). Botón "Nueva reserva" abre modal para crear reservas manuales. Botón "Anular reserva" con advertencia: el stock vuelve a disponible; NO elimina factura/ticket, se debe generar la NC correspondiente.
- **Ajuste Stock:** Nueva pestaña con filtros (fecha desde/hasta, código/producto, personal). KPIs: Ingresos, Egresos, Valorización sin IVA, Valorización con IVA (según costo). Botón "Nuevo ajuste" lleva a Registrar movimiento. Botón "Anular ajuste" revierte el movimiento.
- **BD:** Tabla `ReservasStock`, columnas `Usuario`, `DepositoID`, `Anulado` en `MovimientosStock`.

**Cómo probar:**
1. Stock > Consulta stock > Stock Reservado > Nueva reserva > crear una > Buscar.
2. Anular reserva > confirmar advertencia.
3. Stock > Consulta stock > Ajuste Stock > Buscar > ver KPIs y valorización.
4. Registrar movimiento > guardar ajuste > volver a Ajuste Stock y ver el movimiento.
5. Anular ajuste desde Ajuste Stock.

---

### 15. Módulo Compras completo - 6 pestañas implementadas

**Archivos modificados/creados:**
- `SchettiniGestion/DatabaseService.cs` - Migraciones y métodos CRUD para todas las entidades
- `SchettiniGestion.WPF/ComprasControl.xaml` y `.xaml.cs` - Rediseño con 6 pestañas
- `SchettiniGestion.WPF/CompraModalWindow.xaml` y `.xaml.cs` - Modal para facturas de compra
- `SchettiniGestion.WPF/RecepcionCompraModalWindow.xaml` y `.xaml.cs` (nuevo)
- `SchettiniGestion.WPF/NotaCreditoDebitoModalWindow.xaml` y `.xaml.cs` (nuevo)
- `SchettiniGestion.WPF/GastoRapidoModalWindow.xaml` y `.xaml.cs` (nuevo)
- `SchettiniGestion.WPF/PagoProveedorModalWindow.xaml` y `.xaml.cs` (nuevo)
- `SchettiniGestion.WPF/OrdenCompraModalWindow.xaml` y `.xaml.cs` (nuevo)

**Qué se cambió:**
- **Facturas de Compras:** Listado con filtro, botón "Nueva Factura" abre modal (proveedor, tipo, condición, productos, carrito). **Editar** abre el mismo modal con datos cargados; al guardar se revierte la compra anterior y se crea una nueva. Eliminar revierte stock y movimientos.
- **Recepción de Compras:** Registro de recepciones vinculadas a compras. Estado (Recibido Total/Parcial), observaciones. CRUD completo.
- **Notas Crédito/Débito Compras:** Notas de crédito o débito a proveedores. Proveedor, tipo, monto, nº comprobante, motivo. CRUD completo.
- **Gastos Rápidos:** Gastos varios con concepto, monto, categoría, proveedor opcional, comprobante. CRUD completo.
- **Pagos:** Pagos a proveedores que actualizan saldo, cuenta corriente y caja. Forma de pago (Efectivo, Transferencia, Cheque, Tarjeta). CRUD completo.
- **Órdenes de Compras:** Órdenes con proveedor, estado (Pendiente/Recibida/Anulada), productos y precios estimados. CRUD completo.

**Tablas BD nuevas:** RecepcionesCompra, NotasCreditoDebitoCompras, GastosRapidos, PagosProveedores, OrdenesCompra, OrdenCompraDetalle.

**Cómo probar:**
1. Gestión > Compras.
2. En cada pestaña: usar filtro de búsqueda, botones Nuevo/Editar/Eliminar.
3. Facturas: Nueva Factura > completar y finalizar.
4. Recepciones: Nueva > seleccionar compra existente.
5. Notas: Nueva > proveedor, tipo, monto.
6. Gastos: Nuevo > concepto, monto, categoría.
7. Pagos: Nuevo > proveedor, monto (actualiza saldo y caja).
8. Órdenes: Nueva > proveedor, agregar productos, guardar.
9. Facturas: Seleccionar compra > Editar (o doble clic) > modificar y guardar.

---

### 16. Módulo Informes - 5 pestañas implementadas

**Archivos creados/modificados:**
- `SchettiniGestion/DatabaseService.cs` - Métodos: GetEstadoResultados, GetDetalleCobros, GetValorizacionStock, GetProveedoresConSaldo, GetVentasParaLibroIVA, GetMovimientosCajaRango
- `SchettiniGestion.WPF/InformeGeneralControl.xaml` y `.xaml.cs` (nuevo)
- `SchettiniGestion.WPF/InformeCompraControl.xaml` y `.xaml.cs` (nuevo)
- `SchettiniGestion.WPF/InformeStockControl.xaml` y `.xaml.cs` (nuevo)
- `SchettiniGestion.WPF/InformeTesoreriaControl.xaml` y `.xaml.cs` (nuevo)
- `SchettiniGestion.WPF/InformeContabilidadControl.xaml` y `.xaml.cs` (nuevo)
- `SchettiniGestion.WPF/InformesControl.xaml.cs` - Carga los nuevos controles

**Qué se implementó:**
- **General:** Estado de resultados (ventas, costos, gastos, resultado neto) y detalle de ventas por rango de fechas.
- **Compra:** Cuenta corriente con proveedores (saldos), detalle de compras y gastos.
- **Stock:** Valorización de stock (sin IVA, con IVA, totales).
- **Tesorería:** Detalle de cobros (factura, cliente, medio de pago) y flujo de caja (movimientos por rango).
- **Contabilidad:** Libro IVA (ventas con datos fiscales).

**Cómo probar:**
1. Administración > Informes.
2. General: elegir fechas > Buscar > ver estado de resultados y detalle ventas.
3. Compra: fechas > Buscar > ver cta cte proveedores, compras y gastos.
4. Stock: Buscar > ver valorización.
5. Tesorería: fechas > Buscar > ver cobros y flujo caja.
6. Contabilidad: fechas > Buscar > ver libro IVA.

---

### 17. Módulo Tesorería - 4 sub-módulos implementados

**Archivos creados/modificados:**
- `SchettiniGestion.WPF/MovimientosCajaControl.xaml` y `.xaml.cs` (nuevo)
- `SchettiniGestion.WPF/CuponesTarjetasControl.xaml` y `.xaml.cs` (nuevo)
- `SchettiniGestion.WPF/ConsultaCajaControl.xaml` y `.xaml.cs` (nuevo)
- `SchettiniGestion.WPF/PlanillaDiariaControl.xaml` y `.xaml.cs` (nuevo)
- `SchettiniGestion.WPF/PrincipalWindow.xaml.cs` - Enlaces a los controles

**Qué se implementó:**
- **Movimientos:** Movimientos de caja por rango de fechas con saldo calculado.
- **Cupones de Tarjetas:** Pantalla informativa (para carga de cupones se requiere tabla CuponesTarjeta).
- **Consulta Caja:** Resumen por medio de pago (tipo cierre Z) y movimientos del día.
- **Planilla Diaria:** Planilla diaria de movimientos de caja.

**Cómo probar:**
1. Tesorería > Movimientos > elegir fechas > Buscar.
2. Tesorería > Cupones de Tarjetas > ver pantalla informativa.
3. Tesorería > Caja - Consulta Apertura/Cierre > elegir fecha > Buscar.
4. Tesorería > Caja - Planilla Diaria > elegir fecha > Buscar.

---

### 18. Editar Factura de Compra

**Archivos modificados:**
- `SchettiniGestion/DatabaseService.cs` - Método GetCompraFueContado
- `SchettiniGestion.WPF/CompraModalWindow.xaml.cs` - Constructor con compraId, CargarCompraParaEditar
- `SchettiniGestion.WPF/ComprasControl.xaml.cs` - btnEditarFacturaCompra y doble clic abren modal de edición

**Qué se cambió:**
- Al editar una compra se abre el modal con datos cargados (proveedor, tipo, condición, productos).
- Al guardar se elimina la compra anterior (revirtiendo stock y movimientos) y se crea una nueva.
- Doble clic en la grilla también abre la edición.

**Cómo probar:**
1. Gestión > Compras > Facturas de Compras.
2. Seleccionar una compra > Editar (o doble clic).
3. Modificar productos, cantidades o proveedor > Finalizar compra.
4. Verificar que la compra se actualizó correctamente.

---

## Resumen: Instalador (para el desarrollador)

> **NOTA:** El paso de generar el instalador (Setup.exe) se realizará **cuando se terminen todos los cambios** del sistema. El script y la documentación ya están listos en `Instalador/`.

| Pregunta | Respuesta |
|----------|-----------|
| ¿Tengo que instalar algo para generar el Setup.exe? | Sí: **Inno Setup** (gratuito). Descarga: https://jrsoftware.org/isinfo.php |
| ¿El usuario final necesita instalar algo? | Solo el **Setup.exe** que vos generás. Recomendable que tenga SQL Server Express (gratuito) para la base de datos. |
| ¿Cómo genero el instalador? | Compilar → Abrir `Instalador/SchettiniGestion.iss` en Inno Setup → F9 |
| ¿Dónde queda el Setup.exe? | En `OutputInstalador/SchettiniGestion_Setup_1.0.exe` |

---

## Cómo compilar y ejecutar

```powershell
# Compilar (desde la carpeta del proyecto)
cd c:\SchettiniGestion
& "${env:ProgramFiles}\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe" SchettiniGestion.sln /p:Configuration=Release /p:Platform=x64 /v:minimal

# Ejecutar la aplicación
Start-Process "c:\SchettiniGestion\SchettiniGestion.WPF\bin\x64\Release\SchettiniGestion.WPF.exe" -WorkingDirectory "c:\SchettiniGestion\SchettiniGestion.WPF\bin\x64\Release"
```

---

## Checklist de pruebas (al finalizar todos los cambios)

- [ ] GeneradorLicencias: Enter sin Hardware ID no crashea
- [ ] Clientes: Búsqueda por CUIT en AFIP funciona
- [ ] Clientes: Condición IVA visible al cargar desde AFIP
- [ ] Clientes: Condición IVA visible al seleccionar cliente en la grilla
- [ ] ComboBox: texto legible (negro sobre blanco) en todos los módulos
- [ ] Config: Autenticación Windows y credenciales ocultas para no-admin
- [ ] Instalador: Generar Setup.exe con Inno Setup
- [ ] Instalador: Pantalla de requisitos y mensaje de error mejorado
- [ ] Roles/Permisos: Usuarios respetan permisos del rol asignado
- [ ] Cerrar Sesión: Vuelve al login sin cerrar la app
- [ ] Salir del Sistema: Cierra la aplicación por completo
- [ ] Header: Muestra usuario y rol real (no "Administrador" fijo)
- [ ] Proveedores: Campos completos (Categoría fiscal, Persona contacto, Página web)
- [ ] Clientes: Condición IVA funciona, datos contacto, checkbox cta cte
- [ ] Clientes/Proveedores: Filtro de búsqueda por CUIT o Razón Social
- [ ] Productos: Modal crear/editar/duplicar, filtro por código/descripción/rubro/marca/proveedor
- [ ] Compras: 6 pestañas (Facturas, Recepciones, Notas Crédito/Débito, Gastos Rápidos, Pagos, Órdenes) con CRUD y filtros
- [ ] Compras: Editar factura de compra (botón Editar o doble clic)
- [ ] Informes: General, Compra, Stock, Tesorería, Contabilidad con datos reales
- [ ] Tesorería: Movimientos, Cupones Tarjetas, Consulta Caja, Planilla Diaria
