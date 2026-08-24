using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Interop;
using System.Windows.Media;
using SchettiniGestion;

namespace SchettiniGestion.WPF
{
    public static class CustomerScreenService
    {
        private static VisorClienteWindow _visor;
        public static event Action<string> OnClienteEligioPago;

        private const uint SWP_SHOWWINDOW = 0x0040;
        private const uint SWP_NOZORDER = 0x0004;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        private static bool VisorEstaHabilitado()
        {
            if (!LicenseManager.TieneVisorCliente())
                return false;

            try
            {
                DataRow config = DatabaseService.GetConfiguracion();
                if (config != null && config.Table.Columns.Contains("UsaVisorCliente"))
                {
                    if (config["UsaVisorCliente"] == DBNull.Value) return false;
                    return Convert.ToBoolean(config["UsaVisorCliente"]);
                }
                return false;
            }
            catch { return false; }
        }

        /// <summary>Abre o cierra el visor según el combo (sin releer la base: evita que un flag viejo lo cierre).</summary>
        public static string AplicarModo(bool dosPantallas)
        {
            if (!dosPantallas)
            {
                Cerrar();
                return null;
            }

            if (!LicenseManager.TieneVisorCliente())
            {
                Cerrar();
                return "La pantalla cliente (segundo monitor) no está incluida en su licencia.";
            }

            IniciarForzado();

            if (Screen.AllScreens.Length < 2)
            {
                return "El visor del cliente se abrió. Windows solo ve un monitor (suele pasar si está en «Duplicar» y no en «Extender»).\n\n" +
                       "Pasá a Extender escritorio o arrastrá esa ventana al otro monitor.";
            }

            return null;
        }

        public static void Iniciar()
        {
            if (!VisorEstaHabilitado()) return;
            IniciarForzado();
        }

        private static void IniciarForzado()
        {
            if (_visor != null && !_visor.IsLoaded)
                _visor = null;

            if (_visor == null)
            {
                var ventana = new VisorClienteWindow();
                ventana.Closed += (_, __) =>
                {
                    if (ReferenceEquals(_visor, ventana))
                        _visor = null;
                };
                ventana.OnOpcionSeleccionada += (opcion) => OnClienteEligioPago?.Invoke(opcion);
                _visor = ventana;
            }

            try
            {
                MostrarVisor(_visor);
            }
            catch (InvalidOperationException)
            {
                _visor = null;
                var ventana = new VisorClienteWindow();
                ventana.Closed += (_, __) =>
                {
                    if (ReferenceEquals(_visor, ventana))
                        _visor = null;
                };
                ventana.OnOpcionSeleccionada += (opcion) => OnClienteEligioPago?.Invoke(opcion);
                _visor = ventana;
                MostrarVisor(_visor);
            }
        }

        private static void MostrarVisor(VisorClienteWindow visor)
        {
            visor.WindowStartupLocation = WindowStartupLocation.Manual;
            visor.WindowState = WindowState.Normal;
            visor.WindowStyle = WindowStyle.None;
            visor.ResizeMode = ResizeMode.NoResize;

            var secundaria = Screen.AllScreens.FirstOrDefault(s => !s.Primary);
            if (secundaria == null && Screen.AllScreens.Length > 1)
                secundaria = Screen.AllScreens[1];

            if (secundaria != null)
            {
                UbicarEnPantalla(visor, secundaria);
            }
            else
            {
                visor.Width = 1024;
                visor.Height = 768;
                visor.Left = 80;
                visor.Top = 80;
            }

            if (visor.IsVisible)
                visor.Activate();
            else
                visor.Show();

            if (secundaria != null)
                UbicarEnPantalla(visor, secundaria);
        }

        private static void UbicarEnPantalla(Window ventana, Screen pantalla)
        {
            ventana.WindowState = WindowState.Normal;
            var hwnd = new WindowInteropHelper(ventana).EnsureHandle();
            var b = pantalla.Bounds;
            SetWindowPos(hwnd, IntPtr.Zero, b.X, b.Y, b.Width, b.Height, SWP_SHOWWINDOW | SWP_NOZORDER);
        }

        public static void Actualizar(List<FacturaItem> items, decimal total)
        {
            if (!VisorEstaHabilitado()) return;
            if (_visor != null && _visor.IsLoaded) _visor.ActualizarGrilla(items.ToList(), total);
        }

        public static void PantallaPagos()
        {
            if (!VisorEstaHabilitado()) return;
            if (_visor != null && _visor.IsLoaded) _visor.MostrarSeleccionPago();
        }

        public static void PantallaQR(string qrData, decimal monto)
        {
            if (!VisorEstaHabilitado()) return;
            if (_visor != null && _visor.IsLoaded) _visor.MostrarQR(qrData, monto);
        }

        public static void PantallaPoint(decimal monto, string mensaje)
        {
            if (!VisorEstaHabilitado()) return;
            if (_visor != null && _visor.IsLoaded) _visor.MostrarPoint(monto, mensaje);
        }

        public static void ActualizarEstadoPoint(string mensaje, Brush color)
        {
            if (!VisorEstaHabilitado()) return;
            if (_visor != null && _visor.IsLoaded) _visor.ActualizarEstadoPoint(mensaje, color);
        }

        public static void ActualizarMensajeQR(string mensaje, Brush color)
        {
            if (!VisorEstaHabilitado()) return;
            if (_visor != null && _visor.IsLoaded) _visor.ActualizarEstadoQR(mensaje, color);
        }

        public static void PantallaGracias()
        {
            if (!VisorEstaHabilitado()) return;
            if (_visor != null && _visor.IsLoaded) _visor.MostrarGracias();
        }

        public static void Resetear()
        {
            if (!VisorEstaHabilitado()) return;
            if (_visor != null && _visor.IsLoaded) _visor.Reiniciar();
        }

        public static void RecargarPublicidades()
        {
            if (!VisorEstaHabilitado()) return;
            if (_visor != null && _visor.IsLoaded)
                _visor.RecargarPublicidades();
        }

        public static void Cerrar()
        {
            if (_visor == null) return;
            var ventana = _visor;
            _visor = null;
            try
            {
                if (ventana.IsLoaded)
                    ventana.Close();
            }
            catch { }
        }

        public static void RefrescarSegunConfiguracion()
        {
            if (!VisorEstaHabilitado())
            {
                Cerrar();
                return;
            }

            IniciarForzado();
            Resetear();
        }
    }
}
