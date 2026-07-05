using AdminLicencias.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace AdminLicencias.Services
{
    public static class DataStore
    {
        // ── Ruta configurable ────────────────────────────────────────────
        // Por defecto: %AppData%\SCHPOSAdmin\datos.json
        // Puede cambiarse a una carpeta compartida de red, OneDrive, etc.
        private static readonly string _configFile = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "SCHPOSAdmin", "config.txt");

        private static string _dir;
        private static string _path;

        /// <summary>Ruta del archivo de datos actualmente en uso.</summary>
        public static string RutaActual => _path;

        /// <summary>
        /// Devuelve la ruta de datos configurada, o la ruta por defecto si no hay config.
        /// </summary>
        public static string ObtenerRutaConfigurada()
        {
            try
            {
                if (File.Exists(_configFile))
                {
                    string ruta = File.ReadAllText(_configFile, System.Text.Encoding.UTF8).Trim();
                    if (!string.IsNullOrEmpty(ruta)) return ruta;
                }
            }
            catch { }
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "SCHPOSAdmin", "datos.json");
        }

        /// <summary>
        /// Cambia la ruta del archivo de datos (ej. a una carpeta compartida).
        /// Guarda la preferencia en config.txt para futuros arranques.
        /// </summary>
        public static void CambiarRuta(string nuevaRuta)
        {
            _path = nuevaRuta;
            _dir  = Path.GetDirectoryName(nuevaRuta);

            string cfgDir = Path.GetDirectoryName(_configFile);
            if (!Directory.Exists(cfgDir)) Directory.CreateDirectory(cfgDir);
            File.WriteAllText(_configFile, nuevaRuta, System.Text.Encoding.UTF8);
        }

        private static void InicializarRuta()
        {
            string ruta = ObtenerRutaConfigurada();
            _path = ruta;
            _dir  = Path.GetDirectoryName(ruta);
        }

        private static StoreData _data = new StoreData();

        public static List<Cliente>  Clientes  => _data.Clientes;
        public static List<Licencia> Licencias => _data.Licencias;

        // ── Carga ────────────────────────────────────────────────────────────
        public static void Cargar()
        {
            InicializarRuta();
            try
            {
                if (!File.Exists(_path)) { _data = new StoreData(); return; }
                string json = File.ReadAllText(_path, System.Text.Encoding.UTF8);
                _data = JsonConvert.DeserializeObject<StoreData>(json) ?? new StoreData();
            }
            catch { _data = new StoreData(); }
        }

        // ── Guardado ─────────────────────────────────────────────────────────
        public static void Guardar()
        {
            if (!Directory.Exists(_dir)) Directory.CreateDirectory(_dir);
            string json = JsonConvert.SerializeObject(_data, Formatting.Indented);
            File.WriteAllText(_path, json, System.Text.Encoding.UTF8);
        }

        // ── Clientes ─────────────────────────────────────────────────────────
        public static void GuardarCliente(Cliente c)
        {
            var idx = _data.Clientes.FindIndex(x => x.Id == c.Id);
            if (idx >= 0) _data.Clientes[idx] = c;
            else          _data.Clientes.Add(c);
            Guardar();
        }

        public static void EliminarCliente(Guid id)
        {
            _data.Clientes.RemoveAll(x => x.Id == id);
            _data.Licencias.RemoveAll(x => x.ClienteId == id);
            Guardar();
        }

        // ── Licencias ────────────────────────────────────────────────────────
        public static void GuardarLicencia(Licencia l)
        {
            var idx = _data.Licencias.FindIndex(x => x.Id == l.Id);
            if (idx >= 0) _data.Licencias[idx] = l;
            else          _data.Licencias.Add(l);
            Guardar();
        }

        public static void RevocarLicencia(Guid id)
        {
            // Licencia no tiene campo Estado mutable (es calculado), usamos fecha pasada como indicador
            var lic = _data.Licencias.FirstOrDefault(x => x.Id == id);
            if (lic != null)
            {
                lic.FechaVencimiento = DateTime.Today.AddDays(-1);
                Guardar();
            }
        }

        // ── Consultas ────────────────────────────────────────────────────────
        public static Licencia UltimaLicencia(Guid clienteId) =>
            _data.Licencias
                 .Where(l => l.ClienteId == clienteId)
                 .OrderByDescending(l => l.FechaEmision)
                 .FirstOrDefault();

        public static List<Licencia> LicenciasDeCliente(Guid clienteId) =>
            _data.Licencias
                 .Where(l => l.ClienteId == clienteId)
                 .OrderByDescending(l => l.FechaEmision)
                 .ToList();

        // ── Stats ────────────────────────────────────────────────────────────
        public static int ClientesActivos =>
            _data.Clientes.Count(c => c.Activo && UltimaLicencia(c.Id)?.Estado == EstadoLicencia.Activa);

        public static int ClientesPorVencer =>
            _data.Clientes.Count(c => c.Activo && UltimaLicencia(c.Id)?.Estado == EstadoLicencia.PorVencer);

        public static int ClientesVencidos =>
            _data.Clientes.Count(c => {
                var lic = UltimaLicencia(c.Id);
                return lic == null || lic.Estado == EstadoLicencia.Vencida;
            });

        public static decimal IngresosTotal =>
            _data.Licencias.Sum(l => l.MontoVenta);

        public static decimal IngresosMesActual =>
            _data.Licencias
                 .Where(l => l.FechaEmision.Year  == DateTime.Today.Year &&
                             l.FechaEmision.Month == DateTime.Today.Month)
                 .Sum(l => l.MontoVenta);

        public static decimal IngresosAnioActual =>
            _data.Licencias
                 .Where(l => l.FechaEmision.Year == DateTime.Today.Year)
                 .Sum(l => l.MontoVenta);
    }

    internal class StoreData
    {
        public List<Cliente>  Clientes  { get; set; } = new List<Cliente>();
        public List<Licencia> Licencias { get; set; } = new List<Licencia>();
    }
}
