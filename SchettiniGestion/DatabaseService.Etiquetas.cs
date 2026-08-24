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
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Configuracion' AND COLUMN_NAME='EtiquetaGapHorizontalMm')
  ALTER TABLE Configuracion ADD EtiquetaGapHorizontalMm INT NOT NULL DEFAULT 2;
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Configuracion' AND COLUMN_NAME='EtiquetaGapVerticalMm')
  ALTER TABLE Configuracion ADD EtiquetaGapVerticalMm INT NOT NULL DEFAULT 2;
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Configuracion' AND COLUMN_NAME='EtiquetaMargenIzquierdoMm')
  ALTER TABLE Configuracion ADD EtiquetaMargenIzquierdoMm INT NOT NULL DEFAULT 5;
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Configuracion' AND COLUMN_NAME='EtiquetaMargenSuperiorMm')
  ALTER TABLE Configuracion ADD EtiquetaMargenSuperiorMm INT NOT NULL DEFAULT 5;
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Configuracion' AND COLUMN_NAME='EtiquetaMargenDerechoMm')
  ALTER TABLE Configuracion ADD EtiquetaMargenDerechoMm INT NOT NULL DEFAULT 5;
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Configuracion' AND COLUMN_NAME='EtiquetaMargenInferiorMm')
  ALTER TABLE Configuracion ADD EtiquetaMargenInferiorMm INT NOT NULL DEFAULT 5;
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Configuracion' AND COLUMN_NAME='EtiquetaColumnas')
  ALTER TABLE Configuracion ADD EtiquetaColumnas INT NOT NULL DEFAULT 3;
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Configuracion' AND COLUMN_NAME='EtiquetaOrientacion')
  ALTER TABLE Configuracion ADD EtiquetaOrientacion NVARCHAR(20) NOT NULL DEFAULT 'Vertical';
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Configuracion' AND COLUMN_NAME='EtiquetaModoImpresion')
  ALTER TABLE Configuracion ADD EtiquetaModoImpresion NVARCHAR(20) NOT NULL DEFAULT 'Rollo';
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Configuracion' AND COLUMN_NAME='EtiquetaMostrarDescripcion')
  ALTER TABLE Configuracion ADD EtiquetaMostrarDescripcion BIT NOT NULL DEFAULT 1;
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Configuracion' AND COLUMN_NAME='EtiquetaMostrarDescripcionExtra')
  ALTER TABLE Configuracion ADD EtiquetaMostrarDescripcionExtra BIT NOT NULL DEFAULT 0;
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Configuracion' AND COLUMN_NAME='EtiquetaMostrarPrecio')
  ALTER TABLE Configuracion ADD EtiquetaMostrarPrecio BIT NOT NULL DEFAULT 1;
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Configuracion' AND COLUMN_NAME='EtiquetaMostrarCodigo')
  ALTER TABLE Configuracion ADD EtiquetaMostrarCodigo BIT NOT NULL DEFAULT 0;
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Configuracion' AND COLUMN_NAME='EtiquetaMostrarBarras')
  ALTER TABLE Configuracion ADD EtiquetaMostrarBarras BIT NOT NULL DEFAULT 1;
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Configuracion' AND COLUMN_NAME='EtiquetaMostrarMarca')
  ALTER TABLE Configuracion ADD EtiquetaMostrarMarca BIT NOT NULL DEFAULT 0;
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Configuracion' AND COLUMN_NAME='EtiquetaAutoCorte')
  ALTER TABLE Configuracion ADD EtiquetaAutoCorte BIT NOT NULL DEFAULT 0;
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Configuracion' AND COLUMN_NAME='EtiquetaProtocoloCorte')
  ALTER TABLE Configuracion ADD EtiquetaProtocoloCorte NVARCHAR(20) NOT NULL DEFAULT 'Auto';", c))
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
                if (dr.Table.Columns.Contains("EtiquetaGapHorizontalMm") && dr["EtiquetaGapHorizontalMm"] != DBNull.Value)
                    op.GapHorizontalMm = Math.Max(0, Convert.ToInt32(dr["EtiquetaGapHorizontalMm"]));
                if (dr.Table.Columns.Contains("EtiquetaGapVerticalMm") && dr["EtiquetaGapVerticalMm"] != DBNull.Value)
                    op.GapVerticalMm = Math.Max(0, Convert.ToInt32(dr["EtiquetaGapVerticalMm"]));
                if (dr.Table.Columns.Contains("EtiquetaMargenIzquierdoMm") && dr["EtiquetaMargenIzquierdoMm"] != DBNull.Value)
                    op.MargenIzquierdoMm = Math.Max(0, Convert.ToInt32(dr["EtiquetaMargenIzquierdoMm"]));
                if (dr.Table.Columns.Contains("EtiquetaMargenSuperiorMm") && dr["EtiquetaMargenSuperiorMm"] != DBNull.Value)
                    op.MargenSuperiorMm = Math.Max(0, Convert.ToInt32(dr["EtiquetaMargenSuperiorMm"]));
                if (dr.Table.Columns.Contains("EtiquetaMargenDerechoMm") && dr["EtiquetaMargenDerechoMm"] != DBNull.Value)
                    op.MargenDerechoMm = Math.Max(0, Convert.ToInt32(dr["EtiquetaMargenDerechoMm"]));
                if (dr.Table.Columns.Contains("EtiquetaMargenInferiorMm") && dr["EtiquetaMargenInferiorMm"] != DBNull.Value)
                    op.MargenInferiorMm = Math.Max(0, Convert.ToInt32(dr["EtiquetaMargenInferiorMm"]));
                if (dr.Table.Columns.Contains("EtiquetaColumnas") && dr["EtiquetaColumnas"] != DBNull.Value)
                    op.Columnas = Math.Max(1, Convert.ToInt32(dr["EtiquetaColumnas"]));
                if (dr.Table.Columns.Contains("EtiquetaOrientacion") && dr["EtiquetaOrientacion"] != DBNull.Value)
                    op.Orientacion = string.IsNullOrWhiteSpace(dr["EtiquetaOrientacion"].ToString()) ? "Vertical" : dr["EtiquetaOrientacion"].ToString();
                if (dr.Table.Columns.Contains("EtiquetaModoImpresion") && dr["EtiquetaModoImpresion"] != DBNull.Value)
                    op.ModoImpresion = string.IsNullOrWhiteSpace(dr["EtiquetaModoImpresion"].ToString()) ? "Rollo" : dr["EtiquetaModoImpresion"].ToString();
                if (dr.Table.Columns.Contains("EtiquetaMostrarDescripcion") && dr["EtiquetaMostrarDescripcion"] != DBNull.Value)
                    op.MostrarDescripcion = Convert.ToBoolean(dr["EtiquetaMostrarDescripcion"]);
                if (dr.Table.Columns.Contains("EtiquetaMostrarDescripcionExtra") && dr["EtiquetaMostrarDescripcionExtra"] != DBNull.Value)
                    op.MostrarDescripcionExtra = Convert.ToBoolean(dr["EtiquetaMostrarDescripcionExtra"]);
                if (dr.Table.Columns.Contains("EtiquetaMostrarPrecio") && dr["EtiquetaMostrarPrecio"] != DBNull.Value)
                    op.MostrarPrecio = Convert.ToBoolean(dr["EtiquetaMostrarPrecio"]);
                if (dr.Table.Columns.Contains("EtiquetaMostrarCodigo") && dr["EtiquetaMostrarCodigo"] != DBNull.Value)
                    op.MostrarCodigo = Convert.ToBoolean(dr["EtiquetaMostrarCodigo"]);
                if (dr.Table.Columns.Contains("EtiquetaMostrarBarras") && dr["EtiquetaMostrarBarras"] != DBNull.Value)
                    op.MostrarCodigoBarras = Convert.ToBoolean(dr["EtiquetaMostrarBarras"]);
                if (dr.Table.Columns.Contains("EtiquetaMostrarMarca") && dr["EtiquetaMostrarMarca"] != DBNull.Value)
                    op.MostrarMarca = Convert.ToBoolean(dr["EtiquetaMostrarMarca"]);
                if (dr.Table.Columns.Contains("EtiquetaAutoCorte") && dr["EtiquetaAutoCorte"] != DBNull.Value)
                    op.AutoCorte = Convert.ToBoolean(dr["EtiquetaAutoCorte"]);
                if (dr.Table.Columns.Contains("EtiquetaProtocoloCorte") && dr["EtiquetaProtocoloCorte"] != DBNull.Value)
                {
                    string proto = dr["EtiquetaProtocoloCorte"]?.ToString()?.Trim();
                    op.ProtocoloCorte = string.IsNullOrWhiteSpace(proto) ? "Auto" : proto;
                }
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
                int gapH = Math.Max(0, Math.Min(50, opciones.GapHorizontalMm));
                int gapV = Math.Max(0, Math.Min(50, opciones.GapVerticalMm));
                int margenIzq = Math.Max(0, Math.Min(50, opciones.MargenIzquierdoMm));
                int margenSup = Math.Max(0, Math.Min(50, opciones.MargenSuperiorMm));
                int margenDer = Math.Max(0, Math.Min(50, opciones.MargenDerechoMm));
                int margenInf = Math.Max(0, Math.Min(50, opciones.MargenInferiorMm));
                int columnas = Math.Max(1, Math.Min(12, opciones.Columnas));
                string orientacion = string.IsNullOrWhiteSpace(opciones.Orientacion) ? "Vertical" : opciones.Orientacion.Trim();
                string modo = string.IsNullOrWhiteSpace(opciones.ModoImpresion) ? "Rollo" : opciones.ModoImpresion.Trim();
                string protocoloCorte = NormalizarProtocoloCorte(opciones.ProtocoloCorte);

                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    AsegurarColumnasEtiquetas(c);
                    using (var cmd = new SqlCommand(@"
UPDATE Configuracion SET
  ImpresoraEtiquetas=@ie,
  EtiquetaAnchoMm=@an,
  EtiquetaAltoMm=@al,
  EtiquetaGapHorizontalMm=@gh,
  EtiquetaGapVerticalMm=@gv,
  EtiquetaMargenIzquierdoMm=@mi,
  EtiquetaMargenSuperiorMm=@ms,
  EtiquetaMargenDerechoMm=@mdr,
  EtiquetaMargenInferiorMm=@mif,
  EtiquetaColumnas=@cols,
  EtiquetaOrientacion=@ori,
  EtiquetaModoImpresion=@modo,
  EtiquetaAutoCorte=@ac,
  EtiquetaProtocoloCorte=@pc,
  EtiquetaMostrarDescripcion=@md,
  EtiquetaMostrarDescripcionExtra=@mde,
  EtiquetaMostrarPrecio=@mp,
  EtiquetaMostrarCodigo=@mc,
  EtiquetaMostrarBarras=@mb,
  EtiquetaMostrarMarca=@mm
WHERE ID=1", c))
                    {
                        cmd.Parameters.AddWithValue("@ie", string.IsNullOrWhiteSpace(impresoraEtiquetas) ? (object)DBNull.Value : impresoraEtiquetas);
                        cmd.Parameters.AddWithValue("@an", ancho);
                        cmd.Parameters.AddWithValue("@al", alto);
                        cmd.Parameters.AddWithValue("@gh", gapH);
                        cmd.Parameters.AddWithValue("@gv", gapV);
                        cmd.Parameters.AddWithValue("@mi", margenIzq);
                        cmd.Parameters.AddWithValue("@ms", margenSup);
                        cmd.Parameters.AddWithValue("@mdr", margenDer);
                        cmd.Parameters.AddWithValue("@mif", margenInf);
                        cmd.Parameters.AddWithValue("@cols", columnas);
                        cmd.Parameters.AddWithValue("@ori", orientacion);
                        cmd.Parameters.AddWithValue("@modo", modo);
                        cmd.Parameters.AddWithValue("@ac", opciones.AutoCorte);
                        cmd.Parameters.AddWithValue("@pc", protocoloCorte);
                        cmd.Parameters.AddWithValue("@md", opciones.MostrarDescripcion);
                        cmd.Parameters.AddWithValue("@mde", opciones.MostrarDescripcionExtra);
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

        private static string NormalizarProtocoloCorte(string protocolo)
        {
            string p = (protocolo ?? "").Trim();
            if (string.Equals(p, "ESCPOS", StringComparison.OrdinalIgnoreCase)
                || string.Equals(p, "ESC/POS", StringComparison.OrdinalIgnoreCase))
                return "ESCPOS";
            if (string.Equals(p, "TSPL", StringComparison.OrdinalIgnoreCase)) return "TSPL";
            if (string.Equals(p, "ZPL", StringComparison.OrdinalIgnoreCase)) return "ZPL";
            if (string.Equals(p, "EPL", StringComparison.OrdinalIgnoreCase)) return "EPL";
            return "Auto";
        }
    }
}
