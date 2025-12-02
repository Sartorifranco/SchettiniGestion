using System;
using System.Collections.Generic; // Necesario para List
using System.Linq;
using System.Windows.Forms;
using SchettiniGestion; // Para ver FacturaItem

namespace SchettiniGestion.WPF
{
    public static class CustomerScreenService
    {
        private static VisorClienteWindow _visor;
        public static event Action<string> OnClienteEligioPago;

        public static void Iniciar()
        {
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
            if (_visor != null && _visor.IsLoaded)
            {
                // Pasamos una copia de la lista (.ToList()) para evitar conflictos
                _visor.ActualizarGrilla(items.ToList(), total);
            }
        }
        // --------------------------------------------------

        public static void PantallaPagos()
        {
            if (_visor != null && _visor.IsLoaded) _visor.MostrarSeleccionPago();
        }

        public static void PantallaQR(decimal monto)
        {
            if (_visor != null && _visor.IsLoaded) _visor.MostrarQR(monto);
        }

        public static void PantallaGracias()
        {
            if (_visor != null && _visor.IsLoaded) _visor.MostrarGracias();
        }

        public static void Resetear()
        {
            if (_visor != null && _visor.IsLoaded) _visor.Reiniciar();
        }

        public static void Cerrar()
        {
            if (_visor != null) { _visor.Close(); _visor = null; }
        }
    }
}