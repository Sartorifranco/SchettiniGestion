# Guía: conectar SCHPOS en red (multiestación)

SCHPOS en red funciona con **una PC SERVIDOR** (donde viven los datos en SQL Server) y **una o más PCs CLIENTE** que se conectan a esa misma base. No se comparte una carpeta: se comparte la base `SchPosDB`.

---

## Requisitos previos

1. **Licencia con el extra «Conexión en RED»** (`ACCESO_RED`) en todas las PCs que vayan a usar modo red.
2. **Misma red LAN** (Wi‑Fi o cable) entre el servidor y los clientes.
3. En la **PC SERVIDOR**: **SQL Server Express** (gratis). LocalDB (el que instala el Setup por defecto) **no sirve** para multiestación.  
   Descarga: https://aka.ms/sqlexpress
4. Anotar la **IP de la PC servidor** (ej. `192.168.1.100`). Conviene IP fija.

---

## Parte A — Configurar la PC SERVIDOR

### 1. Instalar SCHPOS

Instalá con el Setup (`Siguiente → Siguiente → Listo`) y activá la licencia **con Conexión en RED**.

### 2. Instalar SQL Server Express

En esa misma PC, instalá SQL Server Express. Después de instalarlo, reiniciá o volvé a abrir SCHPOS.

### 3. Elegir modo SERVIDOR

Al abrir SCHPOS (pantalla de configuración inicial), elegí:

> **Esta PC es el SERVIDOR de la red**

Si ya estaba configurado solo en esta PC:

- Andá a **Administración → Configuración → pestaña «Red y Servidor»**
- Elegí **SERVIDOR (Principal)** y guardá (la app se reinicia).

### 4. Instancia SQL

- Instancia típica: `.\SQLEXPRESS`
- Podés usar **Detectar** si no estás seguro.
- Presioná **Probar conexión** → si OK, **Continuar**.

### 5. Qué hace SCHPOS automáticamente (en primer uso como servidor)

- Intenta habilitar **TCP/IP** en SQL Server.
- Abre el firewall con la regla **`SCHPOS-SQL-1433`** (puerto **1433**).
- Genera en el Escritorio el archivo **`SCHPOS-Configuracion-Clientes.txt`** con la IP y los pasos para los clientes.

**Importante:** compartí ese archivo `.txt` con quien configure las otras PCs.

### 6. Login

Ingresá con el usuario del sistema (por defecto suele ser `admin` tras el bootstrap). Ese mismo usuario va a servir en los clientes.

---

## Parte B — Configurar cada PC CLIENTE

### 1. Instalar SCHPOS

Instalá SCHPOS en la PC cliente y activá la licencia **con Conexión en RED**.

### 2. Elegir modo CLIENTE

En la configuración inicial elegí:

> **Conectarme a otro servidor (soy cliente)**

O desde **Configuración → Red y Servidor → CLIENTE (Puesto de Red)**.

### 3. Datos de conexión

Usá lo que dice el archivo del servidor. Ejemplos:

| Campo | Ejemplo |
|--------|---------|
| IP / servidor | `192.168.1.100` **o** `192.168.1.100\SQLEXPRESS` |
| Puerto | `1433` (si usás `IP\SQLEXPRESS`, a veces conviene **dejar el puerto vacío**) |

### 4. Autenticación

- **Windows (sin contraseña):** si las PCs están en el mismo dominio/grupo de trabajo.
- **Usuario y contraseña SQL:** si Windows no alcanza (usuario `sa` u otro usuario SQL del servidor).

### 5. Probar y continuar

1. **Probar conexión**
2. Si funciona → **Continuar** / Guardar (reinicia)
3. Login con el mismo usuario que en el servidor (`admin`, etc.)

---

## Parte C — Cómo verificar que quedó bien

1. Abrí SCHPOS en **servidor** y en **cliente**.
2. En una PC cargá un producto / hacé una venta.
3. En la otra PC debería verse el mismo stock / historial (misma base).

---

## Si el asistente se traba en «Preparar datos»

Eso no es un problema de SCHPOS en sí: SQL Server sigue en **solo Windows**. El login `schpos` entonces falla (`Login failed for user 'schpos'`).

En la PC servidor, **una sola instancia** Express (`SQLEXPRESS`). Después de que SCHPOS pida el UAC:

1. Reiniciá el servicio **SQL Server (SQLEXPRESS)** (parar y arrancar; el modo mixto no aplica si no).
2. Comprobá: `sqlcmd -S ".\SQLEXPRESS" -E -Q "SELECT SERVERPROPERTY('IsIntegratedSecurityOnly')"` → tiene que dar **0**.
3. Recién ahí **Preparar datos**, **una vez**.
4. Si ya falló antes: borrá el login `schpos` y reintentá una sola vez.
5. Las otras PCs usan `SCHPOS-Configuracion-Clientes.txt` (usuario `schpos` y la clave de ese archivo).

---

## Si no conecta (checklist rápido)

En la **PC servidor**:

- [ ] SQL Server Express está **en ejecución** (servicios de Windows).
- [ ] **TCP/IP** habilitado: SQL Server Configuration Manager → Protocolos → TCP/IP = Habilitado.
- [ ] Firewall: regla **`SCHPOS-SQL-1433`** en **Permitir** (o abrir TCP 1433 a mano).
- [ ] La IP que usan los clientes es la correcta (ping desde el cliente).

En la **PC cliente**:

- [ ] Licencia con **Conexión en RED**.
- [ ] IP correcta (`192.168.x.x` o `192.168.x.x\SQLEXPRESS`).
- [ ] Si es instancia nombrada y falla con puerto 1433 → probar **puerto vacío**.
- [ ] Auth Windows o SQL según cómo esté el servidor.

---

## Resumen

**Servidor** = Express + licencia RED + modo SERVIDOR → compartir el `.txt` del Escritorio.  
**Cliente** = instalar + licencia RED + modo CLIENTE + IP del servidor → Probar conexión → listo.

---

¿Problemas? Soporte: info@schettini.com.ar
