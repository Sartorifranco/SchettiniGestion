using System;
using System.Windows;
using SchettiniGestion;

namespace SchettiniGestion.WPF
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // IMPORTANTE: Evita que la app se cierre si cerramos la ventana de Login/Bienvenida
            this.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            try
            {
                // 1. Intentamos conectar a la Base de Datos
                DatabaseService.InitializeDatabase();

                // 2. Verificamos usuarios (Aquí es donde explota si la IP está mal)
                int cantidadUsuarios = DatabaseService.GetCantidadUsuariosRegistrados();

                if (cantidadUsuarios == 0)
                {
                    // EXPERIENCIA PREMIUM: Base vacía -> Bienvenida
                    WelcomeWindow welcome = new WelcomeWindow();
                    welcome.Show();
                }
                else
                {
                    // EXPERIENCIA NORMAL: Hay usuarios -> Login
                    LoginWindow login = new LoginWindow();
                    login.Show();
                }
            }
            catch (Exception ex)
            {
                // 3. PLAN B (SOLO SI FALLA LA CONEXIÓN)
                // Si llegamos acá, es porque la IP está mal o el servidor apagado.
                // Abrimos la ventana principal DIRECTO para que puedas ir a Configuración.

                string mensaje = "No se pudo conectar a la Base de Datos.\n" +
                                 "Posiblemente la IP configurada no es correcta.\n\n" +
                                 "El sistema se abrirá en MODO DE EMERGENCIA para que pueda corregirlo en:\n" +
                                 "CONFIGURACIÓN > RED Y SERVIDOR";

                MessageBox.Show(mensaje, "Error de Conexión", MessageBoxButton.OK, MessageBoxImage.Error);

                PrincipalWindow principal = new PrincipalWindow();
                principal.Show();
            }
        }
    }
}