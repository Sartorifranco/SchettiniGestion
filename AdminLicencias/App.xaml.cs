using AdminLicencias.Services;
using System;
using System.Windows;
using System.Windows.Threading;

namespace AdminLicencias
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            DispatcherUnhandledException += App_DispatcherUnhandledException;
            DataStore.Cargar();
        }

        private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            e.Handled = true;
            string msg = e.Exception?.InnerException?.Message ?? e.Exception?.Message ?? "Error desconocido";
            string tipo = e.Exception?.GetType().Name ?? "";
            MessageBox.Show(
                $"Error inesperado ({tipo}):\n\n{msg}\n\n" +
                $"Detalle: {e.Exception?.InnerException?.ToString() ?? e.Exception?.ToString()}",
                "Error en AdminLicencias",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
