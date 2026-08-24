using System;
using System.Collections.Generic;
using System.Data;
using Newtonsoft.Json;
using SqlConnection = Microsoft.Data.SqlClient.SqlConnection;
using SqlCommand = Microsoft.Data.SqlClient.SqlCommand;
using SqlDataAdapter = Microsoft.Data.SqlClient.SqlDataAdapter;

namespace SchettiniGestion
{
    public static partial class DatabaseService
    {
        public const int SchemaVersionActual = 2;
        public const int MaxFavoritosPos = 24;
        public const int MaxVentasPausa = 8;
        public const int MaxIntentosAfipCola = 8;

        internal static string SqlEsquemaPosAvances => @"
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME='SchemaVersion')
  CREATE TABLE dbo.SchemaVersion (
    Version INT NOT NULL PRIMARY KEY,
    AplicadaUtc DATETIME NOT NULL DEFAULT GETUTCDATE(),
    Nota NVARCHAR(200) NULL
  );
IF NOT EXISTS (SELECT 1 FROM dbo.SchemaVersion)
  INSERT INTO dbo.SchemaVersion (Version, Nota) VALUES (1, N'Esquema previo (migraciones Lite)');
---SCHPOS_BATCH---
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Productos' AND COLUMN_NAME='EsFavoritoPos')
  ALTER TABLE dbo.Productos ADD EsFavoritoPos BIT NOT NULL DEFAULT 0;
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Productos' AND COLUMN_NAME='OrdenFavoritoPos')
  ALTER TABLE dbo.Productos ADD OrdenFavoritoPos INT NOT NULL DEFAULT 0;
---SCHPOS_BATCH---
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME='VentasPausa')
  CREATE TABLE dbo.VentasPausa (
    PausaID INT IDENTITY(1,1) PRIMARY KEY,
    Numero INT NOT NULL,
    Hora DATETIME NOT NULL DEFAULT GETDATE(),
    ClienteID INT NOT NULL DEFAULT 0,
    ClienteNombre NVARCHAR(200) NULL,
    TipoComprobante NVARCHAR(50) NULL,
    CondicionVenta NVARCHAR(80) NULL,
    ListaID INT NULL,
    Total DECIMAL(18,2) NOT NULL DEFAULT 0,
    ItemsJson NVARCHAR(MAX) NOT NULL,
    Maquina NVARCHAR(120) NULL,
    Usuario NVARCHAR(80) NULL
  );
---SCHPOS_BATCH---
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME='AfipCola')
  CREATE TABLE dbo.AfipCola (
    ColaID INT IDENTITY(1,1) PRIMARY KEY,
    FacturaID INT NOT NULL,
    TipoAfip INT NOT NULL,
    PuntoVenta INT NOT NULL,
    Total DECIMAL(18,2) NOT NULL,
    CuitCliente BIGINT NOT NULL DEFAULT 0,
    CondicionIva NVARCHAR(80) NULL,
    ItemsJson NVARCHAR(MAX) NOT NULL,
    Estado NVARCHAR(20) NOT NULL DEFAULT N'Pendiente',
    Intentos INT NOT NULL DEFAULT 0,
    UltimoError NVARCHAR(MAX) NULL,
    FechaAlta DATETIME NOT NULL DEFAULT GETDATE(),
    FechaUltimoIntento DATETIME NULL,
    CAE NVARCHAR(50) NULL,
    VencimientoCAE NVARCHAR(20) NULL,
    NumeroComprobante INT NULL
  );
---SCHPOS_BATCH---
IF NOT EXISTS (SELECT 1 FROM dbo.SchemaVersion WHERE Version = 2)
  INSERT INTO dbo.SchemaVersion (Version, Nota) VALUES (2, N'Favoritos POS, pausas persistentes, cola ARCA');
";

        public sealed class VentaPausaDto
        {
            public int PausaID { get; set; }
            public int Numero { get; set; }
            public DateTime Hora { get; set; }
            public int ClienteId { get; set; }
            public string ClienteNombre { get; set; }
            public string TipoComprobante { get; set; }
            public string CondicionVenta { get; set; }
            public int? ListaId { get; set; }
            public decimal Total { get; set; }
            public List<FacturaItem> Items { get; set; }
            public string Maquina { get; set; }
            public string Usuario { get; set; }
        }

        public sealed class AfipColaItem
        {
            public int ColaID { get; set; }
            public int FacturaID { get; set; }
            public int TipoAfip { get; set; }
            public int PuntoVenta { get; set; }
            public decimal Total { get; set; }
            public long CuitCliente { get; set; }
            public string CondicionIva { get; set; }
            public List<FacturaItem> Items { get; set; }
            public int Intentos { get; set; }
        }

        public sealed class AfipColaResumen
        {
            public int ColaID { get; set; }
            public int FacturaID { get; set; }
            public decimal Total { get; set; }
            public string Estado { get; set; }
            public string EstadoTexto { get; set; }
            public int Intentos { get; set; }
            public DateTime FechaAlta { get; set; }
            public DateTime? FechaUltimoIntento { get; set; }
            public string UltimoError { get; set; }
            public string CAE { get; set; }
            public int? NumeroComprobante { get; set; }
        }

        public sealed class ClienteImportacionItem
        {
            public int Fila { get; set; }
            public string RazonSocial { get; set; }
            public string Cuit { get; set; }
            public string CondicionIva { get; set; }
            public string Telefono { get; set; }
            public string Email { get; set; }
            public string Direccion { get; set; }
            public bool PermiteCuentaCorriente { get; set; }
        }

        public sealed class ClienteImportacionResultado
        {
            public int Altas { get; set; }
            public int Actualizados { get; set; }
            public int Errores { get; set; }
            public List<string> Mensajes { get; set; } = new List<string>();
        }

        public static bool EsFavoritoPos(DataRow r)
        {
            if (r == null || r.Table == null || !r.Table.Columns.Contains("EsFavoritoPos")) return false;
            if (r["EsFavoritoPos"] == DBNull.Value) return false;
            try { return Convert.ToBoolean(r["EsFavoritoPos"]); } catch { return false; }
        }

        public static int ContarFavoritosPos()
        {
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    AsegurarMigracionLite(c);
                    using (var cmd = new SqlCommand(
                        "SELECT COUNT(*) FROM dbo.Productos WHERE ISNULL(EsFavoritoPos,0)=1 AND ISNULL(Activo,1)=1", c))
                        return Convert.ToInt32(cmd.ExecuteScalar());
                }
            }
            catch { return 0; }
        }

        public static bool SetFavoritoPos(int productoId, bool favorito)
        {
            if (productoId <= 0) return false;
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    AsegurarMigracionLite(c);
                    if (favorito)
                    {
                        int n = 0;
                        using (var q = new SqlCommand(
                            "SELECT COUNT(*) FROM dbo.Productos WHERE ISNULL(EsFavoritoPos,0)=1 AND ISNULL(Activo,1)=1 AND ProductoID<>@id", c))
                        {
                            q.Parameters.AddWithValue("@id", productoId);
                            n = Convert.ToInt32(q.ExecuteScalar());
                        }
                        if (n >= MaxFavoritosPos) return false;
                    }
                    using (var cmd = new SqlCommand(
                        @"UPDATE dbo.Productos
                          SET EsFavoritoPos=@f,
                              OrdenFavoritoPos = CASE WHEN @f=1 AND ISNULL(OrdenFavoritoPos,0)=0 THEN ProductoID ELSE OrdenFavoritoPos END
                          WHERE ProductoID=@id", c))
                    {
                        cmd.Parameters.AddWithValue("@f", favorito);
                        cmd.Parameters.AddWithValue("@id", productoId);
                        cmd.ExecuteNonQuery();
                    }
                    return true;
                }
            }
            catch (Exception ex)
            {
                NotificarError("Favorito POS: " + ex.Message);
                return false;
            }
        }

        public static int GuardarVentaPausa(VentaPausaDto pausa)
        {
            if (pausa == null || pausa.Items == null || pausa.Items.Count == 0) return 0;
            try
            {
                string json = JsonConvert.SerializeObject(pausa.Items);
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    AsegurarMigracionLite(c);
                    using (var cmd = new SqlCommand(
                        @"INSERT INTO dbo.VentasPausa
                          (Numero,Hora,ClienteID,ClienteNombre,TipoComprobante,CondicionVenta,ListaID,Total,ItemsJson,Maquina,Usuario)
                          VALUES (@n,@h,@cid,@nom,@tc,@cv,@lid,@t,@j,@m,@u);
                          SELECT CAST(SCOPE_IDENTITY() AS INT);", c))
                    {
                        cmd.Parameters.AddWithValue("@n", pausa.Numero);
                        cmd.Parameters.AddWithValue("@h", pausa.Hora == default(DateTime) ? DateTime.Now : pausa.Hora);
                        cmd.Parameters.AddWithValue("@cid", pausa.ClienteId);
                        cmd.Parameters.AddWithValue("@nom", (object)pausa.ClienteNombre ?? "");
                        cmd.Parameters.AddWithValue("@tc", (object)pausa.TipoComprobante ?? "");
                        cmd.Parameters.AddWithValue("@cv", (object)pausa.CondicionVenta ?? "");
                        cmd.Parameters.AddWithValue("@lid", (object)pausa.ListaId ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@t", pausa.Total);
                        cmd.Parameters.AddWithValue("@j", json);
                        cmd.Parameters.AddWithValue("@m", (object)(pausa.Maquina ?? Environment.MachineName) ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@u", (object)(pausa.Usuario ?? SesionUsuario.NombreParaRegistro()) ?? DBNull.Value);
                        return Convert.ToInt32(cmd.ExecuteScalar());
                    }
                }
            }
            catch (Exception ex)
            {
                NotificarError("Pausa de venta: " + ex.Message);
                return 0;
            }
        }

        public static int ProximoNumeroPausa(string maquina)
        {
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    AsegurarMigracionLite(c);
                    using (var cmd = new SqlCommand(
                        "SELECT ISNULL(MAX(Numero),0)+1 FROM dbo.VentasPausa WHERE ISNULL(Maquina,'')=@m", c))
                    {
                        cmd.Parameters.AddWithValue("@m", maquina ?? Environment.MachineName);
                        return Convert.ToInt32(cmd.ExecuteScalar());
                    }
                }
            }
            catch { return 1; }
        }

        public static int ContarVentasPausa(string maquina)
        {
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    AsegurarMigracionLite(c);
                    using (var cmd = new SqlCommand(
                        "SELECT COUNT(*) FROM dbo.VentasPausa WHERE ISNULL(Maquina,'')=@m", c))
                    {
                        cmd.Parameters.AddWithValue("@m", maquina ?? Environment.MachineName);
                        return Convert.ToInt32(cmd.ExecuteScalar());
                    }
                }
            }
            catch { return 0; }
        }

        public static List<VentaPausaDto> ListarVentasPausa(string maquina)
        {
            var list = new List<VentaPausaDto>();
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    AsegurarMigracionLite(c);
                    var dt = new DataTable();
                    using (var da = new SqlDataAdapter(
                        "SELECT * FROM dbo.VentasPausa WHERE ISNULL(Maquina,'')=@m ORDER BY PausaID", c))
                    {
                        da.SelectCommand.Parameters.AddWithValue("@m", maquina ?? Environment.MachineName);
                        da.Fill(dt);
                    }
                    foreach (DataRow r in dt.Rows)
                    {
                        var dto = new VentaPausaDto
                        {
                            PausaID = Convert.ToInt32(r["PausaID"]),
                            Numero = Convert.ToInt32(r["Numero"]),
                            Hora = Convert.ToDateTime(r["Hora"]),
                            ClienteId = r["ClienteID"] == DBNull.Value ? 0 : Convert.ToInt32(r["ClienteID"]),
                            ClienteNombre = r["ClienteNombre"]?.ToString() ?? "",
                            TipoComprobante = r["TipoComprobante"]?.ToString() ?? "",
                            CondicionVenta = r["CondicionVenta"]?.ToString() ?? "",
                            ListaId = r["ListaID"] == DBNull.Value ? (int?)null : Convert.ToInt32(r["ListaID"]),
                            Total = r["Total"] == DBNull.Value ? 0m : Convert.ToDecimal(r["Total"]),
                            Maquina = r["Maquina"]?.ToString(),
                            Usuario = r["Usuario"]?.ToString(),
                            Items = DeserializarItems(r["ItemsJson"]?.ToString())
                        };
                        list.Add(dto);
                    }
                }
            }
            catch (Exception ex) { NotificarError("Leer pausas: " + ex.Message); }
            return list;
        }

        public static bool EliminarVentaPausa(int pausaId)
        {
            if (pausaId <= 0) return false;
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    using (var cmd = new SqlCommand("DELETE FROM dbo.VentasPausa WHERE PausaID=@id", c))
                    {
                        cmd.Parameters.AddWithValue("@id", pausaId);
                        cmd.ExecuteNonQuery();
                    }
                    return true;
                }
            }
            catch { return false; }
        }

        public static bool EncolarAfipCae(int facturaId, int tipoAfip, int puntoVenta, decimal total,
            long cuitCliente, string condicionIva, List<FacturaItem> items, string errorInicial)
        {
            if (facturaId <= 0 || tipoAfip <= 0 || items == null || items.Count == 0) return false;
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    AsegurarMigracionLite(c);
                    using (var cmd = new SqlCommand(
                        @"INSERT INTO dbo.AfipCola
                          (FacturaID,TipoAfip,PuntoVenta,Total,CuitCliente,CondicionIva,ItemsJson,Estado,Intentos,UltimoError)
                          VALUES (@fid,@tipo,@pv,@t,@cuit,@iva,@j,N'Pendiente',0,@err)", c))
                    {
                        cmd.Parameters.AddWithValue("@fid", facturaId);
                        cmd.Parameters.AddWithValue("@tipo", tipoAfip);
                        cmd.Parameters.AddWithValue("@pv", puntoVenta);
                        cmd.Parameters.AddWithValue("@t", total);
                        cmd.Parameters.AddWithValue("@cuit", cuitCliente);
                        cmd.Parameters.AddWithValue("@iva", (object)condicionIva ?? "");
                        cmd.Parameters.AddWithValue("@j", JsonConvert.SerializeObject(items));
                        cmd.Parameters.AddWithValue("@err", (object)errorInicial ?? DBNull.Value);
                        cmd.ExecuteNonQuery();
                    }
                    return true;
                }
            }
            catch (Exception ex)
            {
                NotificarError("Cola ARCA: " + ex.Message);
                return false;
            }
        }

        public static int ContarAfipColaPendientes()
        {
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    AsegurarMigracionLite(c);
                    using (var cmd = new SqlCommand(
                        "SELECT COUNT(*) FROM dbo.AfipCola WHERE Estado=N'Pendiente'", c))
                        return Convert.ToInt32(cmd.ExecuteScalar());
                }
            }
            catch { return 0; }
        }

        public static List<AfipColaResumen> ListarAfipColaResumen(int tope = 50)
        {
            var list = new List<AfipColaResumen>();
            if (tope <= 0) tope = 50;
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    AsegurarMigracionLite(c);
                    var dt = new DataTable();
                    using (var da = new SqlDataAdapter(
                        "SELECT TOP " + tope + @" ColaID, FacturaID, Total, Estado, Intentos, FechaAlta,
                                 FechaUltimoIntento, UltimoError, CAE, NumeroComprobante
                          FROM dbo.AfipCola
                          ORDER BY CASE Estado WHEN N'Pendiente' THEN 0 WHEN N'Error' THEN 1 ELSE 2 END,
                                   ColaID DESC", c))
                    {
                        da.Fill(dt);
                    }
                    foreach (DataRow r in dt.Rows)
                    {
                        string estado = r["Estado"]?.ToString() ?? "";
                        string texto = estado == "Ok" ? "Autorizado"
                            : estado == "Error" ? "Sin CAE (agotó reintentos)"
                            : "Pendiente de CAE";
                        list.Add(new AfipColaResumen
                        {
                            ColaID = Convert.ToInt32(r["ColaID"]),
                            FacturaID = Convert.ToInt32(r["FacturaID"]),
                            Total = r["Total"] == DBNull.Value ? 0m : Convert.ToDecimal(r["Total"]),
                            Estado = estado,
                            EstadoTexto = texto,
                            Intentos = r["Intentos"] == DBNull.Value ? 0 : Convert.ToInt32(r["Intentos"]),
                            FechaAlta = r["FechaAlta"] == DBNull.Value ? DateTime.MinValue : Convert.ToDateTime(r["FechaAlta"]),
                            FechaUltimoIntento = r["FechaUltimoIntento"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(r["FechaUltimoIntento"]),
                            UltimoError = r["UltimoError"]?.ToString() ?? "",
                            CAE = r["CAE"]?.ToString() ?? "",
                            NumeroComprobante = r["NumeroComprobante"] == DBNull.Value ? (int?)null : Convert.ToInt32(r["NumeroComprobante"])
                        });
                    }
                }
            }
            catch { }
            return list;
        }

        public static AfipColaItem TomarSiguienteAfipCola()
        {
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    AsegurarMigracionLite(c);
                    var dt = new DataTable();
                    using (var da = new SqlDataAdapter(
                        @"SELECT TOP 1 * FROM dbo.AfipCola
                          WHERE Estado=N'Pendiente' AND Intentos < @max
                          ORDER BY ColaID", c))
                    {
                        da.SelectCommand.Parameters.AddWithValue("@max", MaxIntentosAfipCola);
                        da.Fill(dt);
                    }
                    if (dt.Rows.Count == 0) return null;
                    var r = dt.Rows[0];
                    return new AfipColaItem
                    {
                        ColaID = Convert.ToInt32(r["ColaID"]),
                        FacturaID = Convert.ToInt32(r["FacturaID"]),
                        TipoAfip = Convert.ToInt32(r["TipoAfip"]),
                        PuntoVenta = Convert.ToInt32(r["PuntoVenta"]),
                        Total = Convert.ToDecimal(r["Total"]),
                        CuitCliente = r["CuitCliente"] == DBNull.Value ? 0L : Convert.ToInt64(r["CuitCliente"]),
                        CondicionIva = r["CondicionIva"]?.ToString(),
                        Intentos = r["Intentos"] == DBNull.Value ? 0 : Convert.ToInt32(r["Intentos"]),
                        Items = DeserializarItems(r["ItemsJson"]?.ToString())
                    };
                }
            }
            catch { return null; }
        }

        public static void MarcarAfipColaExito(int colaId, int facturaId, string cae, string vto, int nro)
        {
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    using (var tr = c.BeginTransaction())
                    {
                        using (var updF = new SqlCommand(
                            @"UPDATE dbo.Facturas
                              SET TipoComprobante=N'Factura',
                                  CAE=@cae,
                                  VencimientoCAE=@vto,
                                  NumeroComprobanteAFIP=@nro,
                                  CondicionTicket = CASE
                                    WHEN CondicionTicket IS NULL OR CondicionTicket = N'' THEN N'CAE recuperado por reintento ARCA'
                                    ELSE CondicionTicket + N' | CAE recuperado'
                                  END
                              WHERE FacturaID=@fid", c, tr))
                        {
                            updF.Parameters.AddWithValue("@cae", (object)cae ?? DBNull.Value);
                            updF.Parameters.AddWithValue("@vto", (object)vto ?? DBNull.Value);
                            updF.Parameters.AddWithValue("@nro", nro);
                            updF.Parameters.AddWithValue("@fid", facturaId);
                            updF.ExecuteNonQuery();
                        }
                        using (var updC = new SqlCommand(
                            @"UPDATE dbo.AfipCola
                              SET Estado=N'Ok', CAE=@cae, VencimientoCAE=@vto, NumeroComprobante=@nro,
                                  FechaUltimoIntento=GETDATE(), Intentos=Intentos+1, UltimoError=NULL
                              WHERE ColaID=@id", c, tr))
                        {
                            updC.Parameters.AddWithValue("@cae", (object)cae ?? DBNull.Value);
                            updC.Parameters.AddWithValue("@vto", (object)vto ?? DBNull.Value);
                            updC.Parameters.AddWithValue("@nro", nro);
                            updC.Parameters.AddWithValue("@id", colaId);
                            updC.ExecuteNonQuery();
                        }
                        tr.Commit();
                    }
                }
            }
            catch (Exception ex) { NotificarError("Actualizar CAE: " + ex.Message); }
        }

        public static void MarcarAfipColaFallo(int colaId, string error)
        {
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    using (var cmd = new SqlCommand(
                        @"UPDATE dbo.AfipCola
                          SET Intentos=Intentos+1, FechaUltimoIntento=GETDATE(), UltimoError=@e,
                              Estado = CASE WHEN Intentos+1 >= @max THEN N'Error' ELSE N'Pendiente' END
                          WHERE ColaID=@id", c))
                    {
                        cmd.Parameters.AddWithValue("@e", (error ?? "").Length > 2000 ? error.Substring(0, 2000) : (object)error ?? "");
                        cmd.Parameters.AddWithValue("@max", MaxIntentosAfipCola);
                        cmd.Parameters.AddWithValue("@id", colaId);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch { }
        }

        public static ClienteImportacionResultado ImportarClientesMasivo(IList<ClienteImportacionItem> filas)
        {
            var res = new ClienteImportacionResultado();
            if (filas == null || filas.Count == 0) return res;
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    AsegurarMigracionLite(c);
                    foreach (var item in filas)
                    {
                        if (item == null) continue;
                        string razon = (item.RazonSocial ?? "").Trim();
                        if (string.IsNullOrEmpty(razon))
                        {
                            res.Errores++;
                            if (res.Mensajes.Count < 8)
                                res.Mensajes.Add("Fila " + item.Fila + ": falta razón social.");
                            continue;
                        }
                        string cuit = NormalizarCuitImport(item.Cuit);
                        string iva = string.IsNullOrWhiteSpace(item.CondicionIva) ? "Consumidor Final" : item.CondicionIva.Trim();
                        try
                        {
                            int existenteId = 0;
                            if (!string.IsNullOrEmpty(cuit))
                            {
                                using (var q = new SqlCommand(
                                    "SELECT TOP 1 ClienteID FROM dbo.Clientes WHERE REPLACE(REPLACE(ISNULL(CUIT,''),'-',''),' ','')=@c", c))
                                {
                                    q.Parameters.AddWithValue("@c", cuit.Replace("-", ""));
                                    var o = q.ExecuteScalar();
                                    if (o != null && o != DBNull.Value) existenteId = Convert.ToInt32(o);
                                }
                            }
                            if (existenteId > 0)
                            {
                                using (var u = new SqlCommand(
                                    @"UPDATE dbo.Clientes SET RazonSocial=@r, CondicionIVA=@i, Direccion=@d, Telefono=@t, Email=@e, PermiteCuentaCorriente=@p
                                      WHERE ClienteID=@id", c))
                                {
                                    u.Parameters.AddWithValue("@r", razon);
                                    u.Parameters.AddWithValue("@i", iva);
                                    u.Parameters.AddWithValue("@d", item.Direccion ?? "");
                                    u.Parameters.AddWithValue("@t", item.Telefono ?? "");
                                    u.Parameters.AddWithValue("@e", item.Email ?? "");
                                    u.Parameters.AddWithValue("@p", item.PermiteCuentaCorriente);
                                    u.Parameters.AddWithValue("@id", existenteId);
                                    u.ExecuteNonQuery();
                                }
                                res.Actualizados++;
                            }
                            else
                            {
                                using (var ins = new SqlCommand(
                                    @"INSERT INTO dbo.Clientes (CUIT,RazonSocial,CondicionIVA,Direccion,Telefono,Email,PermiteCuentaCorriente)
                                      VALUES (@c,@r,@i,@d,@t,@e,@p)", c))
                                {
                                    ins.Parameters.AddWithValue("@c", string.IsNullOrEmpty(cuit) ? "" : cuit);
                                    ins.Parameters.AddWithValue("@r", razon);
                                    ins.Parameters.AddWithValue("@i", iva);
                                    ins.Parameters.AddWithValue("@d", item.Direccion ?? "");
                                    ins.Parameters.AddWithValue("@t", item.Telefono ?? "");
                                    ins.Parameters.AddWithValue("@e", item.Email ?? "");
                                    ins.Parameters.AddWithValue("@p", item.PermiteCuentaCorriente);
                                    ins.ExecuteNonQuery();
                                }
                                res.Altas++;
                            }
                        }
                        catch (Exception exFila)
                        {
                            res.Errores++;
                            if (res.Mensajes.Count < 8)
                                res.Mensajes.Add("Fila " + item.Fila + ": " + exFila.Message);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                res.Errores++;
                res.Mensajes.Add(ex.Message);
            }
            return res;
        }

        private static string NormalizarCuitImport(string cuit)
        {
            if (string.IsNullOrWhiteSpace(cuit)) return "";
            string dig = new string(cuit.Trim().ToCharArray());
            var sb = new System.Text.StringBuilder();
            foreach (char ch in dig)
                if (char.IsDigit(ch)) sb.Append(ch);
            string n = sb.ToString();
            if (n.Length == 11)
                return n.Substring(0, 2) + "-" + n.Substring(2, 8) + "-" + n.Substring(10, 1);
            return cuit.Trim();
        }

        private static List<FacturaItem> DeserializarItems(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return new List<FacturaItem>();
            try
            {
                var list = JsonConvert.DeserializeObject<List<FacturaItem>>(json);
                return list ?? new List<FacturaItem>();
            }
            catch { return new List<FacturaItem>(); }
        }
    }
}
