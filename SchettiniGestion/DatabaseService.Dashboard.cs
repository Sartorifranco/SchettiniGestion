using System;
using System.Data;
using SqlConnection = Microsoft.Data.SqlClient.SqlConnection;
using SqlCommand = Microsoft.Data.SqlClient.SqlCommand;

namespace SchettiniGestion
{
    public static partial class DatabaseService
    {
        public static int GetCantidadVentasHoy()
        {
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    var res = new SqlCommand("SELECT COUNT(*) FROM Facturas WHERE CAST(Fecha AS DATE) = CAST(GETDATE() AS DATE)", c).ExecuteScalar();
                    return Convert.ToInt32(res);
                }
            }
            catch { return 0; }
        }

        public static decimal GetTotalVentasHoy()
        {
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    var res = new SqlCommand("SELECT SUM(Total) FROM Facturas WHERE CAST(Fecha AS DATE) = CAST(GETDATE() AS DATE)", c).ExecuteScalar();
                    return res != DBNull.Value ? Convert.ToDecimal(res) : 0;
                }
            }
            catch { return 0; }
        }

        public static int GetCantidadProductos()
        {
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    return Convert.ToInt32(new SqlCommand("SELECT COUNT(*) FROM Productos", c).ExecuteScalar());
                }
            }
            catch { return 0; }
        }

        public static int GetCantidadClientes()
        {
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    return Convert.ToInt32(new SqlCommand("SELECT COUNT(*) FROM Clientes", c).ExecuteScalar());
                }
            }
            catch { return 0; }
        }

        public static decimal GetRentabilidadHoy()
        {
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    string sql = @"SELECT SUM((fd.PrecioUnitario - p.PrecioCosto) * fd.Cantidad) FROM FacturaDetalle fd JOIN Facturas f ON fd.FacturaID = f.FacturaID JOIN Productos p ON fd.ProductoID = p.ProductoID WHERE CAST(f.Fecha AS DATE) = CAST(GETDATE() AS DATE)";
                    var res = new SqlCommand(sql, c).ExecuteScalar();
                    return res != DBNull.Value ? Convert.ToDecimal(res) : 0;
                }
            }
            catch { return 0; }
        }
    }
}
