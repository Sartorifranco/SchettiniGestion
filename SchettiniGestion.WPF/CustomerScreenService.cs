using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using SchettiniGestion;
using System.Data;
// CORRECCION: Importamos Media para que "Brush" sea el correcto
using System.Windows.Media;

namespace SchettiniGestion.WPF
{
    public static class CustomerScreenService
    {
        private static VisorClienteWindow _visor;
        public static event Action<string> OnClienteEligioPago;

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

        public static void Iniciar()
        {
            if (!VisorEstaHabilitado()) return;

            if (Screen.AllScreens.Length <= 1)
                return;

            var pantallaSecundaria = Screen.AllScreens.FirstOrDefault(s => !s.Primary) ?? Screen.AllScreens[1];
            var area = pantallaSecundaria.WorkingArea;

            // Si el usuario cerró el visor, la instancia queda inválida: no se puede volver a Show().
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
                _visor.WindowState = System.Windows.WindowState.Normal;
                _visor.Top = area.Top;
                _visor.Left = area.Left;
                _visor.Width = area.Width;
                _visor.Height = area.Height;
                _visor.WindowState = System.Windows.WindowState.Maximized;

                if (_visor.IsVisible)
                    _visor.Activate();
                else
                    _visor.Show();
            }
            catch (InvalidOperationException)
            {
                // Defensa extra: recrear y reintentar una vez.
                _visor = null;
                var ventana = new VisorClienteWindow();
                ventana.Closed += (_, __) =>
                {
                    if (ReferenceEquals(_visor, ventana))
                        _visor = null;
                };
                ventana.OnOpcionSeleccionada += (opcion) => OnClienteEligioPago?.Invoke(opcion);
                _visor = ventana;
                _visor.Top = area.Top;
                _visor.Left = area.Left;
                _visor.Width = area.Width;
                _visor.Height = area.Height;
                _visor.WindowState = System.Windows.WindowState.Maximized;
                _visor.Show();
            }
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

        public static void PantallaQREstatico(decimal monto)
        {
            if (!VisorEstaHabilitado()) return;
            if (_visor != null && _visor.IsLoaded) _visor.MostrarQREstatico(monto);
        }

        public static bool HayVisorActivo()
        {
            return VisorEstaHabilitado() && _visor != null && _visor.IsLoaded && _visor.IsVisible;
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

        // CORRECCION: Aseguramos que Brush sea System.Windows.Media.Brush
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

        /// <summary>Recarga publicidades del carrusel en la ventana abierta, sin reiniciar el visor.</summary>
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

        /// <summary>Reaplica la configuración de visor (pantalla única vs. cliente en segundo monitor).</summary>
        public static void RefrescarSegunConfiguracion()
        {
            if (!VisorEstaHabilitado())
            {
                Cerrar();
                return;
            }

            if (_visor != null && _visor.IsLoaded)
            {
                RecargarPublicidades();
                return;
            }

            Iniciar();
            Resetear();
        }
    }
}
