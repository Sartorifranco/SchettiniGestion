using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.Data.SqlClient;
using SchettiniGestion;

namespace SchettiniGestion.Tester
{
    partial class Program
    {
        /// <summary>Pruebas automáticas de regresión para Sprints 1–3 (API/BD).</summary>
        static void EjecutarPruebasSprintsComprasInformes()
        {
            Registrar("");
            Registrar("--- SPRINTS 1–3: COMPRAS E INFORMES (AUTO) ---");

            string suf = Guid.NewGuid().ToString("N").Substring(0, 8);
            string tag = "[SPRINT QA " + suf + "]";
            string codProd = "BOT-SPR-" + suf;
            string cuitPr = "30" + Math.Abs(tag.GetHashCode() % 1000000000).ToString("D9");
            if (cuitPr.Length > 11) cuitPr = cuitPr.Substring(0, 11);

            int proveedorId = 0;
            int prodId = 0;
            int ordenId = 0;
            int compraSinStockId = 0;
            int compraConOcId = 0;

            try
            {
                if (!DatabaseService.GuardarProveedor(0, cuitPr, tag + " Prov", "Tel", "qa@test.com", "Dir"))
                {
                    Registrar("❌ SPRINT: No se pudo crear proveedor de prueba.");
                    return;
                }
                proveedorId = ObtenerUltimoIdPorCampo("Proveedores", "ProveedorID", "RazonSocial", tag + " Prov");
                if (proveedorId <= 0) { Registrar("❌ SPRINT: ProveedorID no resuelto."); return; }

                if (!DatabaseService.GuardarProducto(0, codProd, "779SPR" + suf, tag + " Prod", "Varios", "21.0", 50m, 0m, 0m, 100m, 10, ""))
                {
                    Registrar("❌ SPRINT: No se pudo crear producto de prueba.");
                    return;
                }
                prodId = Convert.ToInt32(DatabaseService.BuscarProducto(codProd)["ProductoID"]);
                int stockInicial = Convert.ToInt32(DatabaseService.BuscarProducto(codProd)["StockActual"]);

                // Sprint 2 — factura sin recepcionar
                var items = new List<(int ProductoID, int Cantidad, decimal Costo)> { (prodId, 3, 50m) };
                if (!DatabaseService.GuardarCompra(proveedorId, "Factura A", 150m, items, "Contado", null, false))
                {
                    Registrar("❌ SPRINT-2: GuardarCompra sin recepcionar falló.");
                    return;
                }
                compraSinStockId = ObtenerUltimaCompraIdProveedor(proveedorId);
                int stockTrasSinRec = Convert.ToInt32(DatabaseService.BuscarProducto(codProd)["StockActual"]);
                bool stockRecibidoFlag = false;
                foreach (DataRow r in DatabaseService.GetCompras().Rows)
                {
                    if (Convert.ToInt32(r["CompraID"]) == compraSinStockId)
                    {
                        stockRecibidoFlag = r.Table.Columns.Contains("StockRecibido") && r["StockRecibido"] != DBNull.Value
                            && Convert.ToBoolean(r["StockRecibido"]);
                        break;
                    }
                }
                if (stockTrasSinRec == stockInicial && !stockRecibidoFlag)
                    Registrar("✅ SPRINT-2: Factura sin recepcionar — stock intacto, StockRecibido=false.");
                else
                    Registrar("❌ SPRINT-2: Factura sin recepcionar — stock=" + stockTrasSinRec + " esperado " + stockInicial + ", flag=" + stockRecibidoFlag);

                // Sprint 2 — OC + factura con recepción
                ordenId = DatabaseService.GuardarOrdenCompra(0, proveedorId, DateTime.Today.AddDays(3), tag,
                    new List<(int, int, decimal)> { (prodId, 5, 50m) });
                if (ordenId <= 0)
                {
                    Registrar("❌ SPRINT-2: No se pudo crear OC de prueba.");
                    return;
                }
                var dtOcAb = DatabaseService.GetOrdenesCompraAbiertas(proveedorId);
                bool ocVisible = false;
                foreach (DataRow r in dtOcAb.Rows)
                    if (Convert.ToInt32(r["OrdenCompraID"]) == ordenId) { ocVisible = true; break; }
                if (ocVisible)
                    Registrar("✅ SPRINT-1/2: OC abierta visible en GetOrdenesCompraAbiertas.");
                else
                    Registrar("❌ SPRINT-2: OC no aparece en selector de abiertas.");

                var itemsOc = new List<(int, int, decimal)> { (prodId, 5, 50m) };
                if (!DatabaseService.GuardarCompra(proveedorId, "Factura A", 250m, itemsOc, "Contado", ordenId, true))
                {
                    Registrar("❌ SPRINT-2: GuardarCompra con OC y recepción falló.");
                    return;
                }
                compraConOcId = ObtenerUltimaCompraIdProveedor(proveedorId);
                int stockTrasOc = Convert.ToInt32(DatabaseService.BuscarProducto(codProd)["StockActual"]);
                string estadoOc = ObtenerEstadoOrdenCompra(ordenId);
                int recepcionesVinc = ContarRecepcionesPorCompra(compraConOcId);

                if (stockTrasOc == stockTrasSinRec + 5 && estadoOc == "Recibida" && recepcionesVinc >= 1)
                    Registrar("✅ SPRINT-2: Factura+OC recepcionada — stock +5, OC Recibida, recepción creada.");
                else
                    Registrar("❌ SPRINT-2: Factura+OC — stock=" + stockTrasOc + ", estado OC=" + estadoOc + ", recep=" + recepcionesVinc);

                // Sprint 2 — editar OC preserva CantidadRecibida
                DatabaseService.GuardarOrdenCompra(ordenId, proveedorId, DateTime.Today.AddDays(5), tag + " edit",
                    new List<(int, int, decimal)> { (prodId, 6, 50m) }, "Parcial");
                int cantRecEdit = ObtenerCantidadRecibidaOc(ordenId, prodId);
                if (cantRecEdit == 5)
                    Registrar("✅ SPRINT-2: Editar OC preserva CantidadRecibida (" + cantRecEdit + ").");
                else
                    Registrar("❌ SPRINT-2: Editar OC perdió CantidadRecibida — tiene " + cantRecEdit + ", esperado 5.");

                // Sprint 2 — eliminar factura revierte OC
                if (DatabaseService.EliminarCompra(compraConOcId))
                {
                    int stockTrasDel = Convert.ToInt32(DatabaseService.BuscarProducto(codProd)["StockActual"]);
                    string estadoTrasDel = ObtenerEstadoOrdenCompra(ordenId);
                    int recepTrasDel = ContarRecepcionesPorCompra(compraConOcId);
                    if (stockTrasDel == stockTrasSinRec && estadoTrasDel == "Pendiente" && recepTrasDel == 0)
                        Registrar("✅ SPRINT-2: Eliminar factura con stock revierte OC y recepciones.");
                    else
                        Registrar("❌ SPRINT-2: Eliminar factura — stock=" + stockTrasDel + ", OC=" + estadoTrasDel + ", recep=" + recepTrasDel);
                }
                else
                    Registrar("❌ SPRINT-2: EliminarCompra falló.");

                compraConOcId = 0;

                // Sprint 3 — valorización (producto con stock > 0 debe aparecer)
                bool enValorizacion = false;
                using (var conn = new SqlConnection(DatabaseService.ConnectionString))
                {
                    conn.Open();
                    using (var cmd = new SqlCommand(@"SELECT COUNT(*) FROM Productos
                        WHERE ProductoID=@p AND ISNULL(StockActual,0) > 0 AND ISNULL(Codigo,'') <> 'VARIOS'", conn))
                    {
                        cmd.Parameters.AddWithValue("@p", prodId);
                        enValorizacion = Convert.ToInt32(cmd.ExecuteScalar()) > 0;
                    }
                }
                if (enValorizacion)
                    Registrar("✅ SPRINT-3: Producto con stock elegible para valorización.");
                else
                    Registrar("❌ SPRINT-3: Producto no aparecería en valorización.");

                // Sprint 1 — permisos constantes definidos
                if (!string.IsNullOrEmpty(DatabaseService.PERMISO_COMPRAS)
                    && !string.IsNullOrEmpty(DatabaseService.PERMISO_PROVEEDORES))
                    Registrar("✅ SPRINT-1: Constantes PERMISO_COMPRAS y PERMISO_PROVEEDORES definidas.");
                else
                    Registrar("❌ SPRINT-1: Faltan constantes de permisos.");
            }
            catch (Exception ex)
            {
                Registrar("❌ SPRINT: Excepción — " + ex.Message);
            }
            finally
            {
                if (compraSinStockId > 0) try { DatabaseService.EliminarCompra(compraSinStockId); } catch { }
                if (compraConOcId > 0) try { DatabaseService.EliminarCompra(compraConOcId); } catch { }
                if (ordenId > 0) try { DatabaseService.EliminarOrdenCompra(ordenId); } catch { }
                if (prodId > 0) try { DatabaseService.EliminarProducto(prodId); } catch { }
                if (proveedorId > 0) try { DatabaseService.EliminarProveedor(proveedorId); } catch { }
            }
        }

        static string ObtenerEstadoOrdenCompra(int ordenId)
        {
            try
            {
                using (var conn = new SqlConnection(DatabaseService.ConnectionString))
                {
                    conn.Open();
                    object o = new SqlCommand("SELECT Estado FROM OrdenCompra WHERE OrdenID=" + ordenId, conn).ExecuteScalar();
                    return o?.ToString() ?? "";
                }
            }
            catch { return ""; }
        }

        static int ObtenerCantidadRecibidaOc(int ordenId, int productoId)
        {
            try
            {
                using (var conn = new SqlConnection(DatabaseService.ConnectionString))
                {
                    conn.Open();
                    using (var cmd = new SqlCommand("SELECT CantidadRecibida FROM OrdenCompraDetalle WHERE OrdenID=@o AND ProductoID=@p", conn))
                    {
                        cmd.Parameters.AddWithValue("@o", ordenId);
                        cmd.Parameters.AddWithValue("@p", productoId);
                        object o = cmd.ExecuteScalar();
                        return o == null || o == DBNull.Value ? 0 : Convert.ToInt32(o);
                    }
                }
            }
            catch { return 0; }
        }

        static int ContarRecepcionesPorCompra(int compraId)
        {
            try
            {
                using (var conn = new SqlConnection(DatabaseService.ConnectionString))
                {
                    conn.Open();
                    using (var cmd = new SqlCommand("SELECT COUNT(*) FROM RecepcionesCompra WHERE CompraID=@c", conn))
                    {
                        cmd.Parameters.AddWithValue("@c", compraId);
                        return Convert.ToInt32(cmd.ExecuteScalar());
                    }
                }
            }
            catch { return 0; }
        }
    }
}
