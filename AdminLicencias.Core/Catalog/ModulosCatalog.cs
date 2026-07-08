using Newtonsoft.Json;
using System.Text;

namespace AdminLicencias.Core.Catalog;

/// <summary>Catálogo único de módulos SCHPOS (ModulosCatalog.json).</summary>
public static class ModulosCatalog
{
    public class ModuloDef
    {
        public string Codigo { get; set; } = "";
        public string Nombre { get; set; } = "";
        public string NombreCorto { get; set; } = "";
        public string Grupo { get; set; } = "modulo_adicional";
        public bool Licenciable { get; set; } = true;
        public bool Implicito { get; set; }
        public bool IncluidoEnLite { get; set; }
        public bool VisibleEnLicenciador { get; set; } = true;
        public bool EsAbonoMensual { get; set; }
        public List<string> DependeDe { get; set; } = new();
        public int Orden { get; set; }
    }

    public const string GrupoLiteBase = "lite_base";
    public const string GrupoModuloAdicional = "modulo_adicional";
    public const string GrupoExtraUnico = "extra_unico";
    public const string GrupoAbonoMensual = "abono_mensual";
    public const string GrupoPendiente = "pendiente";

    private class CatalogFile
    {
        public int Version { get; set; }
        public string Descripcion { get; set; } = "";
        public List<ModuloDef> Modulos { get; set; } = new();
    }

    private static CatalogFile? _catalog;
    private static readonly object Lock = new();

    public static void Recargar()
    {
        lock (Lock)
        {
            _catalog = null;
            EnsureLoaded();
        }
    }

    public static void EnsureLoaded()
    {
        if (_catalog != null) return;

        lock (Lock)
        {
            if (_catalog != null) return;

            string json = LeerJson();
            _catalog = JsonConvert.DeserializeObject<CatalogFile>(json) ?? new CatalogFile();
            if (_catalog.Modulos == null)
                _catalog.Modulos = new List<ModuloDef>();

            foreach (var m in _catalog.Modulos)
            {
                m.Codigo = NormalizarCodigo(m.Codigo);
                m.DependeDe = (m.DependeDe ?? new List<string>())
                    .Select(NormalizarCodigo)
                    .Where(c => !string.IsNullOrEmpty(c))
                    .ToList();
            }
        }
    }

    public static IReadOnlyList<ModuloDef> ObtenerTodos()
    {
        EnsureLoaded();
        return _catalog!.Modulos.OrderBy(m => m.Orden).ThenBy(m => m.Nombre).ToList();
    }

    public static IReadOnlyList<ModuloDef> ObtenerLicenciables() =>
        ObtenerTodos().Where(m => m.Licenciable && !m.Implicito && m.VisibleEnLicenciador).ToList();

    public static List<string> ObtenerPresetLite() =>
        ResolverLicencia(ObtenerTodos().Where(m => m.IncluidoEnLite && m.Licenciable).Select(m => m.Codigo));

    /// <summary>Lite + todos los módulos licenciables visibles (plan Pro).</summary>
    public static List<string> ObtenerPresetPro() =>
        ResolverLicencia(ObtenerLicenciables().Select(m => m.Codigo));

    public static IReadOnlyList<string> ObtenerImplicitos() =>
        ObtenerTodos().Where(m => m.Implicito).Select(m => m.Codigo).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

    public static string ObtenerNombreLegible(string codigo)
    {
        if (string.IsNullOrWhiteSpace(codigo)) return "";
        EnsureLoaded();
        var mod = _catalog!.Modulos.FirstOrDefault(m =>
            string.Equals(m.Codigo, codigo, StringComparison.OrdinalIgnoreCase));
        if (mod == null)
            return codigo.Replace("ACCESO_", "").Replace("_", " ");
        return !string.IsNullOrWhiteSpace(mod.NombreCorto) ? mod.NombreCorto : mod.Nombre;
    }

    public static string ObtenerResumenModulos(IEnumerable<string>? codigos)
    {
        if (codigos == null) return "";
        return string.Join(", ",
            codigos.Where(c => !string.IsNullOrWhiteSpace(c))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(ObtenerNombreLegible));
    }

    public static List<string> ResolverLicencia(IEnumerable<string>? seleccionados)
    {
        EnsureLoaded();
        var resultado = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (seleccionados != null)
        {
            foreach (var codigo in seleccionados)
            {
                if (!string.IsNullOrWhiteSpace(codigo))
                    AgregarConDependencias(resultado, NormalizarCodigo(codigo));
            }
        }

        foreach (var implicito in ObtenerImplicitos())
            resultado.Add(implicito);

        return resultado.OrderBy(ObtenerOrden).ThenBy(c => c, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static void AgregarConDependencias(HashSet<string> acumulado, string codigo)
    {
        if (string.IsNullOrWhiteSpace(codigo)) return;
        acumulado.Add(codigo);
        var mod = _catalog!.Modulos.FirstOrDefault(m =>
            string.Equals(m.Codigo, codigo, StringComparison.OrdinalIgnoreCase));
        if (mod?.DependeDe == null) return;
        foreach (var dep in mod.DependeDe)
            AgregarConDependencias(acumulado, dep);
    }

    private static int ObtenerOrden(string codigo)
    {
        var mod = _catalog!.Modulos.FirstOrDefault(m =>
            string.Equals(m.Codigo, codigo, StringComparison.OrdinalIgnoreCase));
        return mod?.Orden ?? 9999;
    }

    private static string NormalizarCodigo(string? codigo) =>
        (codigo ?? "").Trim().ToUpperInvariant();

    private static string LeerJson()
    {
        foreach (string path in ObtenerRutasBusqueda())
        {
            try
            {
                if (File.Exists(path))
                    return File.ReadAllText(path, Encoding.UTF8);
            }
            catch { /* siguiente ruta */ }
        }

        throw new FileNotFoundException(
            "No se encontró ModulosCatalog.json junto a AdminLicencias.Core.");
    }

    private static IEnumerable<string> ObtenerRutasBusqueda()
    {
        var rutas = new List<string>();
        string baseDir = AppContext.BaseDirectory;
        if (!string.IsNullOrWhiteSpace(baseDir))
            rutas.Add(Path.Combine(baseDir, "ModulosCatalog.json"));

        try
        {
            var asmDir = Path.GetDirectoryName(typeof(ModulosCatalog).Assembly.Location);
            if (!string.IsNullOrWhiteSpace(asmDir))
                rutas.Add(Path.Combine(asmDir, "ModulosCatalog.json"));
        }
        catch { }

        return rutas.Distinct(StringComparer.OrdinalIgnoreCase);
    }
}
