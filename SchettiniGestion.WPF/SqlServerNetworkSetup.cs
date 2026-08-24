using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Microsoft.Win32;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using SchettiniGestion;

namespace SchettiniGestion.WPF
{
    /// <summary>Resultado de preparar la PC como servidor de red SCHPOS.</summary>
    public sealed class RedServidorResultado
    {
        public bool Ok { get; set; }
        public string Mensaje { get; set; }
        public string Ip { get; set; }
        public string ServidorClientes { get; set; }
        public string UsuarioSql { get; set; }
        public string PasswordSql { get; set; }
        public string RutaGuia { get; set; }
        public bool MigracionHecha { get; set; }
        public List<string> Checklist { get; } = new List<string>();
    }

    /// <summary>
    /// Prepara SQL Server Express para multiestación: Express, migración LocalDB,
    /// TCP/firewall 1433, login SQL para clientes y guía en Escritorio.
    /// </summary>
    internal static class SqlServerNetworkSetup
    {
        public const string UsuarioRedDefault = "schpos";
        public const string InstanciaLocal = @".\SQLEXPRESS";

        public static string RutaCredencialesClientes => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "SCHPOS", "red_clientes.cfg");

        public static string RutaFlagOfertaRed => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "SCHPOS", "red_oferta_hecha.flag");

        /// <summary>Persiste el rol de esta PC: Servidor | Cliente (no depender solo de parsear Data Source).</summary>
        public static string RutaModoRed => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "SCHPOS", "modo_red.cfg");

        public const string ModoServidor = "Servidor";
        public const string ModoCliente = "Cliente";

        public static void GuardarModoRed(string modo)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(modo)) return;
                Directory.CreateDirectory(Path.GetDirectoryName(RutaModoRed));
                File.WriteAllText(RutaModoRed, modo.Trim(), Encoding.UTF8);
            }
            catch { }
        }

        public static string LeerModoRed()
        {
            try
            {
                if (!File.Exists(RutaModoRed)) return "";
                string m = (File.ReadAllText(RutaModoRed) ?? "").Trim();
                if (m.Equals(ModoServidor, StringComparison.OrdinalIgnoreCase)) return ModoServidor;
                if (m.Equals(ModoCliente, StringComparison.OrdinalIgnoreCase)) return ModoCliente;
            }
            catch { }
            return "";
        }

        public static bool EsModoCliente() =>
            LeerModoRed().Equals(ModoCliente, StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Pipeline completo: Express → TCP/UAC → migrar LocalDB → login SQL → guía → conexion.cfg local.
        /// </summary>
        public static RedServidorResultado ConfigurarComoServidor(Action<string> progreso = null)
        {
            var r = new RedServidorResultado();
            try
            {
                progreso?.Invoke("1/6 · Verificando SQL Server Express...");
                // Solo cuenta si realmente conecta a .\SQLEXPRESS (no alcanza con el servicio a medias).
                if (!SqlExpressInstaller.PuedeConectarExpress())
                {
                    progreso?.Invoke("SQL Express no está listo. Descargando/instalando...");
                    string errInst = SqlExpressInstaller.InstalarSilencioso(progreso);
                    if (errInst != null)
                    {
                        r.Mensaje = errInst +
                            "\n\nTip: en Configuración → Red podés usar «Instalar SQL Express» " +
                            "o bajar Express desde microsoft.com (instancia SQLEXPRESS) y reintentar.";
                        r.Checklist.Add("✗ SQL Express no disponible");
                        return r;
                    }
                }
                if (!SqlExpressInstaller.ServicioEnEjecucion())
                {
                    r.Mensaje = "SQL Express está instalado pero el servicio no arranca (MSSQL$SQLEXPRESS).\n" +
                                "Reiniciá la PC y reintentá desde Configuración → Red.";
                    r.Checklist.Add("✗ Servicio SQLEXPRESS detenido");
                    return r;
                }
                r.Checklist.Add("✓ SQL Express en ejecución");

                progreso?.Invoke("2/6 · Habilitando TCP/IP puerto 1433 y firewall (administrador)...");
                // Sin MessageBox previo: el asistente ya explicó el UAC. Solo el escudo de Windows.
                ElevacionHelper.PedirConfirmacionUac = false;
                string errTcp;
                try { errTcp = HabilitarTcpYFirewall(InstanciaLocal); }
                finally { ElevacionHelper.PedirConfirmacionUac = true; }
                if (errTcp != null)
                {
                    r.Mensaje = errTcp;
                    r.Checklist.Add("✗ TCP/firewall (UAC cancelado o falló)");
                    return r;
                }
                r.Checklist.Add("✓ TCP 1433 + firewall SCHPOS-SQL-1433");

                string avisoInst = AdvertenciaInstanciasExtra();
                if (!string.IsNullOrEmpty(avisoInst))
                    r.Checklist.Add("⚠ " + avisoInst);

                progreso?.Invoke("3/6 · Aplicando modo mixto (SQL + Windows) y reciclando SQLEXPRESS...");
                string errMixto = AsegurarModoMixtoAplicado(progreso);
                if (errMixto != null)
                {
                    r.Mensaje = errMixto;
                    r.Checklist.Add("✗ Modo mixto (SoloWindows sigue en 1)");
                    return r;
                }
                r.Checklist.Add("✓ Modo mixto aplicado (SoloWindows = 0)");

                progreso?.Invoke("4/6 · Migrando datos desde LocalDB (si aplica)...");
                string errMig = MigrarLocalDbHaciaExpress(out bool migro);
                if (errMig != null)
                {
                    r.Mensaje = "Falló la migración LocalDB → Express:\n" + errMig;
                    r.Checklist.Add("✗ Migración LocalDB");
                    return r;
                }
                r.MigracionHecha = migro;
                r.Checklist.Add(migro ? "✓ Datos migrados LocalDB → Express" : "✓ Express listo (sin migración necesaria)");

                progreso?.Invoke("5/6 · Creando usuario SQL para las otras PCs...");
                string pass;
                string errLogin = AsegurarLoginRed(out pass);
                if (errLogin != null)
                {
                    r.Mensaje = "No se pudo crear el usuario SQL de red:\n" + errLogin;
                    r.Checklist.Add("✗ Login SQL schpos");
                    return r;
                }
                r.UsuarioSql = UsuarioRedDefault;
                r.PasswordSql = pass;
                r.Checklist.Add("✓ Usuario SQL «schpos» para clientes");

                progreso?.Invoke("6/6 · Verificando conexión local y TCP...");
                string errPrueba;
                if (!DatabaseService.ProbarNuevaConexion(InstanciaLocal, "", true, null, null, out errPrueba))
                {
                    r.Mensaje = "Express no acepta conexión local (Shared Memory):\n" + errPrueba;
                    r.Checklist.Add("✗ Prueba local Express");
                    return r;
                }
                r.Checklist.Add("✓ Conexión local a Express OK");

                string avisoTcp;
                bool tcpOk = AsegurarYProbarTcpSqlAuth(UsuarioRedDefault, pass, out avisoTcp, progreso);
                if (!tcpOk)
                {
                    r.Checklist.Add("✗ TCP 1433 no responde — servidor de red NO listo");
                    r.Mensaje =
                        "No se marca esta PC como servidor de red: el puerto TCP 1433 no responde.\n\n" +
                        (avisoTcp ?? "Nadie escucha en 1433.") + "\n\n" +
                        "Express puede seguir usándose en esta PC, pero las otras estaciones no van a conectar.\n" +
                        "Reintentá el asistente y aceptá el Sí de Windows (UAC).";
                    return r;
                }
                r.Checklist.Add("✓ TCP 1433 + login schpos OK");

                progreso?.Invoke("Guardando conexión y guía para clientes...");
                if (!DatabaseService.GuardarNuevaConexion(InstanciaLocal, "", true, null, null))
                {
                    r.Mensaje = "No se pudo escribir conexion.cfg en ProgramData\\SCHPOS.";
                    return r;
                }

                r.Ip = ObtenerIPRed();
                r.ServidorClientes = r.Ip + "\\SQLEXPRESS";
                GuardarCredencialesClientes(r.ServidorClientes, "1433", r.UsuarioSql, r.PasswordSql);
                r.RutaGuia = GenerarArchivoClientes(InstanciaLocal, r.UsuarioSql, r.PasswordSql, r.Ip, r.ServidorClientes);
                r.Checklist.Add("✓ Guía en Escritorio + credenciales guardadas");

                GuardarModoRed(ModoServidor);
                r.Ok = true;
                r.Mensaje =
                    "Esta PC quedó como SERVIDOR de red.\n\n" +
                    "IP: " + r.Ip + "\n" +
                    "Para clientes: " + r.ServidorClientes + " · puerto 1433\n" +
                    "Usuario SQL: " + r.UsuarioSql + "\n" +
                    "Contraseña: " + r.PasswordSql + "\n\n" +
                    "Guía: " + (r.RutaGuia ?? "(Escritorio)") +
                    (r.MigracionHecha ? "\n\nSe migraron los datos desde LocalDB." : "");
                return r;
            }
            catch (Exception ex)
            {
                r.Ok = false;
                r.Mensaje = ex.Message;
                return r;
            }
        }

        /// <summary>
        /// PowerShell para elevar. No usar Sysnative: con Verb=runas da error 3 (ruta no encontrada).
        /// </summary>
        internal static string RutaPowerShell64() => ElevacionHelper.RutaPowerShellParaElevar();

        /// <summary>
        /// Habilita TCP/firewall/modo mixto. Retorna null si OK; mensaje si UAC cancelado o script falló.
        /// </summary>
        public static string HabilitarTcpYFirewall(string instanciaSql)
        {
            try
            {
                string instancia = string.IsNullOrWhiteSpace(instanciaSql) ? InstanciaLocal : instanciaSql.Trim();
                string nombreServicio = ResolverNombreServicio(instancia);
                string logPath = Path.Combine(Path.GetTempPath(), "schpos_tcp_" + Process.GetCurrentProcess().Id + ".log");
                try { if (File.Exists(logPath)) File.Delete(logPath); } catch { }

                // Script 64-bit: busca instancias por InstalledInstances, Instance Names y servicios MSSQL$*
                string script = $@"
$ErrorActionPreference = 'Continue'
$log = '{logPath.Replace("'", "''")}'
function W($m) {{ Add-Content -Path $log -Value $m }}
try {{
  $regBase = 'HKLM:\SOFTWARE\Microsoft\Microsoft SQL Server'
  $instList = New-Object System.Collections.Generic.List[string]

  $installed = (Get-ItemProperty $regBase -ErrorAction SilentlyContinue).InstalledInstances
  if ($installed) {{ foreach ($i in @($installed)) {{ [void]$instList.Add([string]$i) }} }}

  $namesKey = Get-ItemProperty ""$regBase\Instance Names\SQL"" -ErrorAction SilentlyContinue
  if ($namesKey) {{
    $namesKey.PSObject.Properties | Where-Object {{ $_.Name -notlike 'PS*' }} | ForEach-Object {{
      if (-not [string]::IsNullOrWhiteSpace($_.Name) -and -not $instList.Contains($_.Name)) {{ [void]$instList.Add($_.Name) }}
    }}
  }}

  if ($instList.Count -eq 0) {{
    Get-Service -Name 'MSSQL*' -ErrorAction SilentlyContinue | ForEach-Object {{
      if ($_.Name -eq 'MSSQLSERVER') {{ [void]$instList.Add('MSSQLSERVER') }}
      elseif ($_.Name -like 'MSSQL$*') {{ [void]$instList.Add($_.Name.Substring(6)) }}
    }}
  }}

  W ('INSTANCES:' + ($instList -join ','))
  if ($instList.Count -eq 0) {{ W 'NO_INSTANCES'; exit 2 }}

  foreach ($i in $instList) {{
    $keyName = $null
    if ($i -eq 'MSSQLSERVER') {{
      $keyName = (Get-ItemProperty ""$regBase\Instance Names\SQL"" -ErrorAction SilentlyContinue).MSSQLSERVER
    }} else {{
      $keyName = (Get-ItemProperty ""$regBase\Instance Names\SQL"" -ErrorAction SilentlyContinue).$i
    }}
    if (-not $keyName) {{
      # Buscar carpeta MSSQL*.INSTANCIA en el registro
      Get-ChildItem $regBase -ErrorAction SilentlyContinue | ForEach-Object {{
        if ($_.PSChildName -like (""MSSQL*.$i"")) {{ $keyName = $_.PSChildName }}
      }}
    }}
    if (-not $keyName) {{ W ""SKIP_NO_KEY:$i""; continue }}

                $tcpPath = ""$regBase\$keyName\MSSQLServer\SuperSocketNetLib\Tcp""
    $tcpHive = ""HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Microsoft SQL Server\$keyName\MSSQLServer\SuperSocketNetLib\Tcp""
    if (Test-Path $tcpPath) {{
      # DWORD explícito: Set-ItemProperty a veces no deja Enabled=1 y TCP nunca abre
      [Microsoft.Win32.Registry]::SetValue($tcpHive, 'Enabled', 1, [Microsoft.Win32.RegistryValueKind]::DWord)
      Get-ChildItem $tcpPath -ErrorAction SilentlyContinue | Where-Object {{ $_.PSChildName -like 'IP*' }} | ForEach-Object {{
        $subHive = ""$tcpHive\$($_.PSChildName)""
        [Microsoft.Win32.Registry]::SetValue($subHive, 'Enabled', 1, [Microsoft.Win32.RegistryValueKind]::DWord)
        [Microsoft.Win32.Registry]::SetValue($subHive, 'Active', 1, [Microsoft.Win32.RegistryValueKind]::DWord)
        if ($_.PSChildName -eq 'IPAll') {{
          [Microsoft.Win32.Registry]::SetValue($subHive, 'TcpDynamicPorts', '', [Microsoft.Win32.RegistryValueKind]::String)
          [Microsoft.Win32.Registry]::SetValue($subHive, 'TcpPort', '1433', [Microsoft.Win32.RegistryValueKind]::String)
        }} else {{
          [Microsoft.Win32.Registry]::SetValue($subHive, 'TcpDynamicPorts', '', [Microsoft.Win32.RegistryValueKind]::String)
          [Microsoft.Win32.Registry]::SetValue($subHive, 'TcpPort', '', [Microsoft.Win32.RegistryValueKind]::String)
        }}
      }}
      $en = (Get-ItemProperty $tcpPath -ErrorAction SilentlyContinue).Enabled
      W (""TCP_OK:$i:Enabled=$en"")
      if ([int]$en -ne 1) {{ W ""TCP_ENABLED_FAIL:$i""; exit 5 }}
    }} else {{ W ""TCP_PATH_MISSING:$i"" }}

    $loginModePath = ""$regBase\$keyName\MSSQLServer""
    $loginHive = ""HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Microsoft SQL Server\$keyName\MSSQLServer""
    if (Test-Path $loginModePath) {{
      [Microsoft.Win32.Registry]::SetValue($loginHive, 'LoginMode', 2, [Microsoft.Win32.RegistryValueKind]::DWord)
      W ""MIXED_OK:$i""
    }}
  }}

  $ruleName = 'SCHPOS-SQL-1433'
  $existe = netsh advfirewall firewall show rule name=$ruleName 2>&1 | Out-String
  if ($existe -match 'No rules match' -or $existe -match 'Ninguna regla') {{
    netsh advfirewall firewall add rule name=$ruleName protocol=TCP dir=in action=allow localport=1433 profile=any | Out-Null
  }}
  W 'FIREWALL_OK'

  # Browser ayuda a resolver instancias nombradas (opcional, no bloqueante)
  $browser = Get-Service -Name 'SQLBrowser' -ErrorAction SilentlyContinue
  if ($browser -ne $null) {{
    Set-Service -Name 'SQLBrowser' -StartupType Automatic -ErrorAction SilentlyContinue
    Start-Service -Name 'SQLBrowser' -ErrorAction SilentlyContinue
    W 'BROWSER_OK'
  }}

  $extras = Get-Service -Name 'MSSQL$*' -ErrorAction SilentlyContinue |
    Where-Object {{ $_.Name -ne 'MSSQL$SQLEXPRESS' }}
  if ($extras) {{ W ('EXTRA_INSTANCES:' + (($extras | ForEach-Object {{ $_.Name }}) -join ',')) }}

  # Siempre SQLEXPRESS (el de 1433). Reciclar otra instancia (SQLEXPRESS01) deja el modo mixto sin aplicar.
  $svcName = 'MSSQL$SQLEXPRESS'
  $svc = Get-Service -Name $svcName -ErrorAction SilentlyContinue
  if ($svc -eq $null -and '{nombreServicio}' -ne $svcName) {{
    $svc = Get-Service -Name '{nombreServicio}' -ErrorAction SilentlyContinue
    if ($svc -ne $null) {{ $svcName = $svc.Name }}
  }}
  if ($svc -eq $null) {{
    W 'SERVICE_MISSING_SQLEXPRESS'
    exit 3
  }}

  # Parar y arrancar (no Restart-Service): LoginMode=2 no rige si el proceso no muere del todo.
  Stop-Service -Name $svcName -Force -ErrorAction SilentlyContinue
  for ($i = 0; $i -lt 50; $i++) {{
    $svc.Refresh()
    if ($svc.Status -eq 'Stopped') {{ break }}
    Start-Sleep -Milliseconds 400
  }}
  $svc.Refresh()
  if ($svc.Status -ne 'Stopped') {{
    & sc.exe stop $svcName | Out-Null
    Start-Sleep -Seconds 4
    $svc.Refresh()
  }}
  W ('SERVICE_STOPPED:' + $svcName + ':' + $svc.Status)
  Start-Service -Name $svcName -ErrorAction SilentlyContinue
  for ($i = 0; $i -lt 50; $i++) {{
    $svc.Refresh()
    if ($svc.Status -eq 'Running') {{ break }}
    Start-Sleep -Milliseconds 400
  }}
  $svc.Refresh()
  if ($svc.Status -ne 'Running') {{
    Start-Service -Name $svcName -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 4
    $svc.Refresh()
  }}
  W ('SERVICE_STARTED:' + $svcName + ':' + $svc.Status)

  # No salir OK si nadie escucha en 1433 (evita marcar servidor listo en falso)
  $listenOk = $false
  for ($t = 0; $t -lt 40; $t++) {{
    try {{
      $client = New-Object System.Net.Sockets.TcpClient
      $ar = $client.BeginConnect([System.Net.IPAddress]::Loopback, 1433, $null, $null)
      if ($ar.AsyncWaitHandle.WaitOne(1000)) {{
        $client.EndConnect($ar)
        $listenOk = $true
        $client.Close()
        break
      }}
      $client.Close()
    }} catch {{ }}
    Start-Sleep -Milliseconds 500
  }}
  if (-not $listenOk) {{
    W 'LISTEN_FAIL_1433'
    exit 4
  }}
  W 'LISTEN_OK_1433'
  W 'DONE'
  exit 0
}} catch {{
  W ('ERR:' + $_.Exception.Message)
  exit 1
}}
";
                string tmpScript = Path.Combine(Path.GetTempPath(), "schpos_enable_tcp.ps1");
                File.WriteAllText(tmpScript, script, Encoding.UTF8);

                // null => ElevacionHelper elige PowerShell 64-bit sin Sysnative (evita error 3).
                string args = $"-NoProfile -ExecutionPolicy Bypass -File \"{tmpScript}\"";
                string errElev;
                using (var p = ElevacionHelper.StartElevado(null, args, out errElev))
                {
                    if (p == null)
                        return errElev ?? ElevacionHelper.MensajeUacCancelado("abrir el puerto 1433 y el firewall");
                    if (!p.WaitForExit(90000))
                        return "El script de TCP/firewall no terminó a tiempo.";
                    if (p.ExitCode != 0)
                    {
                        string log = File.Exists(logPath) ? File.ReadAllText(logPath) : "";
                        if (p.ExitCode == 2)
                        {
                            if (!SqlExpressInstaller.PuedeConectarExpress())
                                return "No está instalado SQL Server Express en esta PC.\n\n" +
                                       "Usá el botón «Instalar SQL Express» en Configuración → Red, " +
                                       "o instalá «SQL Server Express» desde Microsoft (instancia SQLEXPRESS) y reintentá.";
                            return "SQL Express responde, pero no se encontraron claves de red en el registro.\n" +
                                   "Reintentá; si sigue fallando, abrí SQL Server Configuration Manager → " +
                                   "TCP/IP Enabled, puerto 1433, y reiniciá el servicio.\n\nLog:\n" + log;
                        }
                        if (p.ExitCode == 4)
                            return "TCP/IP se configuró, pero el puerto 1433 no quedó escuchando.\n" +
                                   "Reintentá aceptando el Sí de Windows. Si persiste, en Configuration Manager " +
                                   "habilitá TCP/IP de SQLEXPRESS, IPAll = 1433 y reiniciá el servicio.\n\nLog:\n" + log;
                        if (p.ExitCode == 5)
                            return "No se pudo dejar TCP/IP Enabled=1 en el registro de SQL Express.\n" +
                                   "Ejecutá el asistente como administrador y reintentá.\n\nLog:\n" + log;
                        return "Falló la habilitación de TCP/firewall (código " + p.ExitCode + ").\n" + log;
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        /// <summary>
        /// Reaplica TCP si hace falta, espera a que 1433 escuche y prueba login schpos por TCP.
        /// Retorna true solo si TCP + SQL auth responden. Si falla, <paramref name="aviso"/> explica el motivo
        /// (el llamador no debe marcar el servidor de red como listo).
        /// </summary>
        public static bool AsegurarYProbarTcpSqlAuth(string usuario, string password, out string aviso, Action<string> progreso = null)
        {
            aviso = null;
            string err;

            // 1) ¿Ya responde TCP?
            if (ProbarTcpSqlAuthVariasFormas(usuario, password, out err))
                return true;

            // 2) Puerto cerrado → reaplicar registro TCP + firewall + reinicio servicio
            progreso?.Invoke("TCP 1433 no responde. Reaplicando TCP/IP y firewall...");
            LiberarPoolsSql();
            bool pedía = ElevacionHelper.PedirConfirmacionUac;
            ElevacionHelper.PedirConfirmacionUac = false;
            try { err = HabilitarTcpYFirewall(InstanciaLocal); }
            finally { ElevacionHelper.PedirConfirmacionUac = pedía; }

            if (err != null)
            {
                aviso =
                    "No se pudo reaplicar TCP/firewall:\n" + err + "\n\n" +
                    "El servidor local quedó configurado, pero las otras PCs pueden no conectar.\n" +
                    "Abrí SQL Server Configuration Manager → SQLEXPRESS → TCP/IP → Enabled, " +
                    "IPAll puerto 1433, reiniciá el servicio y reintentá.";
                return false;
            }

            progreso?.Invoke("Esperando que SQL Express escuche en el puerto 1433...");
            SqlExpressInstaller.ServicioEnEjecucion();
            EsperarPuertoTcp(1433, 45000);

            if (ProbarTcpSqlAuthVariasFormas(usuario, password, out err))
                return true;

            // 3) Último intento tras otra espera (servicio lento al subir)
            Thread.Sleep(5000);
            SqlExpressInstaller.ServicioEnEjecucion();
            if (ProbarTcpSqlAuthVariasFormas(usuario, password, out err))
                return true;

            bool puertoAbierto = PuertoTcpEscuchando(1433);
            int? soloWin = LeerIsIntegratedSecurityOnly();
            if (puertoAbierto && soloWin == 1)
            {
                progreso?.Invoke("El puerto 1433 responde, pero SQL sigue en solo Windows. Reciclando SQLEXPRESS...");
                string errMixto = AsegurarModoMixtoAplicado(progreso);
                if (errMixto == null && ProbarTcpSqlAuthVariasFormas(usuario, password, out err))
                    return true;
                aviso = errMixto ?? (
                    "El puerto 1433 está abierto, pero SQL aún no acepta logins SQL (SoloWindows = 1).\n" +
                    "No es falta de usuario en pantalla: hay que parar y arrancar MSSQL$SQLEXPRESS.\n\n" +
                    (err ?? ""));
                return false;
            }

            aviso =
                "TCP en 127.0.0.1:1433 aún no responde con el usuario SQL.\n" +
                (puertoAbierto
                    ? "El puerto 1433 está abierto, pero el login SQL falló o tardó demasiado.\n"
                    : "Nadie escucha en el puerto 1433 (TCP/IP de Express no quedó activo).\n") +
                (soloWin == 1
                    ? "SQL sigue en solo autenticación Windows (SoloWindows = 1): el usuario schpos será rechazado.\n"
                    : "") +
                "Detalle: " + (err ?? "(sin detalle)") + "\n\n" +
                "Pasos:\n" +
                "1) Una sola instancia Express (SQLEXPRESS). No reinicies SQLEXPRESS01 si existe.\n" +
                "2) Comprobá: sqlcmd -S \".\\SQLEXPRESS\" -E -Q \"SELECT SERVERPROPERTY('IsIntegratedSecurityOnly')\" → tiene que dar 0\n" +
                "3) Si da 1: pará y arrancá el servicio SQL Server (SQLEXPRESS) y reintentá una vez\n" +
                "4) SQL Server Configuration Manager → TCP/IP Enabled, IPAll = 1433\n" +
                "5) Firewall: regla entrante TCP 1433";
            return false;
        }

        private static bool ProbarTcpSqlAuthVariasFormas(string usuario, string password, out string error)
        {
            error = null;
            // Formato más fiable con puerto fijo: IP,puerto (sin nombre de instancia).
            if (DatabaseService.ProbarNuevaConexion("127.0.0.1", "1433", false, usuario, password, out error))
                return true;
            if (DatabaseService.ProbarNuevaConexion("127.0.0.1\\SQLEXPRESS", "1433", false, usuario, password, out error))
                return true;
            if (DatabaseService.ProbarNuevaConexion("localhost", "1433", false, usuario, password, out error))
                return true;
            // Shared Memory con SQL auth (valida usuario; no prueba red)
            if (DatabaseService.ProbarNuevaConexion(InstanciaLocal, "", false, usuario, password, out error))
            {
                // Usuario OK, pero TCP no — lo reportamos como fallo TCP
                error = "Usuario schpos OK por Shared Memory, pero TCP 1433 no responde. " + error;
            }
            return false;
        }

        public static bool PuertoTcpEscuchando(int puerto)
        {
            try
            {
                using (var client = new TcpClient())
                {
                    var ar = client.BeginConnect(IPAddress.Loopback, puerto, null, null);
                    bool ok = ar.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(2));
                    if (!ok) return false;
                    client.EndConnect(ar);
                    return client.Connected;
                }
            }
            catch { return false; }
        }

        public static void EsperarPuertoTcp(int puerto, int timeoutMs)
        {
            int waited = 0;
            while (waited < timeoutMs)
            {
                if (PuertoTcpEscuchando(puerto)) return;
                Thread.Sleep(1000);
                waited += 1000;
            }
        }

        public static void PrepararServidorParaRed(string instanciaSql)
        {
            // Compatibilidad con llamadas viejas: solo TCP + guía sin credenciales.
            HabilitarTcpYFirewall(instanciaSql);
            try
            {
                string ip = ObtenerIPRed();
                GenerarArchivoClientes(instanciaSql, null, null, ip, null);
            }
            catch { }
        }

        private const string CsExpressSchPos =
            "Server=.\\SQLEXPRESS;Database=SchPosDB;Integrated Security=True;Encrypt=False;TrustServerCertificate=True;Connect Timeout=30;";
        private const string CsExpressMaster =
            "Server=.\\SQLEXPRESS;Database=master;Integrated Security=True;Encrypt=False;TrustServerCertificate=True;Connect Timeout=30;";

        /// <summary>
        /// Si la BD activa es LocalDB con SchPosDB, migra a Express.
        /// Preferencia: BACKUP/RESTORE. Si Express es más viejo que LocalDB (ej. 2019 vs 2022),
        /// migra por copia de tablas (SQL no permite restaurar un .bak más nuevo en un servidor más viejo).
        /// </summary>
        public static string MigrarLocalDbHaciaExpress(out bool migro)
        {
            migro = false;
            try
            {
                // SCHPOS usa Microsoft.Data.SqlClient en DatabaseService y System.Data.SqlClient acá:
                // hay que limpiar ambos pools o ALTER DATABASE falla por conexiones retenidas.
                LiberarPoolsSql();

                string csActual = DatabaseService.ConnectionString ?? "";
                bool desdeLocalDb = csActual.IndexOf("(localdb)", StringComparison.OrdinalIgnoreCase) >= 0;

                // ¿Express ya tiene SchPosDB con tablas?
                bool expressConDatos = ExpressTieneSchPosDb();
                LiberarPoolsSql();
                if (expressConDatos && !desdeLocalDb)
                    return null;

                if (!desdeLocalDb)
                {
                    AsegurarBaseSchPosEnExpress();
                    return null;
                }

                Version vLocal = ObtenerVersionSql(csActual);
                Version vExpress = ObtenerVersionSql(CsExpressMaster);
                bool expressMasViejo = vLocal != null && vExpress != null
                    && (vExpress.Major < vLocal.Major
                        || (vExpress.Major == vLocal.Major && vExpress.Minor < vLocal.Minor));

                // LocalDB 2022 (17.x) → Express 2019 (15.x): RESTORE imposible.
                if (expressMasViejo)
                {
                    string errCopia = MigrarLocalDbPorCopiaDatos(csActual);
                    LiberarPoolsSql();
                    if (errCopia != null) return errCopia;
                    migro = true;
                    return null;
                }

                // Camino normal: BACKUP LocalDB → RESTORE Express
                string bakDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                    "SCHPOS", "Migrate");
                Directory.CreateDirectory(bakDir);
                string bak = Path.Combine(bakDir, "schpos_localdb_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".bak");

                LiberarPoolsSql();
                string errBak = BackupDesdeCadena(csActual, bak);
                LiberarPoolsSql();
                if (errBak != null)
                {
                    if (errBak.IndexOf("cannot open database", StringComparison.OrdinalIgnoreCase) >= 0
                        || errBak.IndexOf("no existe", StringComparison.OrdinalIgnoreCase) >= 0
                        || errBak.IndexOf("does not exist", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        AsegurarBaseSchPosEnExpress();
                        return null;
                    }
                    return errBak;
                }

                string errRest = RestaurarBakEnExpress(bak);
                LiberarPoolsSql();
                if (errRest != null)
                {
                    // Fallback si el restore falla por versión (Express viejo instalado a mano).
                    if (EsErrorVersionBackupIncompatible(errRest))
                    {
                        string errCopia = MigrarLocalDbPorCopiaDatos(csActual);
                        LiberarPoolsSql();
                        if (errCopia != null)
                        {
                            return "No se pudo restaurar el backup (Express es más antiguo que LocalDB) "
                                + "ni copiar los datos tabla por tabla.\n\n"
                                + "RESTORE: " + errRest + "\n\nCOPIA: " + errCopia + "\n\n"
                                + "Solución recomendada: instalá SQL Server Express 2022 (misma generación que LocalDB) "
                                + "con instancia SQLEXPRESS, o desinstalá Express 2019 y reintentá el asistente.";
                        }
                        migro = true;
                        return null;
                    }
                    return errRest;
                }

                migro = true;
                return null;
            }
            catch (Exception ex)
            {
                LiberarPoolsSql();
                return ex.Message;
            }
        }

        /// <summary>
        /// Libera conexiones en reposo de ambos providers ADO.NET usados por SCHPOS.
        /// </summary>
        internal static void LiberarPoolsSql()
        {
            try { SqlConnection.ClearAllPools(); } catch { /* System.Data.SqlClient (asistente / setup) */ }
            try { DatabaseService.LiberarPoolsConexion(); } catch { /* Microsoft.Data.SqlClient (DAL) */ }
        }

        /// <summary>
        /// 0 = modo mixto (acepta SQL), 1 = solo Windows, null = no se pudo consultar.
        /// </summary>
        public static int? LeerIsIntegratedSecurityOnly()
        {
            try
            {
                using (var c = new SqlConnection(CsExpressMaster))
                {
                    c.Open();
                    using (var cmd = new SqlCommand(
                        "SELECT CAST(SERVERPROPERTY('IsIntegratedSecurityOnly') AS int)", c))
                    {
                        object o = cmd.ExecuteScalar();
                        if (o == null || o == DBNull.Value) return null;
                        return Convert.ToInt32(o);
                    }
                }
            }
            catch { return null; }
        }

        /// <summary>Instancias SQL instaladas (registro 64-bit). Vacío si no se pudo leer.</summary>
        public static List<string> ListarInstanciasSql()
        {
            var list = new List<string>();
            try
            {
                using (var hive = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
                using (var k = hive.OpenSubKey(@"SOFTWARE\Microsoft\Microsoft SQL Server"))
                {
                    var inst = k?.GetValue("InstalledInstances") as string[];
                    if (inst != null)
                    {
                        foreach (string i in inst)
                        {
                            if (!string.IsNullOrWhiteSpace(i) && !list.Contains(i, StringComparer.OrdinalIgnoreCase))
                                list.Add(i.Trim());
                        }
                    }
                }
            }
            catch { }
            try
            {
                using (var hive = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
                using (var names = hive.OpenSubKey(@"SOFTWARE\Microsoft\Microsoft SQL Server\Instance Names\SQL"))
                {
                    if (names != null)
                    {
                        foreach (string n in names.GetValueNames())
                        {
                            if (!string.IsNullOrWhiteSpace(n) && !list.Contains(n, StringComparer.OrdinalIgnoreCase))
                                list.Add(n.Trim());
                        }
                    }
                }
            }
            catch { }
            return list;
        }

        /// <summary>Aviso si hay una segunda instancia (SQLEXPRESS01, etc.). Null si solo está SQLEXPRESS.</summary>
        public static string AdvertenciaInstanciasExtra()
        {
            var inst = ListarInstanciasSql();
            var extras = inst
                .Where(i => !i.Equals("SQLEXPRESS", StringComparison.OrdinalIgnoreCase)
                            && !i.Equals("MSSQLSERVER", StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (extras.Count == 0) return null;
            return "Hay más de una instancia SQL (" + string.Join(", ", inst) +
                   "). SCHPOS usa SQLEXPRESS (puerto 1433). No reinicies ni configures " +
                   string.Join(", ", extras) + ".";
        }

        /// <summary>
        /// Escribe LoginMode=2 en la hive de ESTA instancia (.\SQLEXPRESS) vía xp_instance_regwrite.
        /// Windows Auth funciona aunque SoloWindows siga en 1.
        /// </summary>
        public static string ForzarModoMixtoXpRegwrite()
        {
            try
            {
                using (var c = new SqlConnection(CsExpressMaster))
                {
                    c.Open();
                    using (var cmd = new SqlCommand(@"
EXEC xp_instance_regwrite
    N'HKEY_LOCAL_MACHINE',
    N'Software\Microsoft\MSSQLServer\MSSQLServer',
    N'LoginMode',
    REG_DWORD,
    2;", c) { CommandTimeout = 30 })
                        cmd.ExecuteNonQuery();
                }
                return null;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        /// <summary>
        /// Para y arranca MSSQL$SQLEXPRESS (no Restart-Service, no otra instancia).
        /// El modo mixto no entra si el servicio no muere del todo.
        /// </summary>
        public static string ReciclarServicioExpressCompleto()
        {
            try
            {
                string logPath = Path.Combine(Path.GetTempPath(), "schpos_recycle_sql_" + Process.GetCurrentProcess().Id + ".log");
                try { if (File.Exists(logPath)) File.Delete(logPath); } catch { }

                string script = $@"
$ErrorActionPreference = 'Continue'
$log = '{logPath.Replace("'", "''")}'
function W($m) {{ Add-Content -Path $log -Value $m }}
$svcName = 'MSSQL$SQLEXPRESS'
$extras = Get-Service -Name 'MSSQL$*' -ErrorAction SilentlyContinue | Where-Object {{ $_.Name -ne $svcName }}
if ($extras) {{ W ('EXTRA_INSTANCES:' + (($extras | ForEach-Object {{ $_.Name }}) -join ',')) }}
$svc = Get-Service -Name $svcName -ErrorAction SilentlyContinue
if ($svc -eq $null) {{ W 'SERVICE_MISSING_SQLEXPRESS'; exit 3 }}
Stop-Service -Name $svcName -Force -ErrorAction SilentlyContinue
for ($i = 0; $i -lt 50; $i++) {{
  $svc.Refresh()
  if ($svc.Status -eq 'Stopped') {{ break }}
  Start-Sleep -Milliseconds 400
}}
$svc.Refresh()
if ($svc.Status -ne 'Stopped') {{
  & sc.exe stop $svcName | Out-Null
  Start-Sleep -Seconds 4
  $svc.Refresh()
}}
W ('SERVICE_STOPPED:' + $svc.Status)
if ($svc.Status -ne 'Stopped') {{ W 'STOP_FAIL'; exit 6 }}
Start-Service -Name $svcName -ErrorAction SilentlyContinue
for ($i = 0; $i -lt 50; $i++) {{
  $svc.Refresh()
  if ($svc.Status -eq 'Running') {{ break }}
  Start-Sleep -Milliseconds 400
}}
$svc.Refresh()
if ($svc.Status -ne 'Running') {{
  Start-Service -Name $svcName -ErrorAction SilentlyContinue
  Start-Sleep -Seconds 4
  $svc.Refresh()
}}
W ('SERVICE_STARTED:' + $svc.Status)
if ($svc.Status -ne 'Running') {{ W 'START_FAIL'; exit 7 }}
W 'DONE'
exit 0
";
                string tmpScript = Path.Combine(Path.GetTempPath(), "schpos_recycle_sql.ps1");
                File.WriteAllText(tmpScript, script, Encoding.UTF8);
                string args = "-NoProfile -ExecutionPolicy Bypass -File \"" + tmpScript + "\"";
                string errElev;
                using (var p = ElevacionHelper.StartElevado(null, args, out errElev))
                {
                    if (p == null)
                        return errElev ?? ElevacionHelper.MensajeUacCancelado("parar y arrancar SQL Server (SQLEXPRESS)");
                    if (!p.WaitForExit(90000))
                        return "El reciclo de SQL Server (SQLEXPRESS) no terminó a tiempo.";
                    string log = File.Exists(logPath) ? File.ReadAllText(logPath) : "";
                    if (p.ExitCode != 0)
                    {
                        if (p.ExitCode == 3)
                            return "No está el servicio MSSQL$SQLEXPRESS. SCHPOS usa esa instancia, no SQLEXPRESS01.\n" + log;
                        return "No se pudo parar y arrancar SQL Server (SQLEXPRESS) (código " + p.ExitCode + ").\n" + log;
                    }
                }

                LiberarPoolsSql();
                SqlExpressInstaller.ServicioEnEjecucion();
                return null;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        /// <summary>
        /// No crear schpos hasta que SoloWindows = 0. Si sigue en 1: xp_instance_regwrite + stop/start de SQLEXPRESS.
        /// </summary>
        public static string AsegurarModoMixtoAplicado(Action<string> progreso = null)
        {
            LiberarPoolsSql();
            int? solo = LeerIsIntegratedSecurityOnly();
            if (solo == 0)
            {
                progreso?.Invoke("Modo mixto ya aplicado (SoloWindows = 0).");
                return null;
            }

            string extras = AdvertenciaInstanciasExtra();
            progreso?.Invoke(solo == 1
                ? "SQL está en solo Windows (SoloWindows = 1). Forzando modo mixto..."
                : "No se pudo leer el modo de autenticación. Forzando modo mixto...");

            string errXp = ForzarModoMixtoXpRegwrite();
            progreso?.Invoke("Parando y arrancando SQL Server (SQLEXPRESS) para aplicar el modo mixto...");
            string errRec = ReciclarServicioExpressCompleto();
            if (errRec != null)
            {
                return "No se pudo reciclar SQL Server (SQLEXPRESS):\n" + errRec +
                       (errXp != null ? "\n\nxp_instance_regwrite: " + errXp : "") +
                       (extras != null ? "\n\n" + extras : "");
            }

            LiberarPoolsSql();
            solo = LeerIsIntegratedSecurityOnly();
            if (solo == 0) return null;

            // Segundo intento (a veces el primer stop no alcanza).
            progreso?.Invoke("Sigue en solo Windows. Segundo reciclo de SQLEXPRESS...");
            ForzarModoMixtoXpRegwrite();
            errRec = ReciclarServicioExpressCompleto();
            LiberarPoolsSql();
            SqlExpressInstaller.ServicioEnEjecucion();
            solo = LeerIsIntegratedSecurityOnly();
            if (solo == 0) return null;

            return
                "SQL Server Express sigue en solo autenticación Windows (SoloWindows = 1).\n" +
                "El puerto 1433 puede estar abierto, pero el usuario schpos será rechazado.\n\n" +
                "No sigas con «Preparar datos» todavía.\n" +
                "Pará el servicio SQL Server (SQLEXPRESS), arrancalo de nuevo y comprobá:\n" +
                "sqlcmd -S \".\\SQLEXPRESS\" -E -Q \"SELECT SERVERPROPERTY('IsIntegratedSecurityOnly') AS SoloWindows\"\n" +
                "Tiene que dar 0. Recién ahí reintentá una vez.\n" +
                (extras != null ? "\n" + extras + "\n" : "") +
                (errXp != null ? "\nDetalle xp_instance_regwrite: " + errXp : "") +
                (errRec != null ? "\nReciclo: " + errRec : "");
        }

        /// <summary>Reusa la clave ya publicada en red_clientes.cfg; si no hay, genera una nueva.</summary>
        public static string ResolverPasswordRed(string preferida = null)
        {
            if (!string.IsNullOrWhiteSpace(preferida))
                return preferida.Trim();
            var creds = LeerCredencialesClientes();
            string p;
            if (creds.TryGetValue("Password", out p) && !string.IsNullOrWhiteSpace(p))
            {
                string u;
                if (!creds.TryGetValue("Usuario", out u) || string.IsNullOrWhiteSpace(u)
                    || u.Equals(UsuarioRedDefault, StringComparison.OrdinalIgnoreCase))
                    return p.Trim();
            }
            return GenerarPassword();
        }

        public static bool ProbarSqlAuthLocal(string usuario, string password, out string error)
        {
            return DatabaseService.ProbarNuevaConexion(InstanciaLocal, "", false, usuario, password, out error);
        }

        /// <summary>Borra login/usuario schpos (para un único reintento limpio si quedó una clave vieja).</summary>
        public static string BorrarLoginRed(string usuario)
        {
            try
            {
                string u = (usuario ?? UsuarioRedDefault).Replace("]", "]]");
                using (var c = new SqlConnection(CsExpressMaster))
                {
                    c.Open();
                    using (var cmd = new SqlCommand($@"
IF DB_ID(N'SchPosDB') IS NOT NULL
BEGIN
  DECLARE @kill nvarchar(max) = N'';
  SELECT @kill = @kill + N'KILL ' + CAST(session_id AS nvarchar(20)) + N';'
  FROM sys.dm_exec_sessions
  WHERE login_name = N'{u}' AND session_id <> @@SPID;
  IF LEN(@kill) > 0 EXEC(@kill);
END
IF DB_ID(N'SchPosDB') IS NOT NULL
BEGIN
  EXEC(N'USE [SchPosDB]; IF EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N''{u}'') DROP USER [{u}];');
END
IF EXISTS (SELECT 1 FROM sys.server_principals WHERE name = N'{u}')
  DROP LOGIN [{u}];
", c) { CommandTimeout = 60 })
                        cmd.ExecuteNonQuery();
                }
                return null;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        /// <summary>
        /// Crea o actualiza schpos. Si el login SQL falla (clave vieja de un reintento),
        /// borra el login una sola vez y lo vuelve a crear con la misma clave.
        /// </summary>
        public static string AsegurarLoginRed(out string passwordUsado, string preferida = null)
        {
            passwordUsado = ResolverPasswordRed(preferida);
            string err = CrearOActualizarLoginRed(UsuarioRedDefault, passwordUsado);
            if (err != null) return err;

            string errPrueba;
            if (ProbarSqlAuthLocal(UsuarioRedDefault, passwordUsado, out errPrueba))
                return null;

            // Un solo recreado: evita diez claves distintas contra el mismo login.
            string errDrop = BorrarLoginRed(UsuarioRedDefault);
            if (errDrop != null)
                return "Login schpos no conecta y no se pudo borrar el viejo:\n" + errDrop +
                       "\n\nPrueba: " + (errPrueba ?? "");

            err = CrearOActualizarLoginRed(UsuarioRedDefault, passwordUsado);
            if (err != null) return err;

            if (ProbarSqlAuthLocal(UsuarioRedDefault, passwordUsado, out errPrueba))
                return null;

            int? solo = LeerIsIntegratedSecurityOnly();
            if (solo == 1)
            {
                return "Se recreó el usuario schpos, pero SQL sigue en solo Windows (SoloWindows = 1).\n" +
                       "No reintentés el asistente: pará y arrancá MSSQL$SQLEXPRESS y comprobá que SoloWindows = 0.";
            }

            return "El usuario schpos se recreó y sigue sin conectar.\n" + (errPrueba ?? "");
        }

        public static string CrearOActualizarLoginRed(string usuario, string password)
        {
            try
            {
                string csMaster =
                    "Server=.\\SQLEXPRESS;Database=master;Integrated Security=True;Encrypt=False;TrustServerCertificate=True;Connect Timeout=15;";
                string u = (usuario ?? UsuarioRedDefault).Replace("]", "]]");
                string p = (password ?? "").Replace("'", "''");

                using (var c = new SqlConnection(csMaster))
                {
                    c.Open();
                    string sql = $@"
IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = N'{u}')
  CREATE LOGIN [{u}] WITH PASSWORD = N'{p}', CHECK_POLICY = OFF, CHECK_EXPIRATION = OFF;
ELSE
  ALTER LOGIN [{u}] WITH PASSWORD = N'{p}', CHECK_POLICY = OFF, CHECK_EXPIRATION = OFF;

IF DB_ID(N'SchPosDB') IS NULL
  CREATE DATABASE [SchPosDB];

USE [SchPosDB];
IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'{u}')
  CREATE USER [{u}] FOR LOGIN [{u}];
IF IS_ROLEMEMBER('db_owner', N'{u}') = 0
  ALTER ROLE db_owner ADD MEMBER [{u}];
";
                    using (var cmd = new SqlCommand(sql, c) { CommandTimeout = 60 })
                        cmd.ExecuteNonQuery();
                }
                return null;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        public static string GenerarArchivoClientes(string instanciaSql, string usuario, string password, string ip, string servidorClientes)
        {
            string instancia = string.IsNullOrWhiteSpace(instanciaSql) ? InstanciaLocal : instanciaSql.Trim();
            if (string.IsNullOrWhiteSpace(ip)) ip = ObtenerIPRed();

            if (string.IsNullOrWhiteSpace(servidorClientes))
            {
                servidorClientes = instancia
                    .Replace(".\\", ip + "\\")
                    .Replace("localhost\\", ip + "\\")
                    .Replace("127.0.0.1\\", ip + "\\");
                if (servidorClientes == "." || servidorClientes.Equals("localhost", StringComparison.OrdinalIgnoreCase)
                    || servidorClientes == "127.0.0.1")
                    servidorClientes = ip + "\\SQLEXPRESS";
                int coma = servidorClientes.IndexOf(',');
                if (coma > 0) servidorClientes = servidorClientes.Substring(0, coma);
            }

            bool tieneSql = !string.IsNullOrWhiteSpace(usuario) && !string.IsNullOrWhiteSpace(password);

            string contenido = $@"=== SCHPOS — Configuración de conexión para PCs de la red ===
Generado: {DateTime.Now:dd/MM/yyyy HH:mm}
Servidor IP: {ip}
Instancia: {instancia}

------------------------------------------------------------
EN CADA PC CLIENTE:
------------------------------------------------------------
1. Instalá SCHPOS.
2. Activá la licencia con el extra «Conexión en RED».
3. Configuración → Red y Servidor → modo CLIENTE.
4. Completá:
   IP / servidor: {servidorClientes}
   Puerto: 1433
{(tieneSql ? $@"   Autenticación Windows: NO
   Usuario SQL: {usuario}
   Contraseña: {password}
" : "   Autenticación: Windows (mismo usuario/clave que en el servidor) o SQL si te dieron usuario.\n")}
5. Guardar y reiniciar. Entrar con el mismo usuario admin del servidor.

Cadena de ejemplo (SQL):
Server={servidorClientes},1433;Database=SchPosDB;User Id={usuario ?? "schpos"};Password={password ?? "****"};Encrypt=False;TrustServerCertificate=True;

Checklist si no conecta:
• Ping a {ip}
• Puerto TCP 1433 abierto en el servidor
• Servidor no hibernado / firewall SCHPOS-SQL-1433
• Misma licencia RED en el cliente
";

            string escritorio = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            string ruta = Path.Combine(escritorio, "SCHPOS-Configuracion-Clientes.txt");
            File.WriteAllText(ruta, contenido, Encoding.UTF8);

            // Copia también en ProgramData
            try
            {
                string copia = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                    "SCHPOS", "SCHPOS-Configuracion-Clientes.txt");
                File.WriteAllText(copia, contenido, Encoding.UTF8);
            }
            catch { }

            return ruta;
        }

        public static void GuardarCredencialesClientes(string servidor, string puerto, string usuario, string password)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(RutaCredencialesClientes));
                File.WriteAllText(RutaCredencialesClientes,
                    $"Servidor={servidor}\r\nPuerto={puerto}\r\nUsuario={usuario}\r\nPassword={password}\r\n",
                    Encoding.UTF8);
            }
            catch { }
        }

        public static Dictionary<string, string> LeerCredencialesClientes()
        {
            var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                if (!File.Exists(RutaCredencialesClientes)) return d;
                foreach (var line in File.ReadAllLines(RutaCredencialesClientes))
                {
                    int eq = line.IndexOf('=');
                    if (eq <= 0) continue;
                    d[line.Substring(0, eq).Trim()] = line.Substring(eq + 1).Trim();
                }
            }
            catch { }
            return d;
        }

        public static string ObtenerIPRed()
        {
            try
            {
                foreach (var ni in NetworkInterface.GetAllNetworkInterfaces()
                    .Where(n => n.OperationalStatus == OperationalStatus.Up
                             && n.NetworkInterfaceType != NetworkInterfaceType.Loopback
                             && n.NetworkInterfaceType != NetworkInterfaceType.Tunnel))
                {
                    string name = (ni.Name + " " + ni.Description).ToLowerInvariant();
                    if (name.Contains("virtual") || name.Contains("vmware") || name.Contains("hyper-v")
                        || name.Contains("vethernet") || name.Contains("docker") || name.Contains("wsl"))
                        continue;

                    var props = ni.GetIPProperties();
                    if (props.GatewayAddresses == null || props.GatewayAddresses.Count == 0) continue;
                    bool hasGw = props.GatewayAddresses.Any(g =>
                        g.Address != null && g.Address.AddressFamily == AddressFamily.InterNetwork
                        && !g.Address.ToString().StartsWith("0."));
                    if (!hasGw) continue;

                    foreach (var ua in props.UnicastAddresses)
                    {
                        if (ua.Address.AddressFamily == AddressFamily.InterNetwork)
                        {
                            string ip = ua.Address.ToString();
                            if (ip.StartsWith("169.254.")) continue;
                            return ip;
                        }
                    }
                }
            }
            catch { }
            return "VERIFICAR-IP-SERVIDOR";
        }

        public static bool EsConexionLocalDb(string cs)
        {
            return !string.IsNullOrEmpty(cs)
                && cs.IndexOf("(localdb)", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public static string GenerarPassword()
        {
            const string alphabet = "abcdefghijkmnopqrstuvwxyzABCDEFGHJKLMNPQRSTUVWXYZ23456789";
            var bytes = new byte[16];
            using (var rng = RandomNumberGenerator.Create())
                rng.GetBytes(bytes);
            var sb = new StringBuilder(18);
            sb.Append("Sp");
            for (int i = 0; i < 14; i++)
                sb.Append(alphabet[bytes[i] % alphabet.Length]);
            sb.Append('!');
            return sb.ToString();
        }

        private static string ResolverNombreServicio(string instancia)
        {
            int bs = instancia.IndexOf('\\');
            if (bs >= 0)
            {
                string inst = instancia.Substring(bs + 1).ToUpperInvariant();
                int coma = inst.IndexOf(',');
                if (coma > 0) inst = inst.Substring(0, coma);
                return inst == "MSSQLSERVER" ? "MSSQLSERVER" : "MSSQL$" + inst;
            }
            return "MSSQLSERVER";
        }

        private static bool ExpressTieneSchPosDb()
        {
            try
            {
                using (var c = new SqlConnection(
                    "Server=.\\SQLEXPRESS;Database=SchPosDB;Integrated Security=True;Encrypt=False;TrustServerCertificate=True;Connect Timeout=5;"))
                {
                    c.Open();
                    using (var cmd = new SqlCommand(
                        "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME IN ('Productos','Usuarios')", c))
                    {
                        int n = Convert.ToInt32(cmd.ExecuteScalar());
                        return n >= 1;
                    }
                }
            }
            catch { return false; }
        }

        private static void AsegurarBaseSchPosEnExpress()
        {
            using (var c = new SqlConnection(
                "Server=.\\SQLEXPRESS;Database=master;Integrated Security=True;Encrypt=False;TrustServerCertificate=True;Connect Timeout=15;"))
            {
                c.Open();
                using (var cmd = new SqlCommand(
                    "IF DB_ID(N'SchPosDB') IS NULL CREATE DATABASE [SchPosDB];", c))
                    cmd.ExecuteNonQuery();
            }
        }

        private static string BackupDesdeCadena(string cs, string rutaBak)
        {
            try
            {
                var b = new SqlConnectionStringBuilder(cs);
                string db = b.InitialCatalog;
                if (string.IsNullOrWhiteSpace(db)) db = "SchPosDB";
                string seguro = db.Replace("]", "]]");
                string rutaSql = rutaBak.Replace("'", "''");

                using (var c = new SqlConnection(cs))
                {
                    c.Open();
                    using (var cmd = new SqlCommand(
                        $"BACKUP DATABASE [{seguro}] TO DISK = N'{rutaSql}' WITH FORMAT, INIT, SKIP, NOREWIND, NOUNLOAD",
                        c) { CommandTimeout = 300 })
                        cmd.ExecuteNonQuery();
                }
                return null;
            }
            catch (Exception ex) { return ex.Message; }
        }

        private static string RestaurarBakEnExpress(string rutaBak)
        {
            try
            {
                // ALTER/RESTORE siempre desde master: nunca abrir SchPosDB en esta conexión.
                string csMaster =
                    "Server=.\\SQLEXPRESS;Database=master;Integrated Security=True;Encrypt=False;TrustServerCertificate=True;Connect Timeout=30;Pooling=False;";

                // Copiar bak a carpeta legible por el servicio SQL
                string stagingDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                    "SCHPOS", "Migrate");
                Directory.CreateDirectory(stagingDir);
                string staged = Path.Combine(stagingDir, "restore_express.bak");
                File.Copy(rutaBak, staged, true);
                string rutaSql = staged.Replace("'", "''");

                LiberarPoolsSql();

                using (var c = new SqlConnection(csMaster))
                {
                    c.Open();

                    string dataPath = null, logPath = null;
                    using (var cmd = new SqlCommand(
                        "SELECT CAST(SERVERPROPERTY('InstanceDefaultDataPath') AS nvarchar(500)), CAST(SERVERPROPERTY('InstanceDefaultLogPath') AS nvarchar(500))", c))
                    using (var rd = cmd.ExecuteReader())
                    {
                        if (rd.Read())
                        {
                            dataPath = rd.IsDBNull(0) ? null : rd.GetString(0);
                            logPath = rd.IsDBNull(1) ? null : rd.GetString(1);
                        }
                    }
                    if (string.IsNullOrWhiteSpace(dataPath))
                        dataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "SCHPOS", "SqlData");
                    if (string.IsNullOrWhiteSpace(logPath))
                        logPath = dataPath;
                    Directory.CreateDirectory(dataPath);
                    Directory.CreateDirectory(logPath);

                    string logicalData = null, logicalLog = null;
                    using (var cmd = new SqlCommand($"RESTORE FILELISTONLY FROM DISK = N'{rutaSql}'", c))
                    using (var rd = cmd.ExecuteReader())
                    {
                        while (rd.Read())
                        {
                            string logical = rd["LogicalName"].ToString();
                            string type = rd["Type"].ToString();
                            if (type == "D" && logicalData == null) logicalData = logical;
                            if (type == "L" && logicalLog == null) logicalLog = logical;
                        }
                    }
                    if (logicalData == null) logicalData = "SchPosDB";
                    if (logicalLog == null) logicalLog = "SchPosDB_log";

                    // 1) Si quedó SINGLE_USER de un intento fallido, volver a MULTI_USER expulsando sesiones.
                    // 2) Matar spids que usen SchPosDB (excepto la sesión actual).
                    // 3) Pasar a SINGLE_USER y restaurar.
                    // 4) Dejar MULTI_USER con ROLLBACK IMMEDIATE.
                    PrepararSchPosDbParaRestore(c);

                    string mdf = Path.Combine(dataPath, "SchPosDB.mdf").Replace("'", "''");
                    string ldf = Path.Combine(logPath, "SchPosDB_log.ldf").Replace("'", "''");
                    string ld = logicalData.Replace("]", "]]");
                    string ll = logicalLog.Replace("]", "]]");

                    using (var cmd = new SqlCommand($@"
RESTORE DATABASE [SchPosDB] FROM DISK = N'{rutaSql}' WITH REPLACE, RECOVERY,
  MOVE N'{ld}' TO N'{mdf}',
  MOVE N'{ll}' TO N'{ldf}'
", c) { CommandTimeout = 600 })
                        cmd.ExecuteNonQuery();

                    using (var cmd = new SqlCommand(
                        "ALTER DATABASE [SchPosDB] SET MULTI_USER WITH ROLLBACK IMMEDIATE;", c)
                    { CommandTimeout = 60 })
                        cmd.ExecuteNonQuery();
                }

                LiberarPoolsSql();
                return null;
            }
            catch (Exception ex)
            {
                // Intentar dejar la BD usable si el restore falló a mitad de camino.
                try
                {
                    LiberarPoolsSql();
                    using (var c2 = new SqlConnection(
                        "Server=.\\SQLEXPRESS;Database=master;Integrated Security=True;Encrypt=False;TrustServerCertificate=True;Connect Timeout=15;Pooling=False;"))
                    {
                        c2.Open();
                        using (var cmd = new SqlCommand(@"
IF DB_ID(N'SchPosDB') IS NOT NULL
  ALTER DATABASE [SchPosDB] SET MULTI_USER WITH ROLLBACK IMMEDIATE;
", c2) { CommandTimeout = 30 })
                            cmd.ExecuteNonQuery();
                    }
                }
                catch { /* best effort */ }

                LiberarPoolsSql();
                return ex.Message;
            }
        }

        /// <summary>
        /// Expulsa conexiones y deja SchPosDB en SINGLE_USER listo para RESTORE.
        /// Debe ejecutarse sobre una conexión abierta a master.
        /// </summary>
        private static void PrepararSchPosDbParaRestore(SqlConnection cMaster)
        {
            // Si ya está en SINGLE_USER con alguien adentro, primero forzar MULTI_USER.
            using (var cmd = new SqlCommand(@"
IF DB_ID(N'SchPosDB') IS NOT NULL
BEGIN
  BEGIN TRY
    ALTER DATABASE [SchPosDB] SET MULTI_USER WITH ROLLBACK IMMEDIATE;
  END TRY
  BEGIN CATCH
    -- Ignorar: puede estar ya en MULTI_USER o en proceso de cambio.
  END CATCH
END
", cMaster) { CommandTimeout = 60 })
                cmd.ExecuteNonQuery();

            LiberarPoolsSql();

            // Expulsar cualquier sesión restante apuntando a SchPosDB.
            using (var cmd = new SqlCommand(@"
IF DB_ID(N'SchPosDB') IS NOT NULL
BEGIN
  DECLARE @sql nvarchar(max) = N'';
  SELECT @sql = @sql + N'KILL ' + CAST(session_id AS nvarchar(20)) + N';'
  FROM sys.dm_exec_sessions
  WHERE database_id = DB_ID(N'SchPosDB')
    AND session_id <> @@SPID;
  IF LEN(@sql) > 0 EXEC(@sql);
END
", cMaster) { CommandTimeout = 60 })
            {
                try { cmd.ExecuteNonQuery(); }
                catch { /* KILL puede fallar por permisos/timing; seguimos con ALTER */ }
            }

            LiberarPoolsSql();

            using (var cmd = new SqlCommand(@"
IF DB_ID(N'SchPosDB') IS NOT NULL
  ALTER DATABASE [SchPosDB] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
", cMaster) { CommandTimeout = 60 })
                cmd.ExecuteNonQuery();
        }

        private static bool EsErrorVersionBackupIncompatible(string error)
        {
            if (string.IsNullOrEmpty(error)) return false;
            string e = error.ToLowerInvariant();
            return e.Contains("no es compatible")
                || e.Contains("not compatible")
                || e.Contains("versión de servidor")
                || e.Contains("version of the server")
                || e.Contains("cannot be restored")
                || (e.Contains("restore") && e.Contains("version"));
        }

        private static Version ObtenerVersionSql(string connectionString)
        {
            try
            {
                var b = new SqlConnectionStringBuilder(connectionString);
                if (string.IsNullOrWhiteSpace(b.InitialCatalog))
                    b.InitialCatalog = "master";
                b.ConnectTimeout = 8;
                using (var c = new SqlConnection(b.ConnectionString))
                {
                    c.Open();
                    using (var cmd = new SqlCommand("SELECT CAST(SERVERPROPERTY('ProductVersion') AS nvarchar(40))", c))
                    {
                        string v = cmd.ExecuteScalar()?.ToString();
                        if (string.IsNullOrWhiteSpace(v)) return null;
                        // 15.00.2000.0 → Version
                        return Version.TryParse(v, out Version ver) ? ver : null;
                    }
                }
            }
            catch { return null; }
        }

        /// <summary>
        /// Migra LocalDB → Express cuando no se puede RESTORE (Express más viejo).
        /// Crea el esquema con DatabaseService y copia los datos tabla por tabla.
        /// </summary>
        private static string MigrarLocalDbPorCopiaDatos(string csLocalDb)
        {
            try
            {
                LiberarPoolsSql();
                AsegurarBaseSchPosEnExpress();

                var bLocal = new SqlConnectionStringBuilder(csLocalDb);
                if (string.IsNullOrWhiteSpace(bLocal.InitialCatalog))
                    bLocal.InitialCatalog = "SchPosDB";
                string csLocal = bLocal.ConnectionString;

                // Esquema actual de SCHPOS en Express (no el DDL crudo de LocalDB).
                DatabaseService.ActualizarConexion(CsExpressSchPos);
                LiberarPoolsSql();
                if (!DatabaseService.InitializeDatabase())
                    return "No se pudo crear el esquema SchPosDB en SQL Express.";
                try
                {
                    DatabaseService.MigrarNombresPermisosConGuionBajo();
                    DatabaseService.InicializarPermisosBaseDatos();
                    DatabaseService.AsegurarUsuarioAdminInicial();
                }
                catch { /* best effort: el esquema base ya está */ }

                LiberarPoolsSql();

                using (var src = new SqlConnection(csLocal))
                using (var dst = new SqlConnection(CsExpressSchPos))
                {
                    src.Open();
                    dst.Open();

                    // Desactivar FKs para poder vaciar y cargar en cualquier orden.
                    using (var cmd = new SqlCommand(
                        "EXEC sp_MSforeachtable 'ALTER TABLE ? NOCHECK CONSTRAINT ALL'", dst)
                    { CommandTimeout = 120 })
                        cmd.ExecuteNonQuery();

                    var tablas = ListarTablasUsuario(src);
                    var errores = new List<string>();
                    int copiadas = 0;

                    foreach (string tabla in tablas)
                    {
                        if (!ExisteTabla(dst, tabla))
                            continue;
                        try
                        {
                            CopiarTabla(src, dst, tabla);
                            copiadas++;
                        }
                        catch (Exception exTabla)
                        {
                            errores.Add(tabla + ": " + exTabla.Message);
                        }
                    }

                    using (var cmd = new SqlCommand(
                        "EXEC sp_MSforeachtable 'ALTER TABLE ? WITH CHECK CHECK CONSTRAINT ALL'", dst)
                    { CommandTimeout = 120 })
                    {
                        try { cmd.ExecuteNonQuery(); }
                        catch (Exception exFk)
                        {
                            errores.Add("Reactivar FKs: " + exFk.Message);
                        }
                    }

                    if (copiadas == 0 && tablas.Count > 0)
                    {
                        return "No se copió ninguna tabla desde LocalDB hacia Express.\n"
                            + string.Join("\n", errores.Take(8));
                    }

                    if (errores.Count > 0 && copiadas == 0)
                        return string.Join("\n", errores.Take(10));

                    // Éxito parcial: seguimos (mejor que abortar con datos a medias no usables).
                    // Si hubo errores menores, los dejamos en null (migración usable).
                }

                LiberarPoolsSql();
                return null;
            }
            catch (Exception ex)
            {
                LiberarPoolsSql();
                return "Migración por copia de datos: " + ex.Message;
            }
        }

        private static List<string> ListarTablasUsuario(SqlConnection c)
        {
            var list = new List<string>();
            using (var cmd = new SqlCommand(@"
SELECT TABLE_NAME
FROM INFORMATION_SCHEMA.TABLES
WHERE TABLE_TYPE = 'BASE TABLE' AND TABLE_SCHEMA = 'dbo'
ORDER BY TABLE_NAME", c))
            using (var rd = cmd.ExecuteReader())
            {
                while (rd.Read())
                    list.Add(rd.GetString(0));
            }
            return list;
        }

        private static bool ExisteTabla(SqlConnection c, string tabla)
        {
            using (var cmd = new SqlCommand(@"
SELECT 1 FROM INFORMATION_SCHEMA.TABLES
WHERE TABLE_SCHEMA = 'dbo' AND TABLE_TYPE = 'BASE TABLE' AND TABLE_NAME = @t", c))
            {
                cmd.Parameters.AddWithValue("@t", tabla);
                return cmd.ExecuteScalar() != null;
            }
        }

        private static void CopiarTabla(SqlConnection src, SqlConnection dst, string tabla)
        {
            string seguro = tabla.Replace("]", "]]");

            // Columnas en común (evita fallar si el esquema Express tiene columnas nuevas).
            var colsSrc = ListarColumnas(src, tabla);
            var colsDst = new HashSet<string>(ListarColumnas(dst, tabla), StringComparer.OrdinalIgnoreCase);
            var cols = colsSrc.Where(c => colsDst.Contains(c)).ToList();
            if (cols.Count == 0) return;

            string listaCols = string.Join(", ", cols.Select(c => "[" + c.Replace("]", "]]") + "]"));

            using (var del = new SqlCommand($"DELETE FROM [{seguro}]", dst) { CommandTimeout = 120 })
                del.ExecuteNonQuery();

            using (var cmd = new SqlCommand($"SELECT {listaCols} FROM [{seguro}]", src) { CommandTimeout = 300 })
            using (var reader = cmd.ExecuteReader())
            {
                if (!reader.HasRows) return;

                using (var bulk = new SqlBulkCopy(dst,
                    SqlBulkCopyOptions.KeepIdentity | SqlBulkCopyOptions.KeepNulls | SqlBulkCopyOptions.TableLock,
                    null))
                {
                    bulk.DestinationTableName = "dbo.[" + seguro + "]";
                    bulk.BulkCopyTimeout = 600;
                    bulk.BatchSize = 500;
                    foreach (string col in cols)
                        bulk.ColumnMappings.Add(col, col);
                    bulk.WriteToServer(reader);
                }
            }
        }

        private static List<string> ListarColumnas(SqlConnection c, string tabla)
        {
            var list = new List<string>();
            using (var cmd = new SqlCommand(@"
SELECT COLUMN_NAME
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = @t
  AND COLUMNPROPERTY(OBJECT_ID(QUOTENAME(TABLE_SCHEMA)+'.'+QUOTENAME(TABLE_NAME)), COLUMN_NAME, 'IsComputed') = 0
ORDER BY ORDINAL_POSITION", c))
            {
                cmd.Parameters.AddWithValue("@t", tabla);
                using (var rd = cmd.ExecuteReader())
                {
                    while (rd.Read())
                        list.Add(rd.GetString(0));
                }
            }
            return list;
        }
    }
}
