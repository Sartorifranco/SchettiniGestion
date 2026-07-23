using System;
using System.Data;
using SqlConnection = Microsoft.Data.SqlClient.SqlConnection;
using SqlCommand = Microsoft.Data.SqlClient.SqlCommand;

namespace SchettiniGestion
{
    public static partial class DatabaseService
    {
        private static bool _columnasEtiquetasOk;
        private static readonly object _lockEtiquetas = new object();

        private static void AsegurarColumnasEtiquetas(SqlConnection c)
        {
            if (_columnasEtiquetasOk) return;
            lock (_lockEtiquetas)
            {
                if (_columnasEtiquetasOk) return;
                try
                {
                    using (var cmd = new SqlCommand(@"
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Configuracion' AND COLUMN_NAME='ImpresoraEtiquetas')
  ALTER TABLE Configuracion ADD ImpresoraEtiquetas NVARCHAR(256) NULL;
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Configuracion' AND COLUMN_NAME='EtiquetaAnchoMm')
  ALTER TABLE Configuracion ADD EtiquetaAnchoMm INT NOT NULL DEFAULT 50;
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Configuracion' AND COLUMN_NAME='EtiquetaAltoMm')
  ALTER TABLE Configuracion ADD EtiquetaAltoMm INT NOT NULL DEFAULT 25;
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Configuracion' AND COLUMN_NAME='EtiquetaMostrarDescripcion')
  ALTER TABLE Configuracion ADD EtiquetaMostrarDescripcion BIT NOT NULL DEFAULT 1;
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Configuracion' AND COLUMN_NAME='EtiquetaMostrarPrecio')
  ALTER TABLE Configuracion ADD EtiquetaMostrarPrecio BIT NOT NULL DEFAULT 1;
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Configuracion' AND COLUMN_NAME='EtiquetaMostrarCodigo')
  ALTER TABLE Configuracion ADD EtiquetaMostrarCodigo BIT NOT NULL DEFAULT 0;
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Configuracion' AND COLUMN_NAME='EtiquetaMostrarBarras')
  ALTER TABLE Configuracion ADD EtiquetaMostrarBarras BIT NOT NULL DEFAULT 1;
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Configuracion' AND COLUMN_NAME='EtiquetaMostrarMarca')
  ALTER TABLE Configuracion ADD EtiquetaMostrarMarca BIT NOT NULL DEFAULT 0;", c))
                        cmd.ExecuteNonQuery();
                    _columnasEtiquetasOk = true;
                }
                catch { /* sin permiso ALTER */ }
            }
        }

        public static string GetImpresoraEtiquetas()
        {
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    AsegurarColumnasEtiquetas(c);
                }
                DataRow dr = GetConfiguracion();
                if (dr == null || !dr.Table.Columns.Contains("ImpresoraEtiquetas")) return null;
                string t = dr["ImpresoraEtiquetas"]?.ToString();
                return string.IsNullOrWhiteSpace(t) ? null : t;
            }
            catch { return null; }
        }

        public static OpcionesEtiqueta GetOpcionesEtiqueta()
        {
            var op = new OpcionesEtiqueta();
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    AsegurarColumnasEtiquetas(c);
                }
                DataRow dr = GetConfiguracion();
                if (dr == null) return op;

                if (dr.Table.Columns.Contains("EtiquetaAnchoMm") && dr["EtiquetaAnchoMm"] != DBNull.Value)
                    op.AnchoMm = Math.Max(10, Convert.ToInt32(dr["EtiquetaAnchoMm"]));
                if (dr.Table.Columns.Contains("EtiquetaAltoMm") && dr["EtiquetaAltoMm"] != DBNull.Value)
                    op.AltoMm = Math.Max(10, Convert.ToInt32(dr["EtiquetaAltoMm"]));
                if (dr.Table.Columns.Contains("EtiquetaMostrarDescripcion") && dr["EtiquetaMostrarDescripcion"] != DBNull.Value)
                    op.MostrarDescripcion = Convert.ToBoolean(dr["EtiquetaMostrarDescripcion"]);
                if (dr.Table.Columns.Contains("EtiquetaMostrarPrecio") && dr["EtiquetaMostrarPrecio"] != DBNull.Value)
                    op.MostrarPrecio = Convert.ToBoolean(dr["EtiquetaMostrarPrecio"]);
                if (dr.Table.Columns.Contains("EtiquetaMostrarCodigo") && dr["EtiquetaMostrarCodigo"] != DBNull.Value)
                    op.MostrarCodigo = Convert.ToBoolean(dr["EtiquetaMostrarCodigo"]);
                if (dr.Table.Columns.Contains("EtiquetaMostrarBarras") && dr["EtiquetaMostrarBarras"] != DBNull.Value)
                    op.MostrarCodigoBarras = Convert.ToBoolean(dr["EtiquetaMostrarBarras"]);
                if (dr.Table.Columns.Contains("EtiquetaMostrarMarca") && dr["EtiquetaMostrarMarca"] != DBNull.Value)
                    op.MostrarMarca = Convert.ToBoolean(dr["EtiquetaMostrarMarca"]);
            }
            catch { }
            return op;
        }

        public static bool GuardarConfigEtiquetas(string impresoraEtiquetas, OpcionesEtiqueta opciones)
        {
            try
            {
                if (opciones == null) opciones = new OpcionesEtiqueta();
                int ancho = Math.Max(10, Math.Min(300, opciones.AnchoMm));
                int alto = Math.Max(10, Math.Min(300, opciones.AltoMm));

                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    AsegurarColumnasEtiquetas(c);
                    using (var cmd = new SqlCommand(@"
UPDATE Configuracion SET
  ImpresoraEtiquetas=@ie,
  EtiquetaAnchoMm=@an,
  EtiquetaAltoMm=@al,
  EtiquetaMostrarDescripcion=@md,
  EtiquetaMostrarPrecio=@mp,
  EtiquetaMostrarCodigo=@mc,
  EtiquetaMostrarBarras=@mb,
  EtiquetaMostrarMarca=@mm
WHERE ID=1", c))
                    {
                        cmd.Parameters.AddWithValue("@ie", string.IsNullOrWhiteSpace(impresoraEtiquetas) ? (object)DBNull.Value : impresoraEtiquetas);
                        cmd.Parameters.AddWithValue("@an", ancho);
                        cmd.Parameters.AddWithValue("@al", alto);
                        cmd.Parameters.AddWithValue("@md", opciones.MostrarDescripcion);
                        cmd.Parameters.AddWithValue("@mp", opciones.MostrarPrecio);
                        cmd.Parameters.AddWithValue("@mc", opciones.MostrarCodigo);
                        cmd.Parameters.AddWithValue("@mb", opciones.MostrarCodigoBarras);
                        cmd.Parameters.AddWithValue("@mm", opciones.MostrarMarca);
                        cmd.ExecuteNonQuery();
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                NotificarError("Etiquetas: " + ex.Message);
                return false;
            }
        }
    }
}
