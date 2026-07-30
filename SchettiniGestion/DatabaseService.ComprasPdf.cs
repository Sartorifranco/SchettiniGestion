using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using SqlConnection = Microsoft.Data.SqlClient.SqlConnection;
using SqlCommand = Microsoft.Data.SqlClient.SqlCommand;
using SqlDataAdapter = Microsoft.Data.SqlClient.SqlDataAdapter;

namespace SchettiniGestion
{
    public class ProductoMatchCatalogo
    {
        public int ProductoID { get; set; }
        public string Codigo { get; set; }
        public string CodigoBarra { get; set; }
        public string CodigoExterno { get; set; }
        public string Descripcion { get; set; }
        public decimal PrecioCosto { get; set; }
    }

    public static partial class DatabaseService
    {
        private static bool _tablaAliasProveedorOk;
        private static readonly object _lockAliasProveedor = new object();

        private static void AsegurarTablaProductoAliasProveedor(SqlConnection c)
        {
            if (_tablaAliasProveedorOk) return;
            lock (_lockAliasProveedor)
            {
                if (_tablaAliasProveedorOk) return;
                try
                {
                    using (var cmd = new SqlCommand(@"
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME='ProductoAliasProveedor')
  CREATE TABLE dbo.ProductoAliasProveedor (
    AliasID INT IDENTITY(1,1) PRIMARY KEY,
    ProveedorID INT NOT NULL,
    DescripcionProveedor NVARCHAR(300) NOT NULL,
    ProductoID INT NOT NULL,
    CodigoProveedor NVARCHAR(100) NULL,
    UltimoUso DATETIME NOT NULL DEFAULT GETDATE(),
    CONSTRAINT UQ_ProductoAliasProveedor UNIQUE (ProveedorID, DescripcionProveedor)
  );", c))
                    {
                        cmd.ExecuteNonQuery();
                    }
                    _tablaAliasProveedorOk = true;
                }
                catch { }
            }
        }

        /// <summary>Normaliza descripción del proveedor para aliases y matching.</summary>
        public static string NormalizarDescripcionProveedor(string texto)
        {
            if (string.IsNullOrWhiteSpace(texto)) return "";
            string t = texto.Trim().ToUpperInvariant();
            t = t.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder(t.Length);
            foreach (char ch in t)
            {
                var cat = CharUnicodeInfo.GetUnicodeCategory(ch);
                if (cat != UnicodeCategory.NonSpacingMark)
                    sb.Append(ch);
            }
            t = sb.ToString().Normalize(NormalizationForm.FormC);
            t = Regex.Replace(t, @"[^A-Z0-9\s]", " ");
            t = Regex.Replace(t, @"\s+", " ").Trim();
            if (t.Length > 300) t = t.Substring(0, 300);
            return t;
        }

        public static DataRow BuscarProveedorPorCuit(string cuit)
        {
            if (string.IsNullOrWhiteSpace(cuit)) return null;
            string digits = Regex.Replace(cuit, @"[^\d]", "");
            if (digits.Length < 10) return null;
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    using (var cmd = new SqlCommand(@"
SELECT TOP 1 * FROM Proveedores
WHERE REPLACE(REPLACE(REPLACE(ISNULL(CUIT,''),'-',''),' ',''),'.','') = @d
   OR CUIT LIKE @like", c))
                    {
                        cmd.Parameters.AddWithValue("@d", digits);
                        cmd.Parameters.AddWithValue("@like", "%" + digits + "%");
                        var dt = new DataTable();
                        new SqlDataAdapter(cmd).Fill(dt);
                        if (dt.Rows.Count > 0) return dt.Rows[0];
                    }
                }
            }
            catch { }
            return null;
        }

        public static List<ProductoMatchCatalogo> GetProductosCatalogoMatchCompra()
        {
            var list = new List<ProductoMatchCatalogo>();
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    AsegurarMigracionLite(c);
                    string sql = @"
SELECT ProductoID,
       ISNULL(Codigo,'') AS Codigo,
       ISNULL(CodigoBarra,'') AS CodigoBarra,
       ISNULL(CodigoExterno,'') AS CodigoExterno,
       ISNULL(Descripcion,'') AS Descripcion,
       ISNULL(PrecioCosto,0) AS PrecioCosto
FROM Productos
ORDER BY Descripcion";
                    using (var cmd = new SqlCommand(sql, c))
                    using (var rd = cmd.ExecuteReader())
                    {
                        while (rd.Read())
                        {
                            list.Add(new ProductoMatchCatalogo
                            {
                                ProductoID = Convert.ToInt32(rd["ProductoID"]),
                                Codigo = rd["Codigo"]?.ToString() ?? "",
                                CodigoBarra = rd["CodigoBarra"]?.ToString() ?? "",
                                CodigoExterno = rd["CodigoExterno"]?.ToString() ?? "",
                                Descripcion = rd["Descripcion"]?.ToString() ?? "",
                                PrecioCosto = rd["PrecioCosto"] != DBNull.Value ? Convert.ToDecimal(rd["PrecioCosto"]) : 0m
                            });
                        }
                    }
                }
            }
            catch { }
            return list;
        }

        public static int? BuscarAliasProductoProveedor(int proveedorId, string descripcionProveedor, string codigoProveedor = null)
        {
            if (proveedorId <= 0) return null;
            string norm = NormalizarDescripcionProveedor(descripcionProveedor);
            if (string.IsNullOrEmpty(norm) && string.IsNullOrWhiteSpace(codigoProveedor)) return null;
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    AsegurarTablaProductoAliasProveedor(c);
                    if (!string.IsNullOrWhiteSpace(codigoProveedor))
                    {
                        using (var cmd = new SqlCommand(@"
SELECT TOP 1 ProductoID FROM ProductoAliasProveedor
WHERE ProveedorID=@pid AND CodigoProveedor=@cod
ORDER BY UltimoUso DESC", c))
                        {
                            cmd.Parameters.AddWithValue("@pid", proveedorId);
                            cmd.Parameters.AddWithValue("@cod", codigoProveedor.Trim());
                            object o = cmd.ExecuteScalar();
                            if (o != null && o != DBNull.Value) return Convert.ToInt32(o);
                        }
                    }
                    if (!string.IsNullOrEmpty(norm))
                    {
                        using (var cmd = new SqlCommand(@"
SELECT TOP 1 ProductoID FROM ProductoAliasProveedor
WHERE ProveedorID=@pid AND DescripcionProveedor=@desc", c))
                        {
                            cmd.Parameters.AddWithValue("@pid", proveedorId);
                            cmd.Parameters.AddWithValue("@desc", norm);
                            object o = cmd.ExecuteScalar();
                            if (o != null && o != DBNull.Value) return Convert.ToInt32(o);
                        }
                    }
                }
            }
            catch { }
            return null;
        }

        public static void GuardarAliasProductoProveedor(int proveedorId, string descripcionProveedor, int productoId, string codigoProveedor = null)
        {
            if (proveedorId <= 0 || productoId <= 0) return;
            string norm = NormalizarDescripcionProveedor(descripcionProveedor);
            if (string.IsNullOrEmpty(norm)) return;
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    AsegurarTablaProductoAliasProveedor(c);
                    using (var cmd = new SqlCommand(@"
IF EXISTS (SELECT 1 FROM ProductoAliasProveedor WHERE ProveedorID=@pid AND DescripcionProveedor=@desc)
  UPDATE ProductoAliasProveedor
  SET ProductoID=@prod, CodigoProveedor=@cod, UltimoUso=GETDATE()
  WHERE ProveedorID=@pid AND DescripcionProveedor=@desc;
ELSE
  INSERT INTO ProductoAliasProveedor (ProveedorID, DescripcionProveedor, ProductoID, CodigoProveedor, UltimoUso)
  VALUES (@pid, @desc, @prod, @cod, GETDATE());", c))
                    {
                        cmd.Parameters.AddWithValue("@pid", proveedorId);
                        cmd.Parameters.AddWithValue("@desc", norm);
                        cmd.Parameters.AddWithValue("@prod", productoId);
                        cmd.Parameters.AddWithValue("@cod", (object)codigoProveedor?.Trim() ?? DBNull.Value);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch { }
        }
    }
}
