using System;
using System.Diagnostics; // Necesario para Process (Teclado)
using System.Windows;
using System.Windows.Input;
using SchettiniGestion; // Importamos la lógica de base de datos

namespace SchettiniGestion.WPF
{
	public partial class MainWindow : Window
	{
		public MainWindow()
		{
			InitializeComponent();

			// ================================================================
			// CONEXIÓN DEL PUENTE DE ERRORES (CRUCIAL)
			// ================================================================
			// Aquí le decimos al núcleo (DatabaseService): 
			// "Cuando tengas un error, usa MI diseño (CustomMessageBox) para mostrarlo".
			DatabaseService.OnDbError = (mensajeError) =>
			{
				// Usamos Dispatcher por si el error viene de un hilo secundario
				Application.Current.Dispatcher.Invoke(() =>
				{
					CustomMessageBox.Show(mensajeError, "Aviso del Sistema", MessageBoxButton.OK, MessageBoxImage.Error);
				});
			};
		}

		private void btnIngresar_Click(object sender, RoutedEventArgs e)
		{
			string usuario = txtUsuario.Text;
			string password = txtPassword.Password;

			if (string.IsNullOrEmpty(usuario) || string.IsNullOrEmpty(password))
			{
				CustomMessageBox.Show("Por favor, ingrese usuario y contraseña.", "Datos Incompletos", MessageBoxButton.OK, MessageBoxImage.Warning);
				return;
			}

			// 1. Validamos la contraseña
			bool esValido = DatabaseService.ValidarUsuario(usuario, password);

			if (esValido)
			{
				// 2. Si es válida, cargamos todos sus permisos en la sesión global
				bool sesionCargada = DatabaseService.CargarSesionUsuario(usuario);

				if (sesionCargada)
				{
					// 3. Si los permisos se cargaron bien, abrimos la app
					PrincipalWindow ventanaPrincipal = new PrincipalWindow();
					ventanaPrincipal.Show();
					this.Close();
				}
				else
				{
					// Si falla cargar sesión, el DatabaseService ya habrá disparado el OnDbError,
					// pero mostramos un aviso extra por seguridad.
					CustomMessageBox.Show("Error al cargar los permisos. Contacte al administrador.", "Error de Sesión", MessageBoxButton.OK, MessageBoxImage.Error);
				}
			}
			else
			{
				CustomMessageBox.Show("Usuario o contraseña incorrectos.", "Acceso Denegado", MessageBoxButton.OK, MessageBoxImage.Error);
			}
		}

		private void btnClose_Click(object sender, RoutedEventArgs e)
		{
			Application.Current.Shutdown();
		}

		private void btnTeclado_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				Process[] oskProcesses = Process.GetProcessesByName("osk");
				if (oskProcesses.Length == 0)
				{
					// Intentamos abrir el teclado en pantalla de Windows
					string path64 = @"C:\Windows\Www64\osk.exe";
					string path32 = @"C:\Windows\System32\osk.exe";

					if (System.IO.File.Exists(path64))
						Process.Start(path64);
					else
						Process.Start(path32);
				}
			}
			catch (Exception ex)
			{
				CustomMessageBox.Show($"No se pudo iniciar el teclado: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
			}
		}

		private void Input_KeyDown(object sender, KeyEventArgs e)
		{
			if (e.Key == Key.Enter)
			{
				btnIngresar_Click(sender, e);
			}
		}

		private void Window_MouseDown(object sender, MouseButtonEventArgs e)
		{
			if (e.LeftButton == MouseButtonState.Pressed)
			{
				DragMove();
			}
		}
	}
}