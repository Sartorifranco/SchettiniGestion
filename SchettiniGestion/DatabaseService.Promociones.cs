using System;
using System.Data;
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
        }

        public class PromoVigente
        {
            public int PromoID { get; set; }
            public string Nombre { get; set; }
            public decimal Porcentaje { get; set; }
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
    Porcentaje DECIMAL(9,4) NOT NULL DEFAULT 0,
    FechaDesde DATE NULL,
    FechaHasta DATE NULL,
    Activo BIT NOT NULL DEFAULT 1,
    Observaciones NVARCHAR(250) NULL
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
            return tipo ?? "";
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
SELECT pr.PromoID, pr.Nombre, pr.Tipo, pr.ProductoID, pr.Categoria, pr.Porcentaje,
       pr.FechaDesde, pr.FechaHasta, pr.Activo, pr.Observaciones,
       p.Descripcion AS ProductoNombre,
       CASE pr.Tipo
         WHEN 'PCT_PRODUCTO' THEN N'Un producto'
         WHEN 'PCT_CATEGORIA' THEN N'Una categoría'
         WHEN 'PCT_TODOS' THEN N'Todo el local'
         ELSE pr.Tipo END AS TipoEtiqueta,
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
        {
            try
            {
                if (string.IsNullOrWhiteSpace(nombre))
                {
                    NotificarError("Ingresá un nombre para la promoción.");
                    return false;
                }
                if (porcentaje <= 0 || porcentaje > 100)
                {
                    NotificarError("El descuento tiene que ser entre 1 y 100%.");
                    return false;
                }

                tipo = (tipo ?? "").Trim();
                if (tipo != TiposPromo.PctProducto && tipo != TiposPromo.PctCategoria && tipo != TiposPromo.PctTodos)
                    tipo = TiposPromo.PctProducto;

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
                if (desde.HasValue && hasta.HasValue && hasta.Value.Date < desde.Value.Date)
                {
                    NotificarError("La fecha hasta no puede ser anterior a la fecha desde.");
                    return false;
                }

                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    AsegurarTablaPromociones(c);
                    string sql = id == 0
                        ? @"INSERT INTO Promociones (Nombre, Tipo, ProductoID, Categoria, Porcentaje, FechaDesde, FechaHasta, Activo, Observaciones)
                           VALUES (@n, @t, @pid, @cat, @pct, @d, @h, @a, @obs)"
                        : @"UPDATE Promociones SET Nombre=@n, Tipo=@t, ProductoID=@pid, Categoria=@cat, Porcentaje=@pct,
                           FechaDesde=@d, FechaHasta=@h, Activo=@a, Observaciones=@obs WHERE PromoID=@id";
                    using (var cmd = new SqlCommand(sql, c))
                    {
                        cmd.Parameters.AddWithValue("@n", nombre.Trim());
                        cmd.Parameters.AddWithValue("@t", tipo);
                        cmd.Parameters.AddWithValue("@pid", tipo == TiposPromo.PctProducto ? (object)productoId.Value : DBNull.Value);
                        cmd.Parameters.AddWithValue("@cat", tipo == TiposPromo.PctCategoria ? (object)categoria.Trim() : DBNull.Value);
                        cmd.Parameters.AddWithValue("@pct", porcentaje);
                        cmd.Parameters.AddWithValue("@d", desde.HasValue ? (object)desde.Value.Date : DBNull.Value);
                        cmd.Parameters.AddWithValue("@h", hasta.HasValue ? (object)hasta.Value.Date : DBNull.Value);
                        cmd.Parameters.AddWithValue("@a", activo);
                        string obs = (observaciones ?? "").Trim();
                        cmd.Parameters.AddWithValue("@obs", string.IsNullOrEmpty(obs) ? (object)DBNull.Value : obs);
                        cmd.Parameters.AddWithValue("@id", id);
                        cmd.ExecuteNonQuery();
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
                    using (var cmd = new SqlCommand("DELETE FROM Promociones WHERE PromoID=@id", c))
                    {
                        cmd.Parameters.AddWithValue("@id", id);
                        cmd.ExecuteNonQuery();
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

        /// <summary>
        /// Devuelve la promo vigente más específica (producto &gt; categoría &gt; todo)
        /// y, a igual alcance, la de mayor porcentaje.
        /// </summary>
        public static PromoVigente ObtenerPromoVigenteParaProducto(int productoId, string categoria)
        {
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    AsegurarTablaPromociones(c);
                    using (var cmd = new SqlCommand(@"
SELECT TOP 1 PromoID, Nombre, Porcentaje
FROM Promociones
WHERE Activo = 1
  AND (FechaDesde IS NULL OR FechaDesde <= CAST(GETDATE() AS DATE))
  AND (FechaHasta IS NULL OR FechaHasta >= CAST(GETDATE() AS DATE))
  AND (
        (Tipo = 'PCT_PRODUCTO' AND ProductoID = @pid)
     OR (Tipo = 'PCT_CATEGORIA' AND LTRIM(RTRIM(ISNULL(Categoria,N''))) = LTRIM(RTRIM(ISNULL(@cat,N''))))
     OR (Tipo = 'PCT_TODOS')
      )
ORDER BY
  CASE Tipo WHEN 'PCT_PRODUCTO' THEN 1 WHEN 'PCT_CATEGORIA' THEN 2 ELSE 3 END,
  Porcentaje DESC", c))
                    {
                        cmd.Parameters.AddWithValue("@pid", productoId);
                        cmd.Parameters.AddWithValue("@cat", categoria ?? "");
                        using (var rd = cmd.ExecuteReader())
                        {
                            if (!rd.Read()) return null;
                            return new PromoVigente
                            {
                                PromoID = Convert.ToInt32(rd["PromoID"]),
                                Nombre = rd["Nombre"]?.ToString() ?? "",
                                Porcentaje = Convert.ToDecimal(rd["Porcentaje"])
                            };
                        }
                    }
                }
            }
            catch { return null; }
        }
    }
}
