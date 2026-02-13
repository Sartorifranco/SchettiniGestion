# Schettini Gestion

Sistema de facturación y stock en C# .NET (WPF), con integración AFIP, Mercado Pago y control de permisos por licencia.

## Requisitos

- **.NET Framework 4.7.2** (o superior)
- **SQL Server** (Express o estándar), con instancia accesible
- **Visual Studio 2019/2022** (o SDK de compilación) para compilar

## Estructura del repositorio

- **SchettiniGestion** – Biblioteca principal: acceso a datos (SQL Server), licencias, sesión de usuario
- **SchettiniGestion.WPF** – Aplicación de escritorio (interfaz WPF)
- **LicenseGenerator** – Herramienta/utilidad para generación de licencias

## Configuración

### 1. Base de datos

- Crear la base de datos **SchPosDB** en SQL Server (mediante scripts o el método que utilicen).
- En el proyecto **SchettiniGestion.WPF**, editar **App.config** y ajustar la cadena de conexión:

```xml
<connectionStrings>
  <add name="SchPosDB"
       connectionString="Data Source=SU_SERVIDOR\SU_INSTANCIA;Initial Catalog=SchPosDB;Integrated Security=True;TrustServerCertificate=True;"
       providerName="System.Data.SqlClient" />
</connectionStrings>
```

- Reemplazar `SU_SERVIDOR\SU_INSTANCIA` por el nombre de su servidor e instancia (por ejemplo `localhost\SQLEXPRESS`).

### 2. Licencia

La aplicación puede obtener la licencia de dos formas:

- **Archivo:** Colocar un archivo (por defecto `licencia.key`) en la misma carpeta que el ejecutable, con el contenido de la licencia en Base64.
- **App.config:** En **SchettiniGestion.WPF**, sección `<appSettings>`:
  - **RutaLicencia:** ruta del archivo de licencia (por defecto `licencia.key`).
  - **LicenciaBase64:** opcional; si se define, se usa esta cadena Base64 en lugar del archivo.

Sin licencia válida o con licencia expirada, la aplicación no iniciará.

## Compilación

1. Abrir **SchettiniGestion.sln** en Visual Studio.
2. Restaurar paquetes NuGet (clic derecho en la solución → Restaurar paquetes NuGet).
3. Compilar en modo Debug o Release (por ejemplo **Compilar → Compilar solución**).

El ejecutable se generará en `SchettiniGestion.WPF\bin\Debug` o `SchettiniGestion.WPF\bin\Release`.

## Módulos (según licencia y permisos)

- Inicio / Dashboard  
- Facturación  
- Ventas, reportes, presupuestos  
- Caja, cuentas corrientes, precios, listas de precios  
- Compras, stock, productos, clientes, proveedores  
- Usuarios, permisos, configuración  

## Licencia del proyecto

Consulte el repositorio y los archivos de licencia del proyecto para condiciones de uso y distribución.
