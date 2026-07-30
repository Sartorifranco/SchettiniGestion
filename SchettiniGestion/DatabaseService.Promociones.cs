using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using SqlConnection = Microsoft.Data.SqlClient.SqlConnection;
using SqlCommand = Microsoft.Data.SqlClient.SqlCommand;
using SqlDataAdapter = Microsoft.Data.SqlClient.SqlDataAdapter;

namespace SchettiniGestion
{
    public static partial class DatabaseService
    {
        public static class TiposPromo
        {
            public const string PctProducto = "PCT_PRODUCTO";
            public const string PctCategoria = "PCT_CATEGORIA";
            public const string PctTodos = "PCT_TODOS";
            public const string ComboProductos = "COMBO_PRODUCTOS";
        }

        public static class ModalidadesPromo
        {
            public const string Porcentaje = "PORCENTAJE";
            public const string MontoFijo = "MONTO_FIJO";
            public const string PrecioFinal = "PRECIO_FINAL";
            public const string DosPorUno = "2X1";
            public const string TresPorDos = "3X2";
            public const string Bonificar = "BONIFICAR";
            public const string EscalaCantidad = "ESCALA_CANTIDAD";
        }

        public class PromoVigente
        {
            public int PromoID { get; set; }
            public string Nombre { get; set; }
            public string Tipo { get; set; }
            public string Modalidad { get; set; }
            public decimal Porcentaje { get; set; }
            public decimal MontoFijo { get; set; }
            public decimal PrecioCombo { get; set; }
            public int CantidadMinima { get; set; }
            public int CantidadBonificada { get; set; }
            public List<int> ProductoIDs { get; set; } = new List<int>();
        }

        /// <summary>Promo activa y vigente en la fecha de hoy (para banner POS / catálogo).</summary>
        public class PromoActivaHoy
        {
            public int PromoID { get; set; }
            public string Nombre { get; set; }
            public string Tipo { get; set; }
            public int? ProductoID { get; set; }
            public string Categoria { get; set; }
            public string Modalidad { get; set; }
            public decimal Porcentaje { get; set; }
            public decimal MontoFijo { get; set; }
            public decimal PrecioCombo { get; set; }
            public int CantidadMinima { get; set; }
            public int CantidadBonificada { get; set; }
            public string AlcanceTexto { get; set; }
            public List<int> ProductoIDs { get; set; } = new List<int>();
            public string ProductosTexto { get; set; }
        }

        private static bool _tablaPromocionesOk;
        private static readonly object _lockPromociones = new object();

        private static void AsegurarTablaPromociones(SqlConnection c)
        {
            if (_tablaPromocionesOk) return;
            lock (_lockPromociones)
            {
                if (_tablaPromocionesOk) return;
                try
                {
                    using (var cmd = new SqlCommand(@"
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME='Promociones')
  CREATE TABLE dbo.Promociones (
    PromoID INT IDENTITY(1,1) PRIMARY KEY,
    Nombre NVARCHAR(120) NOT NULL,
    Tipo NVARCHAR(30) NOT NULL,
    ProductoID INT NULL,
    Categoria NVARCHAR(100) NULL,
    Modalidad NVARCHAR(30) NOT NULL DEFAULT 'PORCENTAJE',
    Porcentaje DECIMAL(9,4) NOT NULL DEFAULT 0,
    MontoFijo DECIMAL(18,2) NULL,
    PrecioCombo DECIMAL(18,2) NULL,
    CantidadMinima INT NULL,
    CantidadBonificada INT NULL,
    FechaDesde DATE NULL,
    FechaHasta DATE NULL,
    Activo BIT NOT NULL DEFAULT 1,
    Observaciones NVARCHAR(250) NULL
  );
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Promociones' AND COLUMN_NAME='Modalidad')
  ALTER TABLE Promociones ADD Modalidad NVARCHAR(30) NOT NULL DEFAULT 'PORCENTAJE';
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Promociones' AND COLUMN_NAME='MontoFijo')
  ALTER TABLE Promociones ADD MontoFijo DECIMAL(18,2) NULL;
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Promociones' AND COLUMN_NAME='PrecioCombo')
  ALTER TABLE Promociones ADD PrecioCombo DECIMAL(18,2) NULL;
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Promociones' AND COLUMN_NAME='CantidadMinima')
  ALTER TABLE Promociones ADD CantidadMinima INT NULL;
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Promociones' AND COLUMN_NAME='CantidadBonificada')
  ALTER TABLE Promociones ADD CantidadBonificada INT NULL;
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME='PromoProductos')
  CREATE TABLE dbo.PromoProductos (
    PromoID INT NOT NULL,
    ProductoID INT NOT NULL,
    PRIMARY KEY (PromoID, ProductoID)
  );", c))
                        cmd.ExecuteNonQuery();
                    _tablaPromocionesOk = true;
                }
                catch { /* sin permiso CREATE */ }
            }
        }

        public static string EtiquetaTipoPromo(string tipo)
        {
            if (tipo == TiposPromo.PctProducto) return "Un producto";
            if (tipo == TiposPromo.PctCategoria) return "Una categoría";
            if (tipo == TiposPromo.PctTodos) return "Todo el local";
            if (tipo == TiposPromo.ComboProductos) return "Combo";
            return tipo ?? "";
        }

        public static string EtiquetaModalidadPromo(string modalidad)
        {
            if (modalidad == ModalidadesPromo.MontoFijo) return "Monto fijo";
            if (modalidad == ModalidadesPromo.PrecioFinal) return "Precio final";
            if (modalidad == ModalidadesPromo.DosPorUno) return "2x1";
            if (modalidad == ModalidadesPromo.TresPorDos) return "3x2";
            if (modalidad == ModalidadesPromo.Bonificar) return "Bonificar";
            if (modalidad == ModalidadesPromo.EscalaCantidad) return "Escala por cantidad";
            return "% descuento";
        }

        public static string DescribirValorPromo(string modalidad, decimal porcentaje, decimal montoFijo, decimal precioCombo, int cantidadMinima, int cantidadBonificada)
        {
            if (modalidad == ModalidadesPromo.MontoFijo) return "$" + montoFijo.ToString("N2");
            if (modalidad == ModalidadesPromo.PrecioFinal) return "Final $" + precioCombo.ToString("N2");
            if (modalidad == ModalidadesPromo.DosPorUno) return "2x1";
            if (modalidad == ModalidadesPromo.TresPorDos) return "3x2";
            if (modalidad == ModalidadesPromo.Bonificar) return "Llevá " + cantidadMinima + ", bonifica " + cantidadBonificada;
            if (modalidad == ModalidadesPromo.EscalaCantidad) return cantidadMinima + "+ u. -" + porcentaje.ToString("0.##") + "%";
            return "-" + porcentaje.ToString("0.##") + "%";
        }

        private static bool EsTipoPromoValido(string tipo)
            => tipo == TiposPromo.PctProducto || tipo == TiposPromo.PctCategoria
               || tipo == TiposPromo.PctTodos || tipo == TiposPromo.ComboProductos;

        private static bool EsModalidadPromoValida(string modalidad)
            => modalidad == ModalidadesPromo.Porcentaje || modalidad == ModalidadesPromo.MontoFijo
               || modalidad == ModalidadesPromo.PrecioFinal || modalidad == ModalidadesPromo.DosPorUno
               || modalidad == ModalidadesPromo.TresPorDos || modalidad == ModalidadesPromo.Bonificar
               || modalidad == ModalidadesPromo.EscalaCantidad;

        private static List<int> ParseIdsCsv(string csv)
        {
            var ids = new List<int>();
            if (string.IsNullOrWhiteSpace(csv)) return ids;
            foreach (var part in csv.Split(','))
            {
                if (int.TryParse(part.Trim(), out int id) && id > 0 && !ids.Contains(id))
                    ids.Add(id);
            }
            return ids;
        }

        public static DataTable GetPromociones()
        {
            var dt = new DataTable();
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    AsegurarTablaPromociones(c);
                    new SqlDataAdapter(@"
SELECT pr.PromoID, pr.Nombre, pr.Tipo, pr.ProductoID, pr.Categoria,
       ISNULL(pr.Modalidad, 'PORCENTAJE') AS Modalidad,
       pr.Porcentaje, ISNULL(pr.MontoFijo, 0) AS MontoFijo, ISNULL(pr.PrecioCombo, 0) AS PrecioCombo,
       ISNULL(pr.CantidadMinima, 0) AS CantidadMinima, ISNULL(pr.CantidadBonificada, 0) AS CantidadBonificada,
       pr.FechaDesde, pr.FechaHasta, pr.Activo, pr.Observaciones,
       p.Descripcion AS ProductoNombre,
       STUFF((SELECT ',' + CAST(pp.ProductoID AS NVARCHAR(20))
              FROM PromoProductos pp
              WHERE pp.PromoID = pr.PromoID
              ORDER BY pp.ProductoID
              FOR XML PATH(''), TYPE).value('.', 'nvarchar(max)'), 1, 1, '') AS ComboProductoIDs,
       STUFF((SELECT ', ' + ISNULL(px.Descripcion, '#' + CAST(pp.ProductoID AS NVARCHAR(20)))
              FROM PromoProductos pp
              LEFT JOIN Productos px ON px.ProductoID = pp.ProductoID
              WHERE pp.PromoID = pr.PromoID
              ORDER BY px.Descripcion
              FOR XML PATH(''), TYPE).value('.', 'nvarchar(max)'), 1, 2, '') AS ComboProductos,
       CASE pr.Tipo
         WHEN 'PCT_PRODUCTO' THEN N'Un producto'
         WHEN 'PCT_CATEGORIA' THEN N'Una categoría'
         WHEN 'PCT_TODOS' THEN N'Todo el local'
         WHEN 'COMBO_PRODUCTOS' THEN N'Combo'
         ELSE pr.Tipo END AS TipoEtiqueta,
       CASE ISNULL(pr.Modalidad, 'PORCENTAJE')
         WHEN 'MONTO_FIJO' THEN N'Monto fijo'
         WHEN 'PRECIO_FINAL' THEN N'Precio final'
         WHEN '2X1' THEN N'2x1'
         WHEN '3X2' THEN N'3x2'
         WHEN 'BONIFICAR' THEN N'Bonificar'
         WHEN 'ESCALA_CANTIDAD' THEN N'Escala'
         ELSE N'% descuento' END AS ModalidadEtiqueta,
       CASE WHEN pr.Activo = 1 THEN N'Sí' ELSE N'No' END AS ActivoEtiqueta
FROM Promociones pr
LEFT JOIN Productos p ON p.ProductoID = pr.ProductoID
ORDER BY pr.Activo DESC, pr.Nombre", c).Fill(dt);
                }
            }
            catch (Exception ex) { NotificarError("Promociones: " + ex.Message); }
            return dt;
        }

        public static bool GuardarPromocion(int id, string nombre, string tipo, int? productoId, string categoria,
            decimal porcentaje, DateTime? desde, DateTime? hasta, bool activo, string observaciones)
            => GuardarPromocion(id, nombre, tipo, ModalidadesPromo.Porcentaje, productoId, categoria, porcentaje,
                0m, 0m, 0, 0, null, desde, hasta, activo, observaciones);

        public static bool GuardarPromocion(int id, string nombre, string tipo, string modalidad, int? productoId, string categoria,
            decimal porcentaje, decimal montoFijo, decimal precioCombo, int cantidadMinima, int cantidadBonificada,
            IList<int> productoIds, DateTime? desde, DateTime? hasta, bool activo, string observaciones)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(nombre))
                {
                    NotificarError("Ingresá un nombre para la promoción.");
                    return false;
                }

                tipo = (tipo ?? "").Trim();
                if (!EsTipoPromoValido(tipo))
                    tipo = TiposPromo.PctProducto;
                modalidad = (modalidad ?? "").Trim();
                if (!EsModalidadPromoValida(modalidad))
                    modalidad = ModalidadesPromo.Porcentaje;

                if (modalidad == ModalidadesPromo.DosPorUno)
                {
                    cantidadMinima = 2;
                    cantidadBonificada = 1;
                }
                else if (modalidad == ModalidadesPromo.TresPorDos)
                {
                    cantidadMinima = 3;
                    cantidadBonificada = 1;
                }

                if (tipo == TiposPromo.PctProducto && (!productoId.HasValue || productoId.Value <= 0))
                {
                    NotificarError("Elegí el producto de la promo.");
                    return false;
                }
                if (tipo == TiposPromo.PctCategoria && string.IsNullOrWhiteSpace(categoria))
                {
                    NotificarError("Elegí la categoría de la promo.");
                    return false;
                }
                var productosCombo = (productoIds ?? new List<int>())
                    .Where(x => x > 0)
                    .Distinct()
                    .ToList();
                if (tipo == TiposPromo.ComboProductos && productosCombo.Count < 2)
                {
                    NotificarError("Elegí al menos dos productos para el combo.");
                    return false;
                }
                if ((modalidad == ModalidadesPromo.Porcentaje || modalidad == ModalidadesPromo.EscalaCantidad)
                    && (porcentaje <= 0 || porcentaje > 100))
                {
                    NotificarError("El descuento tiene que ser entre 1 y 100%.");
                    return false;
                }
                if (modalidad == ModalidadesPromo.MontoFijo && montoFijo <= 0)
                {
                    NotificarError("Ingresá el monto fijo a descontar.");
                    return false;
                }
                if (modalidad == ModalidadesPromo.PrecioFinal && precioCombo <= 0)
                {
                    NotificarError("Ingresá el precio final de la promo.");
                    return false;
                }
                if ((modalidad == ModalidadesPromo.Bonificar || modalidad == ModalidadesPromo.EscalaCantidad)
                    && cantidadMinima <= 0)
                {
                    NotificarError("Ingresá la cantidad mínima.");
                    return false;
                }
                if (modalidad == ModalidadesPromo.Bonificar && cantidadBonificada <= 0)
                {
                    NotificarError("Ingresá la cantidad bonificada.");
                    return false;
                }
                if (desde.HasValue && hasta.HasValue && hasta.Value.Date < desde.Value.Date)
                {
                    NotificarError("La fecha hasta no puede ser anterior a la fecha desde.");
                    return false;
                }

                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    AsegurarTablaPromociones(c);
                    using (var tx = c.BeginTransaction())
                    {
                        string sql = id == 0
                            ? @"INSERT INTO Promociones (Nombre, Tipo, ProductoID, Categoria, Modalidad, Porcentaje, MontoFijo, PrecioCombo, CantidadMinima, CantidadBonificada, FechaDesde, FechaHasta, Activo, Observaciones)
                               OUTPUT INSERTED.PromoID
                               VALUES (@n, @t, @pid, @cat, @mod, @pct, @mf, @pc, @cmin, @cbon, @d, @h, @a, @obs)"
                            : @"UPDATE Promociones SET Nombre=@n, Tipo=@t, ProductoID=@pid, Categoria=@cat, Modalidad=@mod, Porcentaje=@pct,
                               MontoFijo=@mf, PrecioCombo=@pc, CantidadMinima=@cmin, CantidadBonificada=@cbon,
                               FechaDesde=@d, FechaHasta=@h, Activo=@a, Observaciones=@obs WHERE PromoID=@id";
                        using (var cmd = new SqlCommand(sql, c, tx))
                        {
                            cmd.Parameters.AddWithValue("@n", nombre.Trim());
                            cmd.Parameters.AddWithValue("@t", tipo);
                            cmd.Parameters.AddWithValue("@pid", tipo == TiposPromo.PctProducto ? (object)productoId.Value : DBNull.Value);
                            cmd.Parameters.AddWithValue("@cat", tipo == TiposPromo.PctCategoria ? (object)categoria.Trim() : DBNull.Value);
                            cmd.Parameters.AddWithValue("@mod", modalidad);
                            cmd.Parameters.AddWithValue("@pct", porcentaje);
                            cmd.Parameters.AddWithValue("@mf", modalidad == ModalidadesPromo.MontoFijo ? (object)montoFijo : DBNull.Value);
                            cmd.Parameters.AddWithValue("@pc", modalidad == ModalidadesPromo.PrecioFinal ? (object)precioCombo : DBNull.Value);
                            cmd.Parameters.AddWithValue("@cmin", cantidadMinima > 0 ? (object)cantidadMinima : DBNull.Value);
                            cmd.Parameters.AddWithValue("@cbon", cantidadBonificada > 0 ? (object)cantidadBonificada : DBNull.Value);
                            cmd.Parameters.AddWithValue("@d", desde.HasValue ? (object)desde.Value.Date : DBNull.Value);
                            cmd.Parameters.AddWithValue("@h", hasta.HasValue ? (object)hasta.Value.Date : DBNull.Value);
                            cmd.Parameters.AddWithValue("@a", activo);
                            string obs = (observaciones ?? "").Trim();
                            cmd.Parameters.AddWithValue("@obs", string.IsNullOrEmpty(obs) ? (object)DBNull.Value : obs);
                            cmd.Parameters.AddWithValue("@id", id);
                            if (id == 0)
                                id = Convert.ToInt32(cmd.ExecuteScalar());
                            else
                                cmd.ExecuteNonQuery();
                        }

                        using (var del = new SqlCommand("DELETE FROM PromoProductos WHERE PromoID=@id", c, tx))
                        {
                            del.Parameters.AddWithValue("@id", id);
                            del.ExecuteNonQuery();
                        }
                        if (tipo == TiposPromo.ComboProductos)
                        {
                            foreach (int pid in productosCombo)
                            {
                                using (var ins = new SqlCommand("INSERT INTO PromoProductos (PromoID, ProductoID) VALUES (@promo, @pid)", c, tx))
                                {
                                    ins.Parameters.AddWithValue("@promo", id);
                                    ins.Parameters.AddWithValue("@pid", pid);
                                    ins.ExecuteNonQuery();
                                }
                            }
                        }
                        tx.Commit();
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                NotificarError("No se pudo guardar la promoción: " + ex.Message);
                return false;
            }
        }

        public static bool EliminarPromocion(int id)
        {
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    AsegurarTablaPromociones(c);
                    using (var tx = c.BeginTransaction())
                    {
                        using (var cmd = new SqlCommand("DELETE FROM PromoProductos WHERE PromoID=@id", c, tx))
                        {
                            cmd.Parameters.AddWithValue("@id", id);
                            cmd.ExecuteNonQuery();
                        }
                        using (var cmd = new SqlCommand("DELETE FROM Promociones WHERE PromoID=@id", c, tx))
                        {
                            cmd.Parameters.AddWithValue("@id", id);
                            cmd.ExecuteNonQuery();
                        }
                        tx.Commit();
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                NotificarError("No se pudo eliminar la promoción: " + ex.Message);
                return false;
            }
        }

        public static DataTable GetPromoProductos(int promoId)
        {
            var dt = new DataTable();
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    AsegurarTablaPromociones(c);
                    using (var cmd = new SqlCommand(@"
SELECT p.ProductoID,
       p.Codigo,
       p.CodigoBarra,
       ISNULL(p.Descripcion, N'') AS Descripcion,
       LTRIM(RTRIM(ISNULL(p.Codigo, N''))) + CASE WHEN ISNULL(p.CodigoBarra, N'') <> N'' THEN N' / ' + p.CodigoBarra ELSE N'' END
         + N' - ' + ISNULL(p.Descripcion, N'') AS Display
FROM PromoProductos pp
JOIN Productos p ON p.ProductoID = pp.ProductoID
WHERE pp.PromoID = @id
ORDER BY p.Descripcion", c))
                    {
                        cmd.Parameters.AddWithValue("@id", promoId);
                        new SqlDataAdapter(cmd).Fill(dt);
                    }
                }
            }
            catch { }
            return dt;
        }

        public static DataTable BuscarProductosParaPromocion(string filtro)
        {
            var dt = new DataTable();
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    AsegurarMigracionLite(c);
                    string q = (filtro ?? "").Trim();
                    string like = "%" + q.Replace("[", "[[]").Replace("%", "[%]").Replace("_", "[_]") + "%";
                    using (var cmd = new SqlCommand(@"
SELECT TOP 50
       ProductoID,
       Codigo,
       CodigoBarra,
       ISNULL(Descripcion, N'') AS Descripcion,
       LTRIM(RTRIM(ISNULL(Codigo, N''))) + CASE WHEN ISNULL(CodigoBarra, N'') <> N'' THEN N' / ' + CodigoBarra ELSE N'' END
         + N' - ' + ISNULL(Descripcion, N'') AS Display
FROM Productos
WHERE (@q = N'' OR Codigo = @q OR CodigoBarra = @q OR Codigo LIKE @like OR CodigoBarra LIKE @like OR Descripcion LIKE @like)
ORDER BY CASE WHEN Codigo = @q OR CodigoBarra = @q THEN 0 ELSE 1 END, Descripcion", c))
                    {
                        cmd.Parameters.AddWithValue("@q", q);
                        cmd.Parameters.AddWithValue("@like", like);
                        new SqlDataAdapter(cmd).Fill(dt);
                    }
                }
            }
            catch { }
            return dt;
        }

        /// <summary>
        /// Promos activas cuya ventana de fechas incluye hoy (para banner del POS).
        /// </summary>
        public static List<PromoActivaHoy> GetPromocionesVigentesHoy()
        {
            var list = new List<PromoActivaHoy>();
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    AsegurarTablaPromociones(c);
                    using (var cmd = new SqlCommand(@"
SELECT pr.PromoID, pr.Nombre, pr.Tipo, pr.ProductoID, pr.Categoria,
       ISNULL(pr.Modalidad, 'PORCENTAJE') AS Modalidad,
       pr.Porcentaje, ISNULL(pr.MontoFijo, 0) AS MontoFijo, ISNULL(pr.PrecioCombo, 0) AS PrecioCombo,
       ISNULL(pr.CantidadMinima, 0) AS CantidadMinima, ISNULL(pr.CantidadBonificada, 0) AS CantidadBonificada,
       STUFF((SELECT ',' + CAST(pp.ProductoID AS NVARCHAR(20))
              FROM PromoProductos pp
              WHERE pp.PromoID = pr.PromoID
              ORDER BY pp.ProductoID
              FOR XML PATH(''), TYPE).value('.', 'nvarchar(max)'), 1, 1, '') AS ComboProductoIDs,
       STUFF((SELECT ', ' + ISNULL(px.Descripcion, '#' + CAST(pp.ProductoID AS NVARCHAR(20)))
              FROM PromoProductos pp
              LEFT JOIN Productos px ON px.ProductoID = pp.ProductoID
              WHERE pp.PromoID = pr.PromoID
              ORDER BY px.Descripcion
              FOR XML PATH(''), TYPE).value('.', 'nvarchar(max)'), 1, 2, '') AS ComboProductos,
       CASE pr.Tipo
         WHEN 'PCT_PRODUCTO' THEN N'un producto'
         WHEN 'PCT_CATEGORIA' THEN N'categoría'
         WHEN 'PCT_TODOS' THEN N'todo el local'
         WHEN 'COMBO_PRODUCTOS' THEN N'combo'
         ELSE pr.Tipo END AS AlcanceTexto
FROM Promociones pr
WHERE pr.Activo = 1
  AND (pr.FechaDesde IS NULL OR pr.FechaDesde <= CAST(GETDATE() AS DATE))
  AND (pr.FechaHasta IS NULL OR pr.FechaHasta >= CAST(GETDATE() AS DATE))
ORDER BY
  CASE pr.Tipo WHEN 'PCT_TODOS' THEN 1 WHEN 'PCT_CATEGORIA' THEN 2 WHEN 'PCT_PRODUCTO' THEN 3 ELSE 4 END,
  pr.Porcentaje DESC, pr.Nombre", c))
                    using (var rd = cmd.ExecuteReader())
                    {
                        while (rd.Read())
                        {
                            list.Add(new PromoActivaHoy
                            {
                                PromoID = Convert.ToInt32(rd["PromoID"]),
                                Nombre = rd["Nombre"]?.ToString() ?? "",
                                Tipo = rd["Tipo"]?.ToString() ?? "",
                                ProductoID = rd["ProductoID"] == DBNull.Value ? (int?)null : Convert.ToInt32(rd["ProductoID"]),
                                Categoria = rd["Categoria"]?.ToString() ?? "",
                                Modalidad = rd["Modalidad"]?.ToString() ?? ModalidadesPromo.Porcentaje,
                                Porcentaje = Convert.ToDecimal(rd["Porcentaje"]),
                                MontoFijo = Convert.ToDecimal(rd["MontoFijo"]),
                                PrecioCombo = Convert.ToDecimal(rd["PrecioCombo"]),
                                CantidadMinima = Convert.ToInt32(rd["CantidadMinima"]),
                                CantidadBonificada = Convert.ToInt32(rd["CantidadBonificada"]),
                                AlcanceTexto = rd["AlcanceTexto"]?.ToString() ?? "",
                                ProductoIDs = ParseIdsCsv(rd["ComboProductoIDs"]?.ToString()),
                                ProductosTexto = rd["ComboProductos"]?.ToString() ?? ""
                            });
                        }
                    }
                }
            }
            catch { /* silencioso: el POS sigue sin banner */ }
            return list;
        }

        /// <summary>
        /// Elige la promo más específica (producto &gt; categoría &gt; todo)
        /// y, a igual alcance, la de mayor porcentaje.
        /// </summary>
        public static PromoVigente ResolverMejorPromo(IList<PromoActivaHoy> promos, int productoId, string categoria)
        {
            if (promos == null || promos.Count == 0) return null;
            string cat = (categoria ?? "").Trim();
            PromoActivaHoy best = null;
            int bestRank = 99;
            foreach (var p in promos)
            {
                int rank;
                if (p.Tipo == TiposPromo.PctProducto && p.ProductoID.HasValue && p.ProductoID.Value == productoId)
                    rank = 1;
                else if (p.Tipo == TiposPromo.ComboProductos && p.ProductoIDs != null && p.ProductoIDs.Contains(productoId))
                    rank = 2;
                else if (p.Tipo == TiposPromo.PctCategoria
                         && string.Equals((p.Categoria ?? "").Trim(), cat, StringComparison.OrdinalIgnoreCase)
                         && !string.IsNullOrEmpty(cat))
                    rank = 3;
                else if (p.Tipo == TiposPromo.PctTodos)
                    rank = 4;
                else
                    continue;

                if (best == null || rank < bestRank || (rank == bestRank && p.Porcentaje > best.Porcentaje))
                {
                    best = p;
                    bestRank = rank;
                }
            }
            if (best == null) return null;
            return new PromoVigente
            {
                PromoID = best.PromoID,
                Nombre = best.Nombre,
                Tipo = best.Tipo,
                Modalidad = best.Modalidad,
                Porcentaje = best.Porcentaje,
                MontoFijo = best.MontoFijo,
                PrecioCombo = best.PrecioCombo,
                CantidadMinima = best.CantidadMinima,
                CantidadBonificada = best.CantidadBonificada,
                ProductoIDs = best.ProductoIDs ?? new List<int>()
            };
        }

        /// <summary>
        /// Devuelve la promo vigente más específica (producto &gt; categoría &gt; todo)
        /// y, a igual alcance, la de mayor porcentaje.
        /// </summary>
        public static PromoVigente ObtenerPromoVigenteParaProducto(int productoId, string categoria)
        {
            try
            {
                return ResolverMejorPromo(GetPromocionesVigentesHoy(), productoId, categoria);
            }
            catch { return null; }
        }
    }
}
