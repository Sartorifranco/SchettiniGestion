using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace SchettiniGestion.WPF
{
    /// <summary>
    /// Prepara SQL Server Express para multiestación: TCP/IP puerto 1433, firewall y guía de clientes.
    /// </summary>
    internal static class SqlServerNetworkSetup
    {
        public static void PrepararServidorParaRed(string instanciaSql)
        {
            HabilitarTcpYFirewall(instanciaSql);
            try { GenerarArchivoClientes(instanciaSql); } catch { }
        }

        public static void HabilitarTcpYFirewall(string instanciaSql)
        {
            try
            {
                string instancia = string.IsNullOrWhiteSpace(instanciaSql) ? @".\SQLEXPRESS" : instanciaSql.Trim();
                string nombreServicio;
                int bs = instancia.IndexOf('\\');
                if (bs >= 0)
                {
                    string inst = instancia.Substring(bs + 1).ToUpperInvariant();
                    // Quitar ",1433" si vino pegado
                    int coma = inst.IndexOf(',');
                    if (coma > 0) inst = inst.Substring(0, coma);
                    nombreServicio = inst == "MSSQLSERVER" ? "MSSQLSERVER" : $"MSSQL${inst}";
                }
                else
                {
                    nombreServicio = "MSSQLSERVER";
                }

                string script = $@"
$regBase = 'HKLM:\SOFTWARE\Microsoft\Microsoft SQL Server'
$inst = (Get-ItemProperty ""$regBase"" -ErrorAction SilentlyContinue).InstalledInstances
foreach ($i in $inst) {{
    $keyName = (Get-ItemProperty ""$regBase\Instance Names\SQL"" -ErrorAction SilentlyContinue).$i
    $tcpPath = ""$regBase\$keyName\MSSQLServer\SuperSocketNetLib\Tcp""
    if (Test-Path $tcpPath) {{
        Set-ItemProperty -Path $tcpPath -Name 'Enabled' -Value 1 -ErrorAction SilentlyContinue
        $ipAllPath = ""$tcpPath\IPAll""
        if (Test-Path $ipAllPath) {{
            Set-ItemProperty -Path $ipAllPath -Name 'TcpDynamicPorts' -Value '' -ErrorAction SilentlyContinue
            Set-ItemProperty -Path $ipAllPath -Name 'TcpPort' -Value '1433' -ErrorAction SilentlyContinue
        }}
    }}
    $loginModePath = ""$regBase\$keyName\MSSQLServer""
    if (Test-Path $loginModePath) {{
        Set-ItemProperty -Path $loginModePath -Name 'LoginMode' -Value 2 -ErrorAction SilentlyContinue
    }}
}}

$ruleName = 'SCHPOS-SQL-1433'
$existe = netsh advfirewall firewall show rule name=$ruleName 2>&1
if ($existe -match 'No rules match') {{
    netsh advfirewall firewall add rule name=$ruleName protocol=TCP dir=in action=allow localport=1433 | Out-Null
}}

$svc = Get-Service -Name '{nombreServicio}' -ErrorAction SilentlyContinue
if ($svc -ne $null) {{
    Restart-Service -Name '{nombreServicio}' -Force -ErrorAction SilentlyContinue
}}
";
                string tmpScript = Path.Combine(Path.GetTempPath(), "schpos_enable_tcp.ps1");
                File.WriteAllText(tmpScript, script);

                var psi = new ProcessStartInfo("powershell.exe",
                    $"-ExecutionPolicy Bypass -WindowStyle Hidden -File \"{tmpScript}\"")
                {
                    Verb = "runas",
                    UseShellExecute = true,
                };
                using (var p = Process.Start(psi))
                    p?.WaitForExit(20000);
            }
            catch { /* No bloquear: el usuario puede habilitarlo a mano */ }
        }

        public static void GenerarArchivoClientes(string instanciaSql)
        {
            string instancia = string.IsNullOrWhiteSpace(instanciaSql) ? @".\SQLEXPRESS" : instanciaSql.Trim();
            string ipServidor = ObtenerIPRed();

            string servidorParaClientes = instancia
                .Replace(".\\", $"{ipServidor}\\")
                .Replace("localhost\\", $"{ipServidor}\\")
                .Replace("127.0.0.1\\", $"{ipServidor}\\");
            if (servidorParaClientes == "." || servidorParaClientes.Equals("localhost", StringComparison.OrdinalIgnoreCase)
                || servidorParaClientes == "127.0.0.1")
                servidorParaClientes = $"{ipServidor}\\SQLEXPRESS";

            int coma = servidorParaClientes.IndexOf(',');
            if (coma > 0) servidorParaClientes = servidorParaClientes.Substring(0, coma);

            string paso3 = $"   IP o nombre del servidor: {servidorParaClientes}\n   Puerto: 1433";

            string contenido = $@"=== SCHPOS — Configuración de conexión para PCs de la red ===
Generado: {DateTime.Now:dd/MM/yyyy HH:mm}
Servidor: {ipServidor}  |  Instancia SQL: {instancia}

------------------------------------------------------------
PASOS PARA CADA PC CLIENTE:
------------------------------------------------------------
1. Instalá SCHPOS en la PC cliente.
2. Al abrir / en Configuración → Red y Servidor, elegí CLIENTE.
3. Completá:

{paso3}

4. Autenticación Windows (si falla, usá usuario SQL).
5. Guardar y reiniciar.

Cadena de ejemplo:
Server={servidorParaClientes},1433;Database=SchPosDB;Integrated Security=True;Encrypt=False;TrustServerCertificate=True;
";

            string escritorio = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            string ruta = Path.Combine(escritorio, "SCHPOS-Configuracion-Clientes.txt");
            File.WriteAllText(ruta, contenido);
        }

        public static string ObtenerIPRed()
        {
            try
            {
                foreach (var ni in NetworkInterface.GetAllNetworkInterfaces()
                    .Where(n => n.OperationalStatus == OperationalStatus.Up
                             && n.NetworkInterfaceType != NetworkInterfaceType.Loopback))
                {
                    var props = ni.GetIPProperties();
                    if (props.GatewayAddresses == null || props.GatewayAddresses.Count == 0) continue;
                    foreach (var ua in props.UnicastAddresses)
                    {
                        if (ua.Address.AddressFamily == AddressFamily.InterNetwork)
                            return ua.Address.ToString();
                    }
                }
            }
            catch { }
            return "192.168.X.X";
        }
    }
}
