using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using SchettiniGestion;

namespace SchettiniGestion.WPF
{
    /// <summary>
    /// Reintenta CAE en segundo plano para tickets internos guardados tras un fallo de ARCA.
    /// No cobra de nuevo y no bloquea la caja.
    /// </summary>
    public static class AfipColaService
    {
        private static readonly object _lock = new object();
        private static DispatcherTimer _timer;
        public static event EventHandler EstadoCambiado;

        public static void Iniciar()
        {
            if (_timer != null) return;
            _timer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(3) };
            _timer.Tick += async (s, e) => await ProcesarPendientesAsync();
            _timer.Start();
            Task.Run(async () =>
            {
                await Task.Delay(12000);
                await ProcesarPendientesAsync();
            });
        }

        public static void Detener()
        {
            if (_timer == null) return;
            _timer.Stop();
            _timer = null;
        }

        public static async Task ProcesarPendientesAsync()
        {
            if (!Monitor.TryEnter(_lock)) return;
            try
            {
                for (int i = 0; i < 3; i++)
                {
                    var item = DatabaseService.TomarSiguienteAfipCola();
                    if (item == null || item.Items == null || item.Items.Count == 0)
                        break;
                    try
                    {
                        var r = await AfipService.ReintentarCaePendienteAsync(
                            item.TipoAfip,
                            item.PuntoVenta,
                            (double)item.Total,
                            item.CuitCliente,
                            item.Items,
                            item.CondicionIva);
                        if (r != null && r.Exito && !string.IsNullOrWhiteSpace(r.CAE))
                            DatabaseService.MarcarAfipColaExito(item.ColaID, item.FacturaID, r.CAE, r.Vencimiento, r.NumeroComprobante);
                        else
                            DatabaseService.MarcarAfipColaFallo(item.ColaID, r?.Error ?? "Sin respuesta de ARCA");
                    }
                    catch (Exception ex)
                    {
                        DatabaseService.MarcarAfipColaFallo(item.ColaID, ex.Message);
                    }
                }
            }
            finally
            {
                Monitor.Exit(_lock);
                try { EstadoCambiado?.Invoke(null, EventArgs.Empty); } catch { }
            }
        }
    }
}
