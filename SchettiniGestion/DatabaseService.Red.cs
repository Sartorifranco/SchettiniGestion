using System;
using System.Collections.Generic;
using System.Data;
using SqlConnection = Microsoft.Data.SqlClient.SqlConnection;
using SqlCommand = Microsoft.Data.SqlClient.SqlCommand;
using SqlDataAdapter = Microsoft.Data.SqlClient.SqlDataAdapter;

namespace SchettiniGestion
{
    public sealed class PuestoRedInfo
    {
        public string PuestoId { get; set; }
        public string Nombre { get; set; }
        public string MachineName { get; set; }
        public string Modo { get; set; }
        public DateTime UltimaVistaUtc { get; set; }
    }

    public static partial class DatabaseService
    {
        private static readonly object _lockRedSync = new object();
        private static bool _redSyncOk;

        /// <summary>
        /// Tabla de versión + triggers. Las cajas consultan Version cada ~250 ms
        /// y recargan listas cuando cambia (precios, clientes, stock, cuenta corriente).
        /// </summary>
        public static void AsegurarEsquemaRed()
        {
            if (_redSyncOk) return;
            lock (_lockRedSync)
            {
                if (_redSyncOk) return;
                try
                {
                    using (var c = new SqlConnection(_connectionString))
                    {
                        c.Open();
                        AsegurarEsquemaRed(c);
                    }
                    _redSyncOk = true;
                }
                catch (Exception ex)
                {
                    NotificarError("AsegurarEsquemaRed: " + ex.Message);
                }
            }
        }

        private static void AsegurarEsquemaRed(SqlConnection c)
        {
            lock (_lockRedSync)
            {
                if (_redSyncOk) return;
            using (var cmd = new SqlCommand(@"
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME='RedSync')
  CREATE TABLE dbo.RedSync (
    Id INT NOT NULL CONSTRAINT PK_RedSync PRIMARY KEY,
    Version BIGINT NOT NULL CONSTRAINT DF_RedSync_Version DEFAULT 0,
    UltimoUtc DATETIME2 NOT NULL CONSTRAINT DF_RedSync_Utc DEFAULT SYSUTCDATETIME(),
    Entidad NVARCHAR(80) NULL
  );
IF NOT EXISTS (SELECT 1 FROM dbo.RedSync WHERE Id=1)
  INSERT INTO dbo.RedSync (Id, Version) VALUES (1, 0);

IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME='PuestosRed')
  CREATE TABLE dbo.PuestosRed (
    PuestoId NVARCHAR(80) NOT NULL CONSTRAINT PK_PuestosRed PRIMARY KEY,
    Nombre NVARCHAR(80) NOT NULL,
    MachineName NVARCHAR(80) NULL,
    Modo NVARCHAR(20) NULL,
    UltimaVistaUtc DATETIME2 NOT NULL CONSTRAINT DF_PuestosRed_Utc DEFAULT SYSUTCDATETIME()
  );", c))
            {
                cmd.CommandTimeout = 30;
                cmd.ExecuteNonQuery();
            }

            AsegurarTriggerRedSync(c, "trg_RedSync_Productos", "Productos", "Productos");
            AsegurarTriggerRedSync(c, "trg_RedSync_Clientes", "Clientes", "Clientes");
            AsegurarTriggerRedSync(c, "trg_RedSync_MovCC", "MovimientosCuentaCorriente", "CuentaCorriente");
            AsegurarTriggerRedSync(c, "trg_RedSync_Listas", "ListasPrecios", "ListasPrecios");
            AsegurarTriggerRedSync(c, "trg_RedSync_ProdListas", "ProductoListas", "Productos");
            AsegurarTriggerRedSync(c, "trg_RedSync_ProdsListas", "ProductosListas", "Productos");
            AsegurarTriggerRedSync(c, "trg_RedSync_Promos", "Promociones", "Productos");
            _redSyncOk = true;
            }
        }

        private static void AsegurarTriggerRedSync(SqlConnection c, string trigger, string tabla, string entidad)
        {
            using (var existe = new SqlCommand(
                "SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA='dbo' AND TABLE_NAME=@t", c))
            {
                existe.Parameters.AddWithValue("@t", tabla);
                if (existe.ExecuteScalar() == null) return;
            }

            using (var yaHay = new SqlCommand(
                $"SELECT 1 FROM sys.triggers WHERE name = N'{trigger}' AND parent_id = OBJECT_ID(N'dbo.{tabla}')", c))
            {
                if (yaHay.ExecuteScalar() != null) return;
            }

            string sql = $@"
CREATE TRIGGER [dbo].[{trigger}] ON [dbo].[{tabla}]
AFTER INSERT, UPDATE, DELETE
AS
BEGIN
  SET NOCOUNT ON;
  UPDATE dbo.RedSync
     SET Version = Version + 1,
         UltimoUtc = SYSUTCDATETIME(),
         Entidad = N'{entidad}'
   WHERE Id = 1;
END";
            using (var crear = new SqlCommand(sql, c))
                crear.ExecuteNonQuery();
        }

        /// <summary>Consulta barata para el watcher de las cajas. Devuelve false si no hay BD / tabla.</summary>
        public static bool TryObtenerVersionRed(out long version, out string entidad)
        {
            version = 0;
            entidad = "";
            try
            {
                AsegurarEsquemaRed();
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    using (var cmd = new SqlCommand(
                        "SELECT TOP 1 Version, Entidad FROM dbo.RedSync WHERE Id=1", c))
                    {
                        cmd.CommandTimeout = 2;
                        using (var rd = cmd.ExecuteReader())
                        {
                            if (!rd.Read()) return false;
                            version = Convert.ToInt64(rd["Version"]);
                            entidad = rd["Entidad"] == DBNull.Value ? "" : rd["Entidad"].ToString();
                            return true;
                        }
                    }
                }
            }
            catch
            {
                return false;
            }
        }

        public static void RegistrarPuestoRed(string puestoId, string nombre, string machineName, string modo)
        {
            if (string.IsNullOrWhiteSpace(puestoId) || string.IsNullOrWhiteSpace(nombre)) return;
            try
            {
                AsegurarEsquemaRed();
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    using (var cmd = new SqlCommand(@"
MERGE dbo.PuestosRed AS t
USING (SELECT @id AS PuestoId) AS s
   ON t.PuestoId = s.PuestoId
WHEN MATCHED THEN
  UPDATE SET Nombre=@n, MachineName=@m, Modo=@mo, UltimaVistaUtc=SYSUTCDATETIME()
WHEN NOT MATCHED THEN
  INSERT (PuestoId, Nombre, MachineName, Modo, UltimaVistaUtc)
  VALUES (@id, @n, @m, @mo, SYSUTCDATETIME());", c))
                    {
                        cmd.CommandTimeout = 3;
                        cmd.Parameters.AddWithValue("@id", puestoId.Trim());
                        cmd.Parameters.AddWithValue("@n", nombre.Trim());
                        cmd.Parameters.AddWithValue("@m", machineName ?? "");
                        cmd.Parameters.AddWithValue("@mo", modo ?? "");
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                NotificarError("RegistrarPuestoRed: " + ex.Message);
            }
        }

        public static List<PuestoRedInfo> GetPuestosRed()
        {
            var lista = new List<PuestoRedInfo>();
            try
            {
                AsegurarEsquemaRed();
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    var dt = new DataTable();
                    new SqlDataAdapter(
                        "SELECT PuestoId, Nombre, MachineName, Modo, UltimaVistaUtc FROM dbo.PuestosRed ORDER BY Nombre",
                        c).Fill(dt);
                    foreach (DataRow r in dt.Rows)
                    {
                        lista.Add(new PuestoRedInfo
                        {
                            PuestoId = r["PuestoId"]?.ToString() ?? "",
                            Nombre = r["Nombre"]?.ToString() ?? "",
                            MachineName = r["MachineName"]?.ToString() ?? "",
                            Modo = r["Modo"]?.ToString() ?? "",
                            UltimaVistaUtc = r["UltimaVistaUtc"] == DBNull.Value
                                ? DateTime.MinValue
                                : Convert.ToDateTime(r["UltimaVistaUtc"])
                        });
                    }
                }
            }
            catch { }
            return lista;
        }
    }
}
