using System;
using System.Data;
using SqlConnection = Microsoft.Data.SqlClient.SqlConnection;
using SqlCommand = Microsoft.Data.SqlClient.SqlCommand;
using SqlDataAdapter = Microsoft.Data.SqlClient.SqlDataAdapter;

namespace SchettiniGestion
{
    public static partial class DatabaseService
    {
        public class ResumenVentasPeriodo
        {
            public decimal TotalVendido { get; set; }
            public int CantidadComprobantes { get; set; }
            public decimal TicketPromedio => CantidadComprobantes > 0 ? TotalVendido / CantidadComprobantes : 0m;
        }

        public static ResumenVentasPeriodo GetResumenVentasPeriodo(DateTime desde, DateTime hasta)
        {
            var r = new ResumenVentasPeriodo();
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    using (var cmd = new SqlCommand(
                        @"SELECT COUNT(*) AS Cantidad, ISNULL(SUM(Total), 0) AS Total
                          FROM Facturas
                          WHERE Fecha BETWEEN @d AND @h", c))
                    {
                        cmd.Parameters.AddWithValue("@d", desde);
                        cmd.Parameters.AddWithValue("@h", hasta);
                        using (var rd = cmd.ExecuteReader())
                        {
                            if (rd.Read())
                            {
                                r.CantidadComprobantes = Convert.ToInt32(rd["Cantidad"]);
                                r.TotalVendido = Convert.ToDecimal(rd["Total"]);
                            }
                        }
                    }
                }
            }
            catch { }
            return r;
        }

        public static DataTable GetVentasPorDia(DateTime desde, DateTime hasta)
        {
            var dt = new DataTable();
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    string sql = @"
                        SELECT CAST(Fecha AS DATE) AS Dia, ISNULL(SUM(Total), 0) AS Total
                        FROM Facturas
                        WHERE Fecha BETWEEN @d AND @h
                        GROUP BY CAST(Fecha AS DATE)
                        ORDER BY Dia";
                    using (var cmd = new SqlCommand(sql, c))
                    {
                        cmd.Parameters.AddWithValue("@d", desde);
                        cmd.Parameters.AddWithValue("@h", hasta);
                        new SqlDataAdapter(cmd).Fill(dt);
                    }
                }
            }
            catch { }
            return dt;
        }

        public static DataTable GetTopProductosVentas(DateTime desde, DateTime hasta, int topN = 10)
        {
            var dt = new DataTable();
            if (topN < 1) topN = 10;
            if (topN > 50) topN = 50;
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    string sql = @"
                        SELECT TOP (@top)
                            p.Descripcion,
                            SUM(fd.Cantidad) AS UnidadesVendidas,
                            SUM(fd.Cantidad * fd.PrecioUnitario) AS TotalVendido
                        FROM FacturaDetalle fd
                        JOIN Facturas f ON fd.FacturaID = f.FacturaID
                        JOIN Productos p ON fd.ProductoID = p.ProductoID
                        WHERE f.Fecha BETWEEN @d AND @h
                        GROUP BY p.ProductoID, p.Descripcion
                        ORDER BY TotalVendido DESC";
                    using (var cmd = new SqlCommand(sql, c))
                    {
                        cmd.Parameters.AddWithValue("@top", topN);
                        cmd.Parameters.AddWithValue("@d", desde);
                        cmd.Parameters.AddWithValue("@h", hasta);
                        new SqlDataAdapter(cmd).Fill(dt);
                    }
                }
            }
            catch { }
            return dt;
        }

        public static DataTable GetVentasPorMedioPago(DateTime desde, DateTime hasta)
        {
            var dt = new DataTable();
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    string sql = @"
                        SELECT
                            ISNULL(NULLIF(LTRIM(RTRIM(fc.NombreMedio)), ''), 'Sin medio') AS Medio,
                            ISNULL(SUM(fc.Monto), 0) AS Total
                        FROM FacturasCobranza fc
                        JOIN Facturas f ON fc.FacturaID = f.FacturaID
                        WHERE f.Fecha BETWEEN @d AND @h
                        GROUP BY ISNULL(NULLIF(LTRIM(RTRIM(fc.NombreMedio)), ''), 'Sin medio')
                        ORDER BY Total DESC";
                    using (var cmd = new SqlCommand(sql, c))
                    {
                        cmd.Parameters.AddWithValue("@d", desde);
                        cmd.Parameters.AddWithValue("@h", hasta);
                        new SqlDataAdapter(cmd).Fill(dt);
                    }

                    // Fallback: ventas sin detalle de cobranza
                    if (dt.Rows.Count == 0)
                    {
                        using (var cmd = new SqlCommand(
                            @"SELECT 'Ventas (sin detalle de cobro)' AS Medio, ISNULL(SUM(Total), 0) AS Total
                              FROM Facturas WHERE Fecha BETWEEN @d AND @h HAVING ISNULL(SUM(Total), 0) > 0", c))
                        {
                            cmd.Parameters.AddWithValue("@d", desde);
                            cmd.Parameters.AddWithValue("@h", hasta);
                            new SqlDataAdapter(cmd).Fill(dt);
                        }
                    }
                }
            }
            catch { }
            return dt;
        }
    }
}
