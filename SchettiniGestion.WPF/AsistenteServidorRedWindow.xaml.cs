using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using SchettiniGestion;

namespace SchettiniGestion.WPF
{
    /// <summary>
    /// Asistente paso a paso (Siguiente) para dejar esta PC como servidor de red.
    /// Reemplaza la cascada de MessageBox.
    /// </summary>
    public partial class AsistenteServidorRedWindow : Window
    {
        private int _paso; // 0..3
        private bool _expressListo;
        private bool _tcpListo;
        private bool _finalizado;
        private string _passRed;
        private string _ip;
        private string _servidorClientes;
        private string _rutaGuia;
        private bool _migracionHecha;
        private bool _ocupado;
        private string _avisoTcpPendiente;

        public bool ServidorConfiguradoOk { get; private set; }

        public AsistenteServidorRedWindow()
        {
            InitializeComponent();
            MostrarPaso(0);
        }

        private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed) DragMove();
        }

        private void MostrarPaso(int paso)
        {
            _paso = paso;
            panelPaso0.Visibility = paso == 0 ? Visibility.Visible : Visibility.Collapsed;
            panelPaso1.Visibility = paso == 1 ? Visibility.Visible : Visibility.Collapsed;
            panelPaso2.Visibility = paso == 2 ? Visibility.Visible : Visibility.Collapsed;
            panelPaso3.Visibility = paso == 3 ? Visibility.Visible : Visibility.Collapsed;

            txtPasoHeader.Text = "Paso " + (paso + 1) + " de 4";
            btnAtras.Visibility = (paso > 0 && !_finalizado) ? Visibility.Visible : Visibility.Collapsed;
            btnCancelar.Visibility = _finalizado ? Visibility.Collapsed : Visibility.Visible;

            if (paso == 3 && _finalizado)
                btnSiguiente.Content = "Finalizar";
            else if (paso == 3)
                btnSiguiente.Content = "Preparar datos";
            else if (paso == 1 && !_expressListo)
                btnSiguiente.Content = "Instalar / continuar";
            else if (paso == 2 && !_tcpListo)
                btnSiguiente.Content = "Abrir puerto (Sí en Windows)";
            else
                btnSiguiente.Content = "Siguiente";

            if (paso == 1)
                ActualizarEstadoExpressUi();
        }

        private void ActualizarEstadoExpressUi()
        {
            _expressListo = SqlExpressInstaller.PuedeConectarExpress();
            string extras = SqlServerNetworkSetup.AdvertenciaInstanciasExtra();
            if (_expressListo)
            {
                txtEstadoExpress.Text = "✓ SQL Express (.\\SQLEXPRESS) ya está listo en esta PC." +
                    (string.IsNullOrEmpty(extras) ? "" : "\n\n⚠ " + extras);
            }
            else
            {
                txtEstadoExpress.Text =
                    "✗ SQL Express no está instalado o no responde. Al continuar se descargará e instalará.\n" +
                    "Instalá una sola instancia, nombre SQLEXPRESS. No instales SQLEXPRESS01 si no hace falta.";
            }
        }

        private void btnCancelar_Click(object sender, RoutedEventArgs e)
        {
            if (_ocupado) return;
            DialogResult = false;
            Close();
        }

        private void btnAtras_Click(object sender, RoutedEventArgs e)
        {
            if (_ocupado || _finalizado) return;
            if (_paso > 0) MostrarPaso(_paso - 1);
        }

        private async void btnSiguiente_Click(object sender, RoutedEventArgs e)
        {
            if (_ocupado) return;

            if (_finalizado)
            {
                ServidorConfiguradoOk = true;
                DialogResult = true;
                Close();
                return;
            }

            try
            {
                _ocupado = true;
                btnSiguiente.IsEnabled = false;
                btnAtras.IsEnabled = false;
                btnCancelar.IsEnabled = false;
                ElevacionHelper.PedirConfirmacionUac = false; // el asistente ya explicó el UAC

                if (_paso == 0)
                {
                    MostrarPaso(1);
                    return;
                }

                if (_paso == 1)
                {
                    await EjecutarPasoExpressAsync();
                    if (_expressListo) MostrarPaso(2);
                    return;
                }

                if (_paso == 2)
                {
                    await EjecutarPasoTcpAsync();
                    if (_tcpListo) MostrarPaso(3);
                    return;
                }

                if (_paso == 3)
                {
                    await EjecutarPasoFinalAsync();
                }
            }
            finally
            {
                ElevacionHelper.PedirConfirmacionUac = true;
                _ocupado = false;
                btnSiguiente.IsEnabled = true;
                btnAtras.IsEnabled = !_finalizado;
                btnCancelar.IsEnabled = !_finalizado;
                MostrarPaso(_paso); // refresca textos de botones
            }
        }

        private async Task EjecutarPasoExpressAsync()
        {
            ActualizarEstadoExpressUi();
            if (_expressListo)
            {
                txtLogPaso1.Text = "Ya estaba instalado. Podés seguir.";
                return;
            }

            txtLogPaso1.Text = "Descargando / instalando SQL Express...\nCuando Windows pida permiso, tocá Sí.";
            string err = await Task.Run(() => SqlExpressInstaller.InstalarSilencioso(msg =>
                Dispatcher.Invoke(() => txtLogPaso1.Text = msg)));

            ActualizarEstadoExpressUi();
            if (!_expressListo)
            {
                txtLogPaso1.Text = (err ?? "No quedó instalado.") +
                    "\n\nPodés instalar Express a mano desde microsoft.com y volver a tocar Siguiente.";
                return;
            }

            txtLogPaso1.Text = "✓ SQL Express listo.";
        }

        private async Task EjecutarPasoTcpAsync()
        {
            txtLogPaso2.Text = "Pedí permiso a Windows (escudo) y configurando puerto 1433...";
            string err = await Task.Run(() => SqlServerNetworkSetup.HabilitarTcpYFirewall(SqlServerNetworkSetup.InstanciaLocal));
            if (err != null)
            {
                txtLogPaso2.Text = "✗ " + err + "\n\nReintentá con Siguiente y aceptá el Sí de Windows.";
                _tcpListo = false;
                return;
            }

            txtLogPaso2.Text = "Verificando que el puerto 1433 esté escuchando...";
            bool escucha = await Task.Run(() =>
            {
                SqlExpressInstaller.ServicioEnEjecucion();
                SqlServerNetworkSetup.EsperarPuertoTcp(1433, 30000);
                return SqlServerNetworkSetup.PuertoTcpEscuchando(1433);
            });

            if (!escucha)
            {
                _tcpListo = false;
                txtLogPaso2.Text =
                    "✗ El puerto 1433 no responde todavía.\n\n" +
                    "No se puede continuar como servidor de red hasta que SQL Express escuche en TCP 1433.\n" +
                    "Reintentá con «Abrir puerto» y aceptá el Sí de Windows.";
                return;
            }

            txtLogPaso2.Text = "Puerto 1433 OK. Comprobando modo mixto (SQL tiene que aceptar el usuario schpos)...";
            string errMixto = await Task.Run(() => SqlServerNetworkSetup.AsegurarModoMixtoAplicado(msg =>
                Dispatcher.Invoke(() => txtLogPaso2.Text = msg)));
            if (errMixto != null)
            {
                _tcpListo = false;
                txtLogPaso2.Text = "✗ " + errMixto +
                    "\n\nNo pases a «Preparar datos» todavía. Tocá de nuevo «Abrir puerto» después de parar y arrancar SQLEXPRESS.";
                return;
            }

            _tcpListo = true;
            string extras = SqlServerNetworkSetup.AdvertenciaInstanciasExtra();
            txtLogPaso2.Text = "✓ Puerto 1433 escuchando, firewall listo y modo mixto aplicado (SoloWindows = 0)." +
                (string.IsNullOrEmpty(extras) ? "" : "\n\n⚠ " + extras);
        }

        private async Task EjecutarPasoFinalAsync()
        {
            txtLogPaso3.Text = "Migrando datos y creando usuario para clientes...";
            txtCredenciales.Text = "";
            if (pnlCredencialesFinal != null)
                pnlCredencialesFinal.Visibility = Visibility.Collapsed;

            string err = await Task.Run(() => CompletarServidor());
            if (err != null)
            {
                txtLogPaso3.Text = "✗ " + err;
                return;
            }

            _finalizado = true;
            ServidorConfiguradoOk = true;
            SqlServerNetworkSetup.GuardarModoRed(SqlServerNetworkSetup.ModoServidor);
            _avisoTcpPendiente = null;
            txtLogPaso3.Text =
                (_migracionHecha ? "✓ Datos migrados desde LocalDB\n" : "✓ Base en Express lista\n") +
                "✓ Usuario SQL schpos creado\n" +
                "✓ Conexión guardada\n" +
                "✓ Guía en Escritorio\n" +
                "✓ TCP 1433 OK (servidor de red listo)";

            if (txtServidorFinal != null) txtServidorFinal.Text = _servidorClientes ?? "";
            if (txtPuertoFinal != null) txtPuertoFinal.Text = "1433";
            if (txtUsuarioFinal != null) txtUsuarioFinal.Text = SqlServerNetworkSetup.UsuarioRedDefault;
            if (txtPasswordFinal != null) txtPasswordFinal.Text = _passRed ?? "";
            if (txtRutaGuia != null)
                txtRutaGuia.Text = "Archivo en el Escritorio: " + (_rutaGuia ?? "SCHPOS-Configuracion-Clientes.txt");

            if (pnlCredencialesFinal != null)
                pnlCredencialesFinal.Visibility = Visibility.Visible;

            if (bdrAvisoTcp != null)
                bdrAvisoTcp.Visibility = Visibility.Collapsed;

            btnSiguiente.Content = "Finalizar";
            btnAtras.Visibility = Visibility.Collapsed;
            btnCancelar.Visibility = Visibility.Collapsed;
        }

        private void btnCopiarServidor_Click(object sender, RoutedEventArgs e) => CopiarAlPortapapeles(_servidorClientes, "Servidor");
        private void btnCopiarPuerto_Click(object sender, RoutedEventArgs e) => CopiarAlPortapapeles("1433", "Puerto");
        private void btnCopiarUsuario_Click(object sender, RoutedEventArgs e) => CopiarAlPortapapeles(SqlServerNetworkSetup.UsuarioRedDefault, "Usuario");
        private void btnCopiarPassword_Click(object sender, RoutedEventArgs e) => CopiarAlPortapapeles(_passRed, "Contraseña");

        private void btnCopiarTodo_Click(object sender, RoutedEventArgs e)
        {
            string bloque =
                "SCHPOS — datos para conectar caja cliente\n" +
                "Servidor: " + (_servidorClientes ?? "") + "\n" +
                "Puerto: 1433\n" +
                "Usuario: " + SqlServerNetworkSetup.UsuarioRedDefault + "\n" +
                "Contraseña: " + (_passRed ?? "") + "\n" +
                "Base: SchPosDB";
            CopiarAlPortapapeles(bloque, "Datos de conexión");
        }

        private void btnAbrirGuia_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(_rutaGuia) && File.Exists(_rutaGuia))
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = _rutaGuia,
                        UseShellExecute = true
                    });
                }
                else
                {
                    ModernMessageBox.Show("No se encontró el archivo de guía en el Escritorio.",
                        "Guía", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                ModernMessageBox.Show("No se pudo abrir la guía:\n" + ex.Message, "Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private static void CopiarAlPortapapeles(string texto, string etiqueta)
        {
            try
            {
                if (string.IsNullOrEmpty(texto))
                {
                    ModernMessageBox.Show("No hay " + etiqueta.ToLowerInvariant() + " para copiar todavía.",
                        "Copiar", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                Clipboard.SetText(texto);
                ModernMessageBox.Show(etiqueta + " copiado al portapapeles.\nYa podés pegarlo en WhatsApp, mail o en la otra PC.",
                    "Copiado", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                ModernMessageBox.Show("No se pudo copiar: " + ex.Message, "Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private string CompletarServidor()
        {
            try
            {
                string errMixto = SqlServerNetworkSetup.AsegurarModoMixtoAplicado();
                if (errMixto != null)
                    return errMixto;

                string errMig = SqlServerNetworkSetup.MigrarLocalDbHaciaExpress(out _migracionHecha);
                if (errMig != null) return "Migración: " + errMig;

                // Misma clave si ya se publicó o si este asistente ya generó una (no diez claves distintas).
                string errLogin = SqlServerNetworkSetup.AsegurarLoginRed(out _passRed, _passRed);
                if (errLogin != null) return "Usuario SQL: " + errLogin;

                string errPrueba;
                if (!DatabaseService.ProbarNuevaConexion(SqlServerNetworkSetup.InstanciaLocal, "", true, null, null, out errPrueba))
                    return "Express no acepta conexión local: " + errPrueba;

                // TCP obligatorio: sin 1433 + login SQL no se marca servidor de red como listo.
                string avisoTcp;
                bool tcpOk = SqlServerNetworkSetup.AsegurarYProbarTcpSqlAuth(
                    SqlServerNetworkSetup.UsuarioRedDefault, _passRed, out avisoTcp);
                if (!tcpOk)
                {
                    _avisoTcpPendiente = avisoTcp;
                    int? solo = SqlServerNetworkSetup.LeerIsIntegratedSecurityOnly();
                    if (solo == 1)
                    {
                        return "SQL todavía no acepta logins SQL (SoloWindows = 1). " +
                               "No reintentés «Preparar datos» diez veces: queda un schpos con otra clave.\n\n" +
                               (avisoTcp ?? "");
                    }
                    return "El puerto TCP 1433 no responde: no se puede marcar esta PC como servidor de red.\n\n" +
                           (avisoTcp ?? "") +
                           "\n\nSi SoloWindows ya es 0, reintentá una sola vez.";
                }

                if (!DatabaseService.GuardarNuevaConexion(SqlServerNetworkSetup.InstanciaLocal, "", true, null, null))
                    return "No se pudo guardar conexion.cfg";

                _ip = SqlServerNetworkSetup.ObtenerIPRed();
                _servidorClientes = _ip + "\\SQLEXPRESS";
                SqlServerNetworkSetup.GuardarCredencialesClientes(_servidorClientes, "1433",
                    SqlServerNetworkSetup.UsuarioRedDefault, _passRed);
                _rutaGuia = SqlServerNetworkSetup.GenerarArchivoClientes(
                    SqlServerNetworkSetup.InstanciaLocal,
                    SqlServerNetworkSetup.UsuarioRedDefault,
                    _passRed,
                    _ip,
                    _servidorClientes);

                _avisoTcpPendiente = null;
                return null;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        /// <summary>Abre el asistente. Retorna true si el servidor quedó configurado.</summary>
        public static bool Ejecutar(Window owner = null)
        {
            var w = new AsistenteServidorRedWindow();
            if (owner != null) w.Owner = owner;
            bool? ok = w.ShowDialog();
            return ok == true && w.ServidorConfiguradoOk;
        }
    }
}
