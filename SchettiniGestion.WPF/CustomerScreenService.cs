using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using SchettiniGestion;
using System.Data;

namespace SchettiniGestion.WPF
{
    public static class CustomerScreenService
    {
        private static VisorClienteWindow _visor;
        public static event Action<string> OnClienteEligioPago;

        // --- FUNCIÓN DE VERIFICACIÓN DE CONFIGURACIÓN ---
        private static bool VisorEstaHabilitado()
        {
            try
            {
                DataRow config = DatabaseService.GetConfiguracion();
                if (config != null && config.Table.Columns.Contains("UsaVisorCliente"))
                {
                    // Convertimos el INTEGER (1 o 0) a bool. Si es DBNull, asumimos 'true' (habilitado por defecto)
                    return config["UsaVisorCliente"] != DBNull.Value ? Convert.ToBoolean(config["UsaVisorCliente"]) : true;
                }
                return true; // Falla segura: si no encontramos la columna, asumimos que está habilitado.
            }
            catch
            {
                // Si hay algún error en la base de datos o al leer, deshabilitamos la pantalla para no romper la aplicación.
                return false;
            }
        }
        // ------------------------------------------------

        public static void Iniciar()
        {
            if (!VisorEstaHabilitado()) // ¡VERIFICACIÓN CRÍTICA!
            {
                return;
            }

            // Continuar solo si está habilitado y hay más de una pantalla.
            if (Screen.AllScreens.Length > 1)
            {
                var pantallaSecundaria = Screen.AllScreens.FirstOrDefault(s => !s.Primary) ?? Screen.AllScreens[1];
                var area = pantallaSecundaria.WorkingArea;

                if (_visor == null)
                {
                    _visor = new VisorClienteWindow();
                    _visor.OnOpcionSeleccionada += (opcion) => OnClienteEligioPago?.Invoke(opcion);
                }

                _visor.Top = area.Top;
                _visor.Left = area.Left;
                _visor.Width = area.Width;
                _visor.Height = area.Height;
                _visor.WindowState = System.Windows.WindowState.Maximized;
                _visor.Show();
            }
        }

        // --- MÉTODO CLAVE: DEBE COINCIDIR CON LA VENTANA ---
        public static void Actualizar(List<FacturaItem> items, decimal total)
        {
            if (!VisorEstaHabilitado()) return; // Salir si está deshabilitado
            if (_visor != null && _visor.IsLoaded)
            {
                // Pasamos una copia de la lista (.ToList()) para evitar conflictos
                _visor.ActualizarGrilla(items.ToList(), total);
            }
        }
        // --------------------------------------------------

        public static void PantallaPagos()
        {
            if (!VisorEstaHabilitado()) return; // Salir si está deshabilitado
            if (_visor != null && _visor.IsLoaded) _visor.MostrarSeleccionPago();
        }

        public static void PantallaQR(decimal monto)
        {
            if (!VisorEstaHabilitado()) return; // Salir si está deshabilitado
            if (_visor != null && _visor.IsLoaded) _visor.MostrarQR(monto);
        }

        public static void PantallaGracias()
        {
            if (!VisorEstaHabilitado()) return; // Salir si está deshabilitado
            if (_visor != null && _visor.IsLoaded) _visor.MostrarGracias();
        }

        public static void Resetear()
        {
            if (!VisorEstaHabilitado()) return; // Salir si está deshabilitado
            if (_visor != null && _visor.IsLoaded) _visor.Reiniciar();
        }

        public static void Cerrar()
        {
            // No requiere chequeo de habilitación, siempre debe poder cerrar si está abierto.
            if (_visor != null) { _visor.Close(); _visor = null; }
        }
    }
}