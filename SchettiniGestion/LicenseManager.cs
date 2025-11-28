using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Forms; // Usamos MessageBox estándar aquí para evitar líos de referencias
using Newtonsoft.Json;

namespace SchettiniGestion
{
	public static class LicenseManager
	{
		// 1. Clase interna para los datos de la licencia
		public class LicenseData
		{
			public string CuitCliente { get; set; }
			public DateTime FechaExpiracion { get; set; }
			public List<string> ModulosPermitidos { get; set; } = new List<string>();
		}

		// 2. Variable para guardar la licencia
		private static LicenseData _licenciaActual;

		// 3. Método para cargar la licencia
		private static bool CargarLicencia()
		{
			try
			{
				// Clave PRO (Incluye Listas de Precios)
				string claveLicencia = "eyJDdWl0Q2xpZW50ZSI6IjIwLTMzNDQ1NTY2LTUiLCJGZWNoYUV4cGlyYWNpb24iOiIyMDI2LTEyLTMxVDIzOjU5OjU5IiwiTW9kdWxvc1Blcm1pdGlkb3MiOlsiQUNDRVNPX0ZBQ1RVUkFDSU9OIiwiQUNDRVNPX1BST0RVQ1RPUyIsIkFDQ0VTT19DTElFTlRFUyIsIkFDQ0VTT19WRU5UQVMiLCJBQ0NFU09fU1RPQ0siLCJBQ0NFU09fVVNVQVJJT1MiLCJBQ0NFU09fUEVSTUlTT1MiLCJBQ0NFU09fUFJPVkVFRE9SRVMiLCJBQ0NFU09fQ09NUFJBUyIsIkFDQ0VTT19QUkVDSU9TIiwiQUNDRVNPX0NBSkEiLCJBQ0NFU09fUFJFU1VQVUVTVE9TIiwiQUNDRVNPX0NVRU5UQVNDT1JSSUVOVEVTIiwiQUNDRVNPX0xJU1RBU1BSRUNJT1MiXX0=";

				byte[] bytesLicencia = Convert.FromBase64String(claveLicencia);
				string jsonLicencia = Encoding.UTF8.GetString(bytesLicencia);

				_licenciaActual = JsonConvert.DeserializeObject<LicenseData>(jsonLicencia);

				if (_licenciaActual == null)
				{
					MessageBox.Show("La clave de licencia está corrupta.", "Error de Licencia", MessageBoxButtons.OK, MessageBoxIcon.Error);
					return false;
				}

				return true;
			}
			catch (Exception ex)
			{
				MessageBox.Show($"Error al validar la licencia: {ex.Message}", "Error de Licencia", MessageBoxButtons.OK, MessageBoxIcon.Error);
				return false;
			}
		}

		// 4. Método para validar
		public static bool ValidarLicencia()
		{
			if (!CargarLicencia()) return false;

			// Anti-Reloj
			if (DateTime.Now < new DateTime(2024, 1, 1))
			{
				MessageBox.Show("Se detectó una fecha de sistema inválida. Por favor, corrija el reloj.", "Error de Licencia", MessageBoxButtons.OK, MessageBoxIcon.Error);
				return false;
			}

			// Expiración
			if (DateTime.Now > _licenciaActual.FechaExpiracion)
			{
				MessageBox.Show($"Su licencia ha expirado el {_licenciaActual.FechaExpiracion.ToShortDateString()}. Por favor, contacte al proveedor.", "Licencia Expirada", MessageBoxButtons.OK, MessageBoxIcon.Error);
				return false;
			}

			return true;
		}

		// 5. Chequear módulos
		public static bool IsModuleEnabled(string moduleName)
		{
			if (_licenciaActual == null || _licenciaActual.ModulosPermitidos == null) return false;
			return _licenciaActual.ModulosPermitidos.Contains(moduleName.ToUpper());
		}
	}
}