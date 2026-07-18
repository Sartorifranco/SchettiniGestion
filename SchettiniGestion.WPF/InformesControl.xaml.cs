using System;
using System.Data;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using SchettiniGestion;

namespace SchettiniGestion.WPF
{
    public partial class InformesControl : UserControl
    {
        private DataTable _dtActual;

        public InformesControl() { InitializeComponent(); }
        public InformesControl(object param) : this() { }

        private void Control_Loaded(object sender, RoutedEventArgs e)
        {
            dpDesde.SelectedDate = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            dpHasta.SelectedDate = DateTime.Today;
        }

        private void cmbTipoInforme_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dpDesde == null || dpHasta == null) return;
            string tipo = (cmbTipoInforme.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "";
            bool usaFechas = tipo != "Valorización de Stock";
            dpDesde.IsEnabled = usaFechas;
            dpHasta.IsEnabled = usaFechas;
        }

        private void btnGenerar_Click(object sender, RoutedEventArgs e)
        {
            DateTime desde = dpDesde.SelectedDate ?? new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            DateTime hasta = (dpHasta.SelectedDate ?? DateTime.Today).AddDays(1).AddSeconds(-1);
            string tipo = (cmbTipoInforme.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Ventas por Período";

            try
            {
                _dtActual = GenerarInforme(tipo, desde, hasta);
                dgvInforme.ItemsSource = _dtActual?.DefaultView;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al generar informe: " + ex.Message);
            }
        }

        private DataTable GenerarInforme(string tipo, DateTime desde, DateTime hasta)
        {
            using (var conn = new System.Data.SqlClient.SqlConnection(DatabaseService.ConnectionString))
            {
                conn.Open();
                string sql;
                switch (tipo)
                {
                    case "Ventas por Período":
                        sql = @"SELECT f.Fecha, f.TipoComprobante, ISNULL(c.RazonSocial,'Consumidor Final') AS Cliente,
                                       ISNULL(f.NombrePersonal,'') AS Personal, f.Total, f.CAE, f.NumeroComprobanteAFIP AS NroComprobante
                                FROM Facturas f LEFT JOIN Clientes c ON f.ClienteID=c.ClienteID
                                WHERE f.Fecha >= @d AND f.Fecha <= @h ORDER BY f.Fecha DESC";
                        break;
                    case "Libro IVA Ventas":
                        // Neto/IVA por línea desde TipoIVA del producto (no total/1.21 global)
                        sql = @";WITH lv AS (
  SELECT fd.FacturaID,
    fd.Cantidad * fd.PrecioUnitario AS Subtot,
    CASE WHEN UPPER(ISNULL(pr.TipoIVA,N'')) LIKE N'%EXE%' OR UPPER(ISNULL(pr.TipoIVA,N'')) LIKE N'%NO GRAVA%' THEN 0.0
         WHEN ISNULL(pr.TipoIVA,N'') LIKE N'%10%' THEN 10.5
         ELSE 21.0 END AS Pct
  FROM FacturaDetalle fd
  INNER JOIN Facturas fa ON fd.FacturaID=fa.FacturaID
  LEFT JOIN Productos pr ON fd.ProductoID=pr.ProductoID
  WHERE fa.Fecha>=@d AND fa.Fecha<=@h
), lin AS (
  SELECT FacturaID, Subtot, Pct,
    CASE WHEN Pct<=0.01 THEN Subtot ELSE ROUND(Subtot/(1.0+Pct/100.0), 2) END AS NetoLin,
    CASE WHEN Pct<=0.01 THEN 0 ELSE Subtot-ROUND(Subtot/(1.0+Pct/100.0), 2) END AS IvaLin
  FROM lv
), agg AS (
  SELECT FacturaID, SUM(NetoLin) AS Neto,SUM(IvaLin) AS IVA, SUM(Subtot) AS TotalDet
  FROM lin GROUP BY FacturaID
)
SELECT fa.Fecha, fa.TipoComprobante AS TipoComp, ISNULL(c.CUIT,'') AS CUIT,
       ISNULL(c.RazonSocial,'Consumidor Final') AS RazonSocial,
       ISNULL(c.CondicionIVA,'CF') AS CondIVA,
       a.Neto AS Neto, a.IVA AS IVA,
       CASE WHEN ABS(ISNULL(fa.Total,0)-ISNULL(a.TotalDet,0))<0.02 THEN fa.Total ELSE a.TotalDet END AS Total
FROM Facturas fa
INNER JOIN agg a ON fa.FacturaID=a.FacturaID
LEFT JOIN Clientes c ON fa.ClienteID=c.ClienteID
WHERE fa.Fecha>=@d AND fa.Fecha<=@h
ORDER BY fa.Fecha;";
                        break;
                    case "Libro IVA Compras":
                        sql = @";WITH lc AS (
  SELECT cd.CompraID,
    cd.Cantidad * cd.PrecioCosto AS Subtot,
    CASE WHEN UPPER(ISNULL(pr.TipoIVA,N'')) LIKE N'%EXE%' OR UPPER(ISNULL(pr.TipoIVA,N'')) LIKE N'%NO GRAVA%' THEN 0.0
         WHEN ISNULL(pr.TipoIVA,N'') LIKE N'%10%' THEN 10.5
         ELSE 21.0 END AS Pct
  FROM CompraDetalle cd
  INNER JOIN Compras cp ON cd.CompraID=cp.CompraID
  LEFT JOIN Productos pr ON cd.ProductoID=pr.ProductoID
  WHERE cp.Fecha>=@d AND cp.Fecha<=@h
), lin AS (
  SELECT CompraID, Subtot, Pct,
    CASE WHEN Pct<=0.01 THEN Subtot ELSE ROUND(Subtot/(1.0+Pct/100.0), 2) END AS NetoLin,
    CASE WHEN Pct<=0.01 THEN 0 ELSE Subtot-ROUND(Subtot/(1.0+Pct/100.0), 2) END AS IvaLin
  FROM lc
), agg AS (
  SELECT CompraID, SUM(NetoLin) AS Neto,SUM(IvaLin) AS IVA, SUM(Subtot) AS TotalDet
  FROM lin GROUP BY CompraID
)
SELECT cp.Fecha, cp.TipoComprobante AS TipoComp, ISNULL(pv.CUIT,'') AS CUIT,
       ISNULL(pv.RazonSocial,'') AS Proveedor,
       a.Neto AS Neto, a.IVA AS IVA,
       CASE WHEN ABS(ISNULL(cp.Total,0)-ISNULL(a.TotalDet,0))<0.02 THEN cp.Total ELSE a.TotalDet END AS Total
FROM Compras cp
INNER JOIN agg a ON cp.CompraID=a.CompraID
LEFT JOIN Proveedores pv ON cp.ProveedorID=pv.ProveedorID
WHERE cp.Fecha>=@d AND cp.Fecha<=@h
ORDER BY cp.Fecha;";
                        break;
                    case "Productos Más Vendidos":
                        sql = @"SELECT ISNULL(p.Descripcion,'Desconocido') AS Producto,
                                       SUM(fd.Cantidad) AS CantidadVendida,
                                       SUM(fd.Cantidad * fd.PrecioUnitario) AS TotalVendido
                                FROM FacturaDetalle fd
                                JOIN Facturas f ON fd.FacturaID=f.FacturaID
                                LEFT JOIN Productos p ON fd.ProductoID=p.ProductoID
                                WHERE f.Fecha >= @d AND f.Fecha <= @h
                                GROUP BY fd.ProductoID, p.Descripcion
                                ORDER BY CantidadVendida DESC";
                        break;
                    case "Ranking Clientes":
                        sql = @"SELECT ISNULL(c.RazonSocial,'Consumidor Final') AS Cliente,
                                       COUNT(*) AS NroCompras, SUM(f.Total) AS TotalComprado
                                FROM Facturas f LEFT JOIN Clientes c ON f.ClienteID=c.ClienteID
                                WHERE f.Fecha >= @d AND f.Fecha <= @h
                                GROUP BY f.ClienteID, c.RazonSocial
                                ORDER BY TotalComprado DESC";
                        break;
                    case "Valorización de Stock":
                        sql = @"SELECT ISNULL(p.Codigo,'') AS Codigo,
                                       ISNULL(p.Descripcion,'') AS Producto,
                                       ISNULL(p.StockActual,0) AS Stock,
                                       ISNULL(p.PrecioCosto,0) AS CostoUnitario,
                                       ISNULL(p.StockActual,0) * ISNULL(p.PrecioCosto,0) AS ValorCosto,
                                       CASE WHEN UPPER(ISNULL(p.TipoIVA,N'')) LIKE N'%EXE%' OR UPPER(ISNULL(p.TipoIVA,N'')) LIKE N'%NO GRAVA%' THEN 0.0
                                            WHEN ISNULL(p.TipoIVA,N'') LIKE N'%10%' THEN 10.5
                                            ELSE 21.0 END AS PctIVA,
                                       ROUND(ISNULL(p.StockActual,0) * ISNULL(p.PrecioCosto,0) *
                                         (1.0 + CASE WHEN UPPER(ISNULL(p.TipoIVA,N'')) LIKE N'%EXE%' OR UPPER(ISNULL(p.TipoIVA,N'')) LIKE N'%NO GRAVA%' THEN 0.0
                                                     WHEN ISNULL(p.TipoIVA,N'') LIKE N'%10%' THEN 10.5
                                                     ELSE 21.0 END / 100.0), 2) AS ValorConIVA
                                FROM Productos p
                                WHERE ISNULL(p.StockActual,0) > 0
                                  AND ISNULL(p.Codigo,'') <> 'VARIOS'
                                ORDER BY ValorCosto DESC";
                        break;
                    case "Ventas por Vendedor":
                        sql = @"SELECT ISNULL(NULLIF(LTRIM(RTRIM(f.NombrePersonal)),''), '(Sin vendedor)') AS Vendedor,
                                       COUNT(*) AS Comprobantes,
                                       ISNULL(SUM(f.Total),0) AS TotalVendido
                                FROM Facturas f
                                WHERE f.Fecha >= @d AND f.Fecha <= @h
                                GROUP BY ISNULL(NULLIF(LTRIM(RTRIM(f.NombrePersonal)),''), '(Sin vendedor)')
                                ORDER BY TotalVendido DESC";
                        break;
                    case "Faltantes en Pedidos":
                        sql = @"SELECT p.PedidoID, p.Fecha,
                                       ISNULL(c.RazonSocial,'') AS Cliente,
                                       ISNULL(pr.Codigo,'') AS Codigo,
                                       ISNULL(pr.Descripcion,'') AS Producto,
                                       pd.Cantidad AS CantPedida,
                                       ISNULL(pr.StockActual,0) AS StockActual,
                                       (pd.Cantidad - ISNULL(pr.StockActual,0)) AS Faltante
                                FROM Pedidos p
                                INNER JOIN PedidoDetalle pd ON p.PedidoID = pd.PedidoID
                                INNER JOIN Productos pr ON pd.ProductoID = pr.ProductoID
                                LEFT JOIN Clientes c ON p.ClienteID = c.ClienteID
                                WHERE p.Estado IN (N'Pendiente', N'Confirmado')
                                  AND p.Fecha >= @d AND p.Fecha <= @h
                                  AND pd.Cantidad > ISNULL(pr.StockActual, 0)
                                ORDER BY p.Fecha, p.PedidoID, pr.Descripcion";
                        break;
                    case "Cuenta Corriente Proveedores":
                        sql = @"SELECT m.Fecha,
                                       ISNULL(pv.RazonSocial,'') AS Proveedor,
                                       ISNULL(pv.CUIT,'') AS CUIT,
                                       m.Descripcion,
                                       m.Monto,
                                       m.SaldoHistorico AS Saldo
                                FROM MovimientosCuentaCorriente m
                                INNER JOIN Proveedores pv ON m.ProveedorID = pv.ProveedorID
                                WHERE m.ProveedorID IS NOT NULL
                                  AND m.Fecha >= @d AND m.Fecha <= @h
                                ORDER BY m.Fecha DESC, pv.RazonSocial";
                        break;
                    default:
                        return new DataTable();
                }
                var dt = new DataTable();
                var da = new System.Data.SqlClient.SqlDataAdapter(sql, conn);
                if (tipo != "Valorización de Stock")
                {
                    da.SelectCommand.Parameters.AddWithValue("@d", desde);
                    da.SelectCommand.Parameters.AddWithValue("@h", hasta);
                }
                da.Fill(dt);
                if (tipo == "Valorización de Stock" && dt.Rows.Count > 0)
                    AgregarFilaTotalesValorizacion(dt);
                return dt;
            }
        }

        private static void AgregarFilaTotalesValorizacion(DataTable dt)
        {
            decimal totCosto = 0, totIva = 0;
            foreach (DataRow r in dt.Rows)
            {
                totCosto += r["ValorCosto"] != DBNull.Value ? Convert.ToDecimal(r["ValorCosto"]) : 0;
                totIva += r["ValorConIVA"] != DBNull.Value ? Convert.ToDecimal(r["ValorConIVA"]) : 0;
            }
            var total = dt.NewRow();
            total["Codigo"] = "TOTAL";
            total["Producto"] = $"{dt.Rows.Count} productos con stock";
            total["Stock"] = DBNull.Value;
            total["CostoUnitario"] = DBNull.Value;
            total["ValorCosto"] = totCosto;
            total["PctIVA"] = DBNull.Value;
            total["ValorConIVA"] = totIva;
            dt.Rows.Add(total);
        }

        private void btnExportar_Click(object sender, RoutedEventArgs e)
        {
            if (_dtActual == null || _dtActual.Rows.Count == 0)
            { MessageBox.Show("Genere un informe primero.", "Información", MessageBoxButton.OK, MessageBoxImage.Information); return; }
            var dlg = new SaveFileDialog { Filter = "CSV (*.csv)|*.csv", FileName = $"informe_{DateTime.Today:yyyyMMdd}.csv" };
            if (dlg.ShowDialog() != true) return;
            try
            {
                var sb = new StringBuilder();
                var cols = new System.Collections.Generic.List<string>();
                foreach (DataColumn col in _dtActual.Columns) cols.Add(col.ColumnName);
                sb.AppendLine(string.Join(";", cols));
                foreach (DataRow r in _dtActual.Rows)
                {
                    var vals = new System.Collections.Generic.List<string>();
                    foreach (var col in cols) vals.Add(r[col]?.ToString()?.Replace(";", ",") ?? "");
                    sb.AppendLine(string.Join(";", vals));
                }
                System.IO.File.WriteAllText(dlg.FileName, sb.ToString(), Encoding.UTF8);
                MessageBox.Show("Exportado correctamente.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex) { MessageBox.Show("Error al exportar: " + ex.Message); }
        }
    }
}
