using System;
using System.Data.SqlClient;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Media;
using SchettiniGestion;

namespace SchettiniGestion.WPF
{
    public partial class PrimerUsoWindow : Window
    {
        private string _cadenaTesteada = null;
        private double _originalTop    = double.NaN;

        public PrimerUsoWindow()
        {
            InitializeComponent();
            Loaded   += (s, e) =>
            {
                KeyboardService.VisibilityChanged += OnKeyboardVisibilityChanged;
                if (KeyboardService.IsEnabled && KeyboardService.KeyboardTop < double.MaxValue)
                    OnKeyboardVisibilityChanged(true);
            };
            Unloaded += (s, e) => KeyboardService.VisibilityChanged -= OnKeyboardVisibilityChanged;
        }

        private void OnKeyboardVisibilityChanged(bool visible)
        {
            Dispatcher.Invoke(() =>
            {
                if (visible)
                {
                    if (double.IsNaN(_originalTop)) _originalTop = Top;
                    double kbTop = KeyboardService.KeyboardTop;
                    Top = Math.Max(4, (kbTop - ActualHeight) / 2.0);
                }
                else if (!double.IsNaN(_originalTop))
                {
                    Top        = _originalTop;
                    _originalTop = double.NaN;
                }
            });
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            bool redOk = LicenseManager.ValidarLicencia() && LicenseManager.TieneConexionRed();
            if (rbServidor != null) rbServidor.Visibility = redOk ? Visibility.Visible : Visibility.Collapsed;
            if (rbCliente != null) rbCliente.Visibility = redOk ? Visibility.Visible : Visibility.Collapsed;
            if (txtNotaConexionRed != null)
                txtNotaConexionRed.Visibility = redOk ? Visibility.Collapsed : Visibility.Visible;
        }

        private void Header_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ButtonState == System.Windows.Input.MouseButtonState.Pressed) DragMove();
        }

        // ── Cambio de modo ───────────────────────────────────────────────────────────

        private void rbModo_Checked(object sender, RoutedEventArgs e)
        {
            if (panelExpress == null) return;

            bool esServidor = rbServidor.IsChecked == true;
            bool esCliente  = rbCliente.IsChecked == true;

            panelExpress.Visibility = esServidor ? Visibility.Visible : Visibility.Collapsed;
            panelCliente.Visibility = esCliente  ? Visibility.Visible : Visibility.Collapsed;

            _cadenaTesteada = null;
            btnContinuar.IsEnabled = false;
            SetTestStatus("⚪", "Listo para probar la conexión",
                "Presioná 'Probar conexión' para verificar antes de continuar.", "neutral");
        }

        private void rbAuth_Checked(object sender, RoutedEventArgs e)
        {
            if (panelSqlAuth == null) return;
            panelSqlAuth.Visibility = rbSqlAuth.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        }

        private void txtIPServidor_GotFocus(object sender, RoutedEventArgs e)
        {
            if (txtIPServidor.Text.Trim() == "") txtIPServidor.SelectAll();
        }

        // ── Detectar instancias SQL locales ──────────────────────────────────────────

        private void btnDetectarInstancias_Click(object sender, RoutedEventArgs e)
        {
            SetTestStatus("⏳", "Buscando SQL Server en esta PC...", "Probando instancias comunes...", "pending");

            string[] candidatos = {
                @".\SQLEXPRESS",
                @".\MSSQLSERVER",
                @".\SQLSERVER2019",
                @".\SQLSERVER2022",
                @"localhost\SQLEXPRESS",
            };

            string encontrado = null;
            foreach (string inst in candidatos)
            {
                try
                {
                    string cs = $"Server={inst};Database=master;Integrated Security=True;Connect Timeout=3;Encrypt=False;TrustServerCertificate=True;";
                    using (var conn = new SqlConnection(cs))
                    {
                        conn.Open();
                        encontrado = inst;
                        break;
                    }
                }
                catch { }
            }

            if (encontrado != null)
            {
                txtInstanciaServidor.Text = encontrado;
                panelAvisoExpress.Visibility = Visibility.Collapsed;
                SetTestStatus("✔", $"SQL Server encontrado: {encontrado}",
                    "Presioná 'Probar conexión' para continuar.", "success");
            }
            else
            {
                panelAvisoExpress.Visibility = Visibility.Visible;
                SetTestStatus("✖", "SQL Server Express no detectado",
                    "No se encontró ninguna instancia local. Revisá el aviso naranja para instrucciones.", "error");
            }
        }

        private void LinkDescarga_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            try { Process.Start(new ProcessStartInfo("https://aka.ms/sqlexpress") { UseShellExecute = true }); }
            catch { }
        }

        // ── Construcción de cadena de conexión ───────────────────────────────────────

        private string ObtenerCadenaSeleccionada()
        {
            if (rbSoloPC.IsChecked == true)
                return DatabaseService.CS_LOCALDB;

            if (rbServidor.IsChecked == true)
            {
                string inst = txtInstanciaServidor.Text.Trim();
                if (string.IsNullOrEmpty(inst)) inst = @".\SQLEXPRESS";
                return $"Server={inst};Database=SchPosDB;Integrated Security=True;Encrypt=False;TrustServerCertificate=True;";
            }

            // rbCliente
            string ip = txtIPServidor.Text.Trim();
            if (string.IsNullOrEmpty(ip)) return null;

            // Server={ip}\Instancia,puerto es un formato válido y RECOMENDADO: al incluir el
            // puerto junto con el nombre de instancia, el driver se conecta directo por TCP y
            // evita depender del servicio "SQL Server Browser" (UDP 1434) para resolver el
            // puerto dinámico. Antes acá se descartaba el puerto si había instancia — quedaba
            // atado a SQL Browser sin que el usuario lo supiera. Ahora siempre lo incluimos si
            // se cargó un valor.
            string puerto = txtPuertoCliente.Text.Trim();
            string dataSource = string.IsNullOrEmpty(puerto) ? ip : $"{ip},{puerto}";

            bool integrado = rbWinAuth.IsChecked == true;
            if (integrado)
                return $"Server={dataSource};Database=SchPosDB;Integrated Security=True;Encrypt=False;TrustServerCertificate=True;";
            else
            {
                string user = txtUsuarioCliente.Text.Trim();
                string pwd  = txtPasswordCliente.Password;
                return $"Server={dataSource};Database=SchPosDB;User Id={user};Password={pwd};Encrypt=False;TrustServerCertificate=True;";
            }
        }

        // ── Probar conexión ──────────────────────────────────────────────────────────

        private void btnTestear_Click(object sender, RoutedEventArgs e)
        {
            panelAvisoExpress.Visibility = Visibility.Collapsed;
            string cs = ObtenerCadenaSeleccionada();

            if (string.IsNullOrWhiteSpace(cs))
            {
                SetTestStatus("⚪", "Ingresá la IP del servidor", "El campo de IP no puede estar vacío.", "neutral");
                return;
            }

            string diagLocalDb = null;
            if (rbSoloPC.IsChecked == true)
            {
                SetTestStatus("⏳", "Preparando LocalDB...", "Creando/iniciando la instancia de SQL Server LocalDB en esta PC...", "pending");
                // Forzar un pase de render para que el mensaje de arriba se vea antes
                // de la llamada bloqueante de abajo (puede tardar unos segundos).
                Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Render);
                // Reintenta crear/arrancar la instancia acá mismo (no solo al abrir la app),
                // para que "Probar conexión" siempre tenga el diagnóstico más fresco posible.
                diagLocalDb = App.PrepararYDiagnosticarLocalDB();
            }

            SetTestStatus("⏳", "Probando conexión...", "Conectando al servidor de base de datos...", "pending");

            try
            {
                var builder = new SqlConnectionStringBuilder(cs);
                string dbName = builder.InitialCatalog;
                builder.InitialCatalog = "master";
                builder.ConnectTimeout = 8;

                using (var conn = new SqlConnection(builder.ConnectionString))
                {
                    conn.Open();
                    object result = new SqlCommand($"SELECT db_id(N'{dbName}')", conn).ExecuteScalar();
                    bool dbExiste = result != DBNull.Value;

                    string desc = dbExiste
                        ? $"✅ Base de datos '{dbName}' ya existe y será usada."
                        : $"✅ Servidor conectado. La base de datos '{dbName}' será creada automáticamente.";

                    SetTestStatus("✔", "¡Conexión exitosa!", desc, "success");
                    _cadenaTesteada = cs;
                    btnContinuar.IsEnabled = true;
                }
            }
            catch (Exception ex)
            {
                string ayuda = ObtenerAyudaError(ex.Message, diagLocalDb);
                string detalleTecnico = !string.IsNullOrWhiteSpace(diagLocalDb)
                    ? $"{ex.Message}\n\nDetalle técnico de LocalDB:\n{diagLocalDb}"
                    : ex.Message;
                SetTestStatus("✖", "No se pudo conectar", $"{detalleTecnico}\n\n{ayuda}", "error");
                _cadenaTesteada = null;
                btnContinuar.IsEnabled = false;

                if (rbServidor.IsChecked == true)
                    panelAvisoExpress.Visibility = Visibility.Visible;
            }
        }

        private string ObtenerAyudaError(string mensaje, string diagLocalDb = null)
        {
            if (rbSoloPC.IsChecked == true)
            {
                if (!string.IsNullOrWhiteSpace(diagLocalDb) &&
                    diagLocalDb.IndexOf("no se encontró", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return "El motor de SQL Server LocalDB no está instalado en esta PC. Instalalo desde:\n" +
                           "https://aka.ms/sqllocaldb\n\n" +
                           "Además, LocalDB necesita el 'Visual C++ Redistributable 2015-2022 (x64 y x86)' instalado.\n" +
                           "Descargalo desde: https://aka.ms/vs/17/release/vc_redist.x64.exe";
                }
                if (!string.IsNullOrWhiteSpace(diagLocalDb) &&
                    (diagLocalDb.IndexOf("process failed to start", StringComparison.OrdinalIgnoreCase) >= 0
                     || diagLocalDb.IndexOf("Error 50", StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    return "El motor de LocalDB está instalado pero el proceso de SQL Server no arranca.\n" +
                           "Causa más común: falta el 'Visual C++ Redistributable 2015-2022'.\n" +
                           "Instalá AMBAS versiones y reiniciá la PC:\n" +
                           "• https://aka.ms/vs/17/release/vc_redist.x64.exe\n" +
                           "• https://aka.ms/vs/17/release/vc_redist.x86.exe";
                }
                return "LocalDB no está instalado o no se puede iniciar.\n" +
                       "1. Instalá 'Visual C++ Redistributable 2015-2022' (x64 y x86): https://aka.ms/vs/17/release/vc_redist.x64.exe\n" +
                       "2. Reiniciá la PC y volvé a probar.\n" +
                       "3. Si persiste, instalá manualmente SQL Server Express LocalDB: https://aka.ms/sqllocaldb";
            }

            if (rbServidor.IsChecked == true)
                return "Verificá que SQL Server Express esté instalado y en ejecución en esta PC. También podés hacer clic en '🔍 Detectar' para buscar la instancia correcta.";

            // Cliente
            if (mensaje.Contains("network") || mensaje.Contains("Error 26") || mensaje.Contains("No se puede"))
            {
                string ip   = txtIPServidor.Text.Trim();
                string puertoActual = txtPuertoCliente.Text.Trim();
                bool usaInst = ip.Contains("\\");
                string extra = usaInst
                    ? (string.IsNullOrEmpty(puertoActual)
                        ? "\n• Estás usando instancia nombrada (IP\\Instancia) sin puerto: el servidor debe tener instalado el servicio 'SQL Server Browser' corriendo y el puerto UDP 1434 abierto en el firewall.\n  Si el servidor fue configurado con SCHPOS, en cambio, escribí '1433' en el campo Puerto para conectar directo sin depender de SQL Browser."
                        : "\n• Verificá que el puerto ingresado sea el correcto (por defecto SCHPOS deja el servidor escuchando en 1433).\n  Podés confirmarlo en el servidor con SQL Server Configuration Manager → Protocolos → TCP/IP → Puertos IP → IPAll.")
                    : "\n• El firewall del servidor debe permitir el puerto 1433 (TCP).\n• Si el servidor usa instancia nombrada (ej. IP\\SQLEXPRESS), escribí la IP y la instancia juntas en el campo de arriba.";
                return "No se encontró el servidor en la red. Verificá:\n• Que el servidor esté encendido.\n• Que ambas PCs estén en la misma red (mismo router/switch, sin VPN de por medio).\n• La IP ingresada sea correcta." + extra;
            }

            if (mensaje.Contains("Login failed") || mensaje.Contains("login"))
            {
                return rbSqlAuth.IsChecked == true
                    ? "Las credenciales son incorrectas, o el servidor no tiene habilitado el modo de autenticación mixta (SQL Server y Windows).\nSi el servidor fue configurado con 'Esta PC es el SERVIDOR' de SCHPOS, el modo mixto ya se habilita solo. Si el SQL Server fue instalado/configurado a mano, habilitalo desde SQL Server Management Studio → click derecho en el servidor → Propiedades → Seguridad → 'SQL Server and Windows Authentication mode', y reiniciá el servicio."
                    : "El usuario de Windows de esta PC no tiene acceso al SQL Server del servidor.\nEn un grupo de trabajo (workgroup, sin dominio), la autenticación Windows requiere que exista el MISMO usuario y contraseña de Windows en ambas PCs. Si no es así, probá con 'Usuario y contraseña SQL' en su lugar.";
            }

            return "Verificá los datos e intentá de nuevo.";
        }

        // ── Continuar ────────────────────────────────────────────────────────────────

        private void btnContinuar_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_cadenaTesteada))
            {
                MessageBox.Show("Primero probá la conexión correctamente.", "Atención", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                DatabaseService.ActualizarConexion(_cadenaTesteada);

                SetTestStatus("⏳", "Configurando base de datos...", "Creando tablas y datos iniciales...", "pending");

                var app = (App)Application.Current;
                typeof(App)
                    .GetMethod("InicializarBaseDeDatosCompleta",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                    ?.Invoke(app, new object[] { _cadenaTesteada });

                DatabaseService.InitializeDatabase();
                DatabaseService.AsegurarUsuarioAdminInicial();

                // Si configuró como SERVIDOR: habilitar TCP y generar archivo para clientes
                if (rbServidor.IsChecked == true)
                    ConfigurarServidorRed();

                SetTestStatus("✔", "¡Base de datos configurada!", "El sistema está listo para usar.", "success");
                MessageBox.Show("¡Configuración completada!\n\nEl sistema se iniciará ahora.", "Listo", MessageBoxButton.OK, MessageBoxImage.Information);
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                SetTestStatus("✖", "Error al configurar", ex.Message, "error");
            }
        }

        // ── Configuración automática del Servidor ────────────────────────────────────

        private void ConfigurarServidorRed()
        {
            // 1. Intentar habilitar TCP/IP en SQL Server Express (requiere permisos de admin)
            HabilitarTcpSqlServer();

            // 2. Generar archivo en el Escritorio con instrucciones para los clientes
            GenerarArchivoClientes();
        }

        private void HabilitarTcpSqlServer()
        {
            try
            {
                // Extraer nombre de instancia del campo (ej: .\SQLEXPRESS → SQLEXPRESS, .\MSSQLSERVER → MSSQLSERVER)
                string instancia = txtInstanciaServidor.Text.Trim();
                string nombreServicio;
                int bs = instancia.IndexOf('\\');
                if (bs >= 0)
                {
                    string inst = instancia.Substring(bs + 1).ToUpperInvariant();
                    nombreServicio = inst == "MSSQLSERVER" ? "MSSQLSERVER" : $"MSSQL${inst}";
                }
                else
                {
                    nombreServicio = "MSSQLSERVER"; // instancia por defecto
                }

                // Habilitar TCP/IP vía registro (no requiere SQLPS ni SMO)
                // y abrir firewall. Sin SQLPS: usamos sqlservermanager WMI o registro directo.
                //
                // Importante: además de habilitar TCP, forzamos un puerto FIJO (1433), incluso
                // para instancias con nombre (ej. SQLEXPRESS). Por defecto, una instancia con
                // nombre usa un puerto DINÁMICO que solo se puede resolver vía el servicio
                // "SQL Server Browser" (UDP 1434) — un requisito extra que no queda garantizado
                // ni documentado para el cliente. Al fijar el puerto, los clientes se conectan
                // directo por TCP 1433 sin depender de SQL Browser ni de UDP 1434 en el firewall.
                // También habilitamos el modo mixto de autenticación (SQL Server y Windows) para
                // que la opción "Usuario y contraseña SQL" del asistente funcione sin pasos manuales.
                string script = $@"
# Habilitar TCP/IP, fijar puerto 1433 y modo de autenticación mixto en SQL Server
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

# Abrir puerto 1433 en Firewall (sin duplicar)
$ruleName = 'SCHPOS-SQL-1433'
$existe = netsh advfirewall firewall show rule name=$ruleName 2>&1
if ($existe -match 'No rules match') {{
    netsh advfirewall firewall add rule name=$ruleName protocol=TCP dir=in action=allow localport=1433 | Out-Null
}}

# Reiniciar servicio SQL (nombre detectado desde instancia) para aplicar todos los cambios
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
                    Verb            = "runas",
                    UseShellExecute = true,
                };
                using (var p = Process.Start(psi))
                    p?.WaitForExit(15000);
            }
            catch { /* No bloquear si falla — el usuario puede habilitarlo manualmente */ }
        }

        private void GenerarArchivoClientes()
        {
            try
            {
                string instancia  = txtInstanciaServidor.Text.Trim();
                string ipServidor = ObtenerIPRed(); // IP de interfaz con gateway

                // Para clientes: reemplazar . / localhost por la IP real de red
                string servidorParaClientes = instancia
                    .Replace(".\\",        $"{ipServidor}\\")
                    .Replace("localhost\\", $"{ipServidor}\\");
                if (servidorParaClientes == "." || servidorParaClientes.ToLower() == "localhost")
                    servidorParaClientes = ipServidor;

                // Determinar si es instancia nombrada o IP:puerto.
                // SCHPOS fija el puerto de SQL Server en 1433 (ver HabilitarTcpSqlServer), así que
                // los clientes siempre pueden usar ese puerto explícitamente y conectar directo,
                // sin depender del servicio "SQL Server Browser".
                bool tieneInstancia = servidorParaClientes.Contains("\\");
                string dataSourceClientes = $"{servidorParaClientes},1433";
                string connectionString = $"Server={dataSourceClientes};Database=SchPosDB;Integrated Security=True;Encrypt=False;TrustServerCertificate=True;";

                // Paso 3 varía según instancia nombrada o solo IP
                string paso3 = tieneInstancia
                    ? $"   IP o nombre del servidor: {servidorParaClientes}\n   Puerto: 1433\n   (SCHPOS ya dejó el servidor escuchando en ese puerto fijo)"
                    : $"   IP del servidor: {ipServidor}\n   Puerto: 1433 (por defecto)";

                string contenido = $@"=== SCHPOS — Configuración de conexión para PCs de la red ===
Generado: {DateTime.Now:dd/MM/yyyy HH:mm}
Servidor: {ipServidor}  |  Instancia SQL: {instancia}

------------------------------------------------------------
PASOS PARA CADA PC CLIENTE:
------------------------------------------------------------
1. Instalá SCHPOS en la PC cliente.
2. Al abrir por primera vez, elegí:
   '🌐 Conectarme a otro servidor (soy cliente)'
3. En el campo 'IP o nombre del servidor', ingresá:

{paso3}

4. Autenticación: Windows (sin contraseña) si ambas PCs están en el mismo grupo/dominio.
   Si no, usá 'Usuario y contraseña SQL' con las credenciales de SQL Server.
5. Presioná 'Probar conexión'. Si funciona, presioná 'Continuar'.

------------------------------------------------------------
CADENA DE CONEXIÓN COMPLETA (para usuarios avanzados):
------------------------------------------------------------
{connectionString}

------------------------------------------------------------
REQUISITOS EN EL SERVIDOR ({ipServidor} — ESTA PC):
------------------------------------------------------------
✅ TCP/IP habilitado en SQL Server        (SCHPOS lo hizo automáticamente)
✅ Puerto fijo 1433 (sin puerto dinámico) (SCHPOS lo hizo automáticamente)
✅ Puerto 1433 abierto en Firewall        (SCHPOS creó la regla 'SCHPOS-SQL-1433')
✅ Modo de autenticación mixto habilitado (SCHPOS lo hizo automáticamente,
                                            necesario solo si algún cliente usa
                                            'Usuario y contraseña SQL')
✅ SQL Server Express en ejecución        (servicio Windows activo)

Si los clientes no pueden conectar, verificá manualmente en ESTA PC:
  • SQL Server Configuration Manager
    → Configuración de red de SQL Server → Protocolos para {instancia.Replace(".\\","").Replace("localhost\\","")}
    → TCP/IP: Habilitado → Propiedades → pestaña 'Direcciones IP' → IPAll
    → Puertos TCP dinámicos: (vacío)   Puerto TCP: 1433
  • Panel de control → Firewall de Windows
    → Reglas de entrada → 'SCHPOS-SQL-1433' → Acción: Permitir
  • Que ambas PCs estén en la MISMA red (mismo router, sin VPN de por medio)
  • Que esta PC no esté en modo 'suspender' o 'hibernar' (debe estar
    encendida y con SCHPOS instalado; no hace falta tenerlo abierto,
    solo que el servicio de SQL Server esté corriendo)

------------------------------------------------------------
¿Problemas?  Soporte: info@schettini.com.ar
------------------------------------------------------------
";

                string escritorio = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                string archivo    = Path.Combine(escritorio, "SCHPOS-Configuracion-Clientes.txt");
                File.WriteAllText(archivo, contenido, System.Text.Encoding.UTF8);

                // Abrir el archivo para que el usuario lo vea inmediatamente
                try { Process.Start(new ProcessStartInfo(archivo) { UseShellExecute = true }); } catch { }

                MessageBox.Show(
                    $"Se generó el archivo con los datos de conexión para las PCs clientes:\n\n{archivo}\n\n" +
                    "El archivo se abrió automáticamente. Compartilo con quien configure las otras PCs.",
                    "Archivo para clientes generado",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch { /* No crítico */ }
        }

        /// <summary>
        /// Devuelve la IP de la interfaz de red con gateway configurado (la IP "real" de LAN).
        /// Evita devolver IPs de VPN, Hyper-V o loopback.
        /// </summary>
        private static string ObtenerIPRed()
        {
            try
            {
                // Preferir interfaz activa con gateway por defecto (la que sale a Internet/LAN)
                foreach (var ni in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus != System.Net.NetworkInformation.OperationalStatus.Up) continue;
                    if (ni.NetworkInterfaceType == System.Net.NetworkInformation.NetworkInterfaceType.Loopback) continue;

                    var props = ni.GetIPProperties();
                    // Tiene gateway configurado = interfaz de red "real"
                    if (props.GatewayAddresses.Count == 0) continue;

                    foreach (var ua in props.UnicastAddresses)
                    {
                        if (ua.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                        {
                            string ip = ua.Address.ToString();
                            if (!ip.StartsWith("127.") && !ip.StartsWith("169.254."))
                                return ip;
                        }
                    }
                }

                // Fallback: primera IPv4 no loopback (cualquier interfaz)
                var entry = System.Net.Dns.GetHostEntry(System.Net.Dns.GetHostName());
                foreach (var addr in entry.AddressList)
                {
                    if (addr.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork &&
                        !addr.ToString().StartsWith("127."))
                        return addr.ToString();
                }
            }
            catch { }
            return "VERIFICAR-IP-SERVIDOR";
        }

        // ── Helpers UI ───────────────────────────────────────────────────────────────

        private void SetTestStatus(string icon, string titulo, string desc, string tone)
        {
            iconTest.Text          = icon;
            lblTestTitle.Text      = titulo;
            lblTestDesc.Text       = desc;

            Brush accent;
            switch (tone)
            {
                case "success": accent = BrushFromResource("SuccessColor");  break;
                case "error":   accent = BrushFromResource("DangerColor");   break;
                case "pending": accent = BrushFromResource("WarningColor");  break;
                default:        accent = BrushFromResourceDyn("TextSecondary"); break;
            }
            iconTest.Foreground    = accent;
            lblTestTitle.Foreground = accent;
        }

        private static Brush BrushFromResource(string key) =>
            Application.Current?.TryFindResource(key) as Brush ?? Brushes.Gray;

        private static Brush BrushFromResourceDyn(string key) =>
            Application.Current?.TryFindResource(key) as Brush ?? Brushes.DimGray;
    }
}
