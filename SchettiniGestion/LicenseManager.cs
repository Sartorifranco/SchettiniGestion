using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Forms;
using Newtonsoft.Json;

namespace SchettiniGestion
{
	public static class LicenseManager
	{
		public class LicenseData
		{
			public string CuitCliente { get; set; }
			public DateTime FechaExpiracion { get; set; }
			public List<string> ModulosPermitidos { get; set; } = new List<string>();
		}

		private static LicenseData _licenciaActual;

		private static bool CargarLicencia()
		{
			try
			{
				string claveLicencia = "eyJDdWl0Q2xpZW50ZSI6IjIwLTMzNDQ1NTY2LTUiLCJGZWNoYUV4cGlyYWNpb24iOiIyMDI2LTEyLTMxVDIzOjU5OjU5IiwiTW9kdWxvc1Blcm1pdGlkb3MiOlsiQUNDRVNPX0ZBQ1RVUkFDSU9OIiwiQUNDRVNPX1BST0RVQ1RPUyIsIkFDQ0VTT19DTElFTlRFUyIsIkFDQ0VTT19WRU5UQVMiLCJBQ0NFU09fU1RPQ0siLCJBQ0NFU09fVVNVQVJJT1MiLCJBQ0NFU09fUEVSTUlTT1MiLCJBQ0NFU09fUFJPVkVFRE9SRVMiLCJBQ0NFU09fQ09NUFJBUyIsIkFDQ0VTT19QUkVDSU9TIiwiQUNDRVNPX0NBSkEiLCJBQ0NFU09fUFJFU1VQVUVTVE9TIiwiQUNDRVNPX0NVRU5UQVNDT1JSSUVOVEVTIiwiQUNDRVNPX0xJU1RBU1BSRUNJT1MiXX0=";
				byte[] bytesLicencia = Convert.FromBase64String(claveLicencia);
				string jsonLicencia = Encoding.UTF8.GetString(bytesLicencia);
				_licenciaActual = JsonConvert.DeserializeObject<LicenseData>(jsonLicencia);

				if (_licenciaActual == null) return false;
				return true;
			}
			catch { return false; }
		}

		public static bool ValidarLicencia()
		{
			if (!CargarLicencia()) return false;
			if (DateTime.Now > _licenciaActual.FechaExpiracion)
			{
				MessageBox.Show("Licencia Expirada.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
				return false;
			}
			return true;
		}

		public static bool IsModuleEnabled(string moduleName)
		{
			if (_licenciaActual == null || _licenciaActual.ModulosPermitidos == null) return false;
			return _licenciaActual.ModulosPermitidos.Contains(moduleName.ToUpper());
		}
	}
}