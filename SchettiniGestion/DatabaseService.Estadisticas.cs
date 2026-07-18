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
            public decimal MargenEstimado { get; set; }
            public decimal MargenPct => TotalVendido > 0 ? (MargenEstimado / TotalVendido) * 100m : 0m;
            public decimal TotalPeriodoAnterior { get; set; }
            public decimal VariacionPct
            {
                get
                {
                    if (TotalPeriodoAnterior <= 0)
                        return TotalVendido > 0 ? 100m : 0m;
                    return ((TotalVendido - TotalPeriodoAnterior) / TotalPeriodoAnterior) * 100m;
                }
            }
        }

        public static ResumenVentasPeriodo GetResumenVentasPeriodo(DateTime desde, DateTime hasta)
        {
            var r = new ResumenVentasPeriodo();
            try
            {
                TimeSpan duracion = hasta - desde;
                if (duracion < TimeSpan.Zero) duracion = TimeSpan.Zero;
                DateTime hastaAnt = desde.AddSeconds(-1);
                DateTime desdeAnt = hastaAnt - duracion;

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

                    using (var cmd = new SqlCommand(
                        @"SELECT ISNULL(SUM((fd.PrecioUnitario - ISNULL(p.PrecioCosto,0)) * fd.Cantidad), 0)
                          FROM FacturaDetalle fd
                          JOIN Facturas f ON fd.FacturaID = f.FacturaID
                          JOIN Productos p ON fd.ProductoID = p.ProductoID
                          WHERE f.Fecha BETWEEN @d AND @h", c))
                    {
                        cmd.Parameters.AddWithValue("@d", desde);
                        cmd.Parameters.AddWithValue("@h", hasta);
                        object res = cmd.ExecuteScalar();
                        r.MargenEstimado = res != null && res != DBNull.Value ? Convert.ToDecimal(res) : 0m;
                    }

                    using (var cmd = new SqlCommand(
                        @"SELECT ISNULL(SUM(Total), 0) FROM Facturas WHERE Fecha BETWEEN @d AND @h", c))
                    {
                        cmd.Parameters.AddWithValue("@d", desdeAnt);
                        cmd.Parameters.AddWithValue("@h", hastaAnt);
                        object res = cmd.ExecuteScalar();
                        r.TotalPeriodoAnterior = res != null && res != DBNull.Value ? Convert.ToDecimal(res) : 0m;
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

        /// <summary>Ventas agrupadas por hora (0-23) en el período.</summary>
        public static DataTable GetVentasPorHora(DateTime desde, DateTime hasta)
        {
            var dt = new DataTable();
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    string sql = @"
                        SELECT DATEPART(HOUR, Fecha) AS Hora, ISNULL(SUM(Total), 0) AS Total, COUNT(*) AS Cantidad
                        FROM Facturas
                        WHERE Fecha BETWEEN @d AND @h
                        GROUP BY DATEPART(HOUR, Fecha)
                        ORDER BY Hora";
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

        /// <summary>Productos con stock en o bajo el mínimo configurado (alerta operativa).</summary>
        public static DataTable GetProductosStockBajo(int topN = 15)
        {
            var dt = new DataTable();
            if (topN < 1) topN = 15;
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    string sql = @"
                        SELECT TOP (@top)
                            p.Codigo,
                            p.Descripcion,
                            ISNULL(p.StockActual, 0) AS StockActual,
                            ISNULL(p.StockMinimo, 0) AS StockMinimo
                        FROM Productos p
                        WHERE ISNULL(p.StockMinimo, 0) > 0
                          AND ISNULL(p.StockActual, 0) <= ISNULL(p.StockMinimo, 0)
                          AND ISNULL(p.Codigo, '') <> 'VARIOS'
                        ORDER BY (ISNULL(p.StockActual, 0) - ISNULL(p.StockMinimo, 0)) ASC, p.Descripcion";
                    using (var cmd = new SqlCommand(sql, c))
                    {
                        cmd.Parameters.AddWithValue("@top", topN);
                        new SqlDataAdapter(cmd).Fill(dt);
                    }
                }
            }
            catch { }
            return dt;
        }

        public static int GetCantidadProductosStockBajo()
        {
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    object res = new SqlCommand(
                        @"SELECT COUNT(*) FROM Productos
                          WHERE ISNULL(StockMinimo, 0) > 0
                            AND ISNULL(StockActual, 0) <= ISNULL(StockMinimo, 0)
                            AND ISNULL(Codigo, '') <> 'VARIOS'", c).ExecuteScalar();
                    return Convert.ToInt32(res);
                }
            }
            catch { return 0; }
        }

        /// <summary>
        /// Ranking de productos por facturación con margen y clasificación ABC (Pareto 80/15/5).
        /// Columnas: Codigo, Descripcion, Unidades, TotalVendido, Margen, MargenPct, PctAcumulado, ClaseAbc
        /// </summary>
        public static DataTable GetAnalisisAbcProductos(DateTime desde, DateTime hasta)
        {
            var dt = new DataTable();
            dt.Columns.Add("Codigo", typeof(string));
            dt.Columns.Add("Descripcion", typeof(string));
            dt.Columns.Add("Unidades", typeof(decimal));
            dt.Columns.Add("TotalVendido", typeof(decimal));
            dt.Columns.Add("Margen", typeof(decimal));
            dt.Columns.Add("MargenPct", typeof(decimal));
            dt.Columns.Add("PctAcumulado", typeof(decimal));
            dt.Columns.Add("ClaseAbc", typeof(string));
            dt.Columns.Add("TipoComercial", typeof(string));

            try
            {
                var raw = new DataTable();
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    string sql = @"
                        SELECT
                            p.Codigo,
                            p.Descripcion,
                            SUM(fd.Cantidad) AS Unidades,
                            SUM(fd.Cantidad * fd.PrecioUnitario
                                * (1 - ISNULL(fd.DescuentoPorcentaje,0)/100.0)
                                * (1 + ISNULL(fd.RecargoPorcentaje,0)/100.0)) AS TotalVendido,
                            SUM((fd.PrecioUnitario
                                * (1 - ISNULL(fd.DescuentoPorcentaje,0)/100.0)
                                * (1 + ISNULL(fd.RecargoPorcentaje,0)/100.0)
                                - ISNULL(p.PrecioCosto,0)) * fd.Cantidad) AS Margen
                        FROM FacturaDetalle fd
                        JOIN Facturas f ON fd.FacturaID = f.FacturaID
                        JOIN Productos p ON fd.ProductoID = p.ProductoID
                        WHERE f.Fecha BETWEEN @d AND @h
                          AND ISNULL(p.Codigo,'') <> 'VARIOS'
                        GROUP BY p.ProductoID, p.Codigo, p.Descripcion
                        HAVING SUM(fd.Cantidad * fd.PrecioUnitario) > 0
                        ORDER BY TotalVendido DESC";
                    using (var cmd = new SqlCommand(sql, c))
                    {
                        cmd.Parameters.AddWithValue("@d", desde);
                        cmd.Parameters.AddWithValue("@h", hasta);
                        new SqlDataAdapter(cmd).Fill(raw);
                    }
                }

                decimal total = 0m;
                foreach (DataRow r in raw.Rows)
                    total += Convert.ToDecimal(r["TotalVendido"]);

                    decimal acum = 0m;
                foreach (DataRow r in raw.Rows)
                {
                    decimal vendido = Convert.ToDecimal(r["TotalVendido"]);
                    decimal margen = Convert.ToDecimal(r["Margen"]);
                    decimal pctAntes = total > 0 ? (acum / total) * 100m : 0m;
                    acum += vendido;
                    decimal pctAcum = total > 0 ? (acum / total) * 100m : 0m;
                    string clase = pctAntes < 80m ? "A" : (pctAntes < 95m ? "B" : "C");
                    string tipo = clase == "A" ? "Estrella" : (clase == "B" ? "Importante" : "Resto");

                    dt.Rows.Add(
                        r["Codigo"]?.ToString() ?? "",
                        r["Descripcion"]?.ToString() ?? "",
                        Convert.ToDecimal(r["Unidades"]),
                        vendido,
                        margen,
                        vendido > 0 ? (margen / vendido) * 100m : 0m,
                        pctAcum,
                        clase,
                        tipo);
                }
            }
            catch { }
            return dt;
        }

        public static DataTable GetTopProductosPorMargen(DateTime desde, DateTime hasta, int topN = 10)
        {
            var dt = new DataTable();
            if (topN < 1) topN = 10;
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    string sql = @"
                        SELECT TOP (@top)
                            p.Descripcion,
                            SUM(fd.Cantidad) AS Unidades,
                            SUM(fd.Cantidad * fd.PrecioUnitario
                                * (1 - ISNULL(fd.DescuentoPorcentaje,0)/100.0)
                                * (1 + ISNULL(fd.RecargoPorcentaje,0)/100.0)) AS TotalVendido,
                            SUM((fd.PrecioUnitario
                                * (1 - ISNULL(fd.DescuentoPorcentaje,0)/100.0)
                                * (1 + ISNULL(fd.RecargoPorcentaje,0)/100.0)
                                - ISNULL(p.PrecioCosto,0)) * fd.Cantidad) AS Margen
                        FROM FacturaDetalle fd
                        JOIN Facturas f ON fd.FacturaID = f.FacturaID
                        JOIN Productos p ON fd.ProductoID = p.ProductoID
                        WHERE f.Fecha BETWEEN @d AND @h
                          AND ISNULL(p.Codigo,'') <> 'VARIOS'
                        GROUP BY p.ProductoID, p.Descripcion
                        ORDER BY Margen DESC";
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

        /// <summary>Productos con stock que no se vendieron en los últimos N días.</summary>
        public static DataTable GetProductosSinMovimiento(int diasSinVenta = 60, int topN = 50)
        {
            var dt = new DataTable();
            if (diasSinVenta < 1) diasSinVenta = 60;
            if (topN < 1) topN = 50;
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    string sql = @"
                        SELECT TOP (@top)
                            p.Codigo,
                            p.Descripcion,
                            ISNULL(p.StockActual, 0) AS StockActual,
                            ISNULL(p.PrecioCosto, 0) AS PrecioCosto,
                            ISNULL(p.StockActual, 0) * ISNULL(p.PrecioCosto, 0) AS CapitalInmovilizado,
                            (
                                SELECT MAX(f.Fecha)
                                FROM FacturaDetalle fd
                                JOIN Facturas f ON fd.FacturaID = f.FacturaID
                                WHERE fd.ProductoID = p.ProductoID
                            ) AS UltimaVenta
                        FROM Productos p
                        WHERE ISNULL(p.Codigo,'') <> 'VARIOS'
                          AND ISNULL(p.StockActual, 0) > 0
                          AND NOT EXISTS (
                              SELECT 1
                              FROM FacturaDetalle fd
                              JOIN Facturas f ON fd.FacturaID = f.FacturaID
                              WHERE fd.ProductoID = p.ProductoID
                                AND f.Fecha >= DATEADD(DAY, -@dias, GETDATE())
                          )
                        ORDER BY CapitalInmovilizado DESC, p.Descripcion";
                    using (var cmd = new SqlCommand(sql, c))
                    {
                        cmd.Parameters.AddWithValue("@top", topN);
                        cmd.Parameters.AddWithValue("@dias", diasSinVenta);
                        new SqlDataAdapter(cmd).Fill(dt);
                    }
                }
            }
            catch { }
            return dt;
        }
    }
}
