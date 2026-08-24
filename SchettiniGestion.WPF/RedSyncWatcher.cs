using System;
using System.Windows;
using System.Windows.Threading;
using SchettiniGestion;

namespace SchettiniGestion.WPF
{
    /// <summary>Módulo que recarga listas cuando otra caja cambió datos en SQL.</summary>
    public interface ISincronizableEnRed
    {
        void AplicarCambioRed(string entidad);
    }

    /// <summary>
    /// Pregunta a SQL la versión de RedSync ~250 ms. No es un push milimétrico
    /// (eso pediría un bus extra); en LAN se siente al instante.
    /// </summary>
    internal static class RedSyncWatcher
    {
        private static DispatcherTimer _timer;
        private static long _version = -1;
        private static int _ticksHeartbeat;
        private static bool _busy;
        private static bool _pendiente;
        private static string _entidadPendiente = "";

        public static event Action<string> Cambio;

        public static void Iniciar()
        {
            if (_timer != null) return;
            DatabaseService.AsegurarEsquemaRed();
            RegistrarHeartbeat();
            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(250)
            };
            _timer.Tick += Timer_Tick;
            _timer.Start();
        }

        public static void Detener()
        {
            if (_timer == null) return;
            _timer.Stop();
            _timer.Tick -= Timer_Tick;
            _timer = null;
        }

        private static void Timer_Tick(object sender, EventArgs e)
        {
            _ticksHeartbeat++;
            if (_ticksHeartbeat >= 20)
            {
                _ticksHeartbeat = 0;
                RegistrarHeartbeat();
            }

            if (!DatabaseService.TryObtenerVersionRed(out long v, out string entidad))
                return;

            if (_version < 0)
            {
                _version = v;
                return;
            }

            if (v == _version) return;
            _version = v;
            Disparar(entidad);
        }

        private static void Disparar(string entidad)
        {
            if (_busy)
            {
                _pendiente = true;
                _entidadPendiente = entidad;
                return;
            }

            _busy = true;
            try
            {
                do
                {
                    _pendiente = false;
                    string actual = entidad;
                    try { Cambio?.Invoke(actual); }
                    catch { }
                    if (_pendiente)
                        entidad = _entidadPendiente;
                } while (_pendiente);
            }
            finally
            {
                _busy = false;
            }
        }

        public static void RegistrarHeartbeat()
        {
            try
            {
                string modo = SqlServerNetworkSetup.LeerModoRed();
                DatabaseService.RegistrarPuestoRed(
                    PuestoLocalService.IdPuesto,
                    PuestoLocalService.Nombre,
                    Environment.MachineName,
                    modo);
            }
            catch { }
        }

        public static bool HayVentanaVisible<T>() where T : Window
        {
            var app = Application.Current;
            if (app == null) return false;
            foreach (Window w in app.Windows)
            {
                if (w is T && w.IsVisible) return true;
            }
            return false;
        }
    }
}
