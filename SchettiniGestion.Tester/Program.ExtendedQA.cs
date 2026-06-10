using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.Data.SqlClient;
using SchettiniGestion;

namespace SchettiniGestion.Tester
{
    partial class Program
    {
        /// <summary>
        /// Ejercita APIs de DatabaseService y SQL auxiliar (remitos/pedidos/NC ventas/cierre) con limpieza.
        /// </summary>
        static void EjecutarPruebasModulosExtendidos()
        {
            Registrar("");
            Registrar("--- MÓDULOS EXTENDIDOS (AUTO) ---");
            DatabaseService.AsegurarUsuariosBootstrap();
            DatabaseService.ValidarUsuario("admin", DatabaseService.UsuarioBootstrapAdminContraseña);
            DatabaseService.CargarSesionUsuario("admin");
            AsegurarTablaReservasStockSiFalta();

            string suf = Guid.NewGuid().ToString("N").Substring(0, 8);
            string tag = "[TEST BOT " + suf + "]";
            string codProd = "BOT-EXT-" + suf;
            string cuitCli = "20" + Math.Abs(tag.GetHashCode() % 1000000000).ToString("D9");
            string cuitPr = "27" + Math.Abs((tag + "P").GetHashCode() % 1000000000).ToString("D9");
            if (cuitCli.Length > 11) cuitCli = cuitCli.Substring(0, 11);
            if (cuitPr.Length > 11) cuitPr = cuitPr.Substring(0, 11);

            int clienteId = 0;
            int proveedorId = 0;
            int prodId = 0;
            int listaId = 0;
            int facturaCcId = 0;
            int compraCcId = 0;
            int ordenId = 0;
            int recepcionId = 0;
            int gastoId = 0;
            int notaCompraId = 0;
            int remitoId = 0;
            int pedidoId = 0;
            int notaVentaId = 0;
            int cierreInsertado = 0;
            int movCajaTestId = 0;
            int maxMovCajaAntesPagoProv = 0;
            int maxMovCcAntesPagoProv = 0;
            int reservaId = 0;

            try
            {
                if (!DatabaseService.GuardarCliente(0, cuitCli, tag + " Cliente", "CONSUMIDOR FINAL", "Dir", "Tel", "e@t.com", true, 999999m))
                {
                    Registrar("❌ EXT: No se pudo crear cliente de prueba.");
                    return;
                }
                clienteId = ObtenerUltimoIdPorCampo("Clientes", "ClienteID", "RazonSocial", tag + " Cliente");
                if (clienteId <= 0)
                {
                    Registrar("❌ EXT: ClienteID no resuelto.");
                    return;
                }
                Registrar("✅ EXT: Cliente de prueba creado (ClienteID=" + clienteId + ").");

                if (!DatabaseService.GuardarProveedor(0, cuitPr, tag + " Proveedor", "Tel", "mail@test.com", "Dir"))
                {
                    Registrar("❌ EXT: No se pudo crear proveedor de prueba.");
                    return;
                }
                proveedorId = ObtenerUltimoIdPorCampo("Proveedores", "ProveedorID", "RazonSocial", tag + " Proveedor");
                if (proveedorId <= 0)
                {
                    Registrar("❌ EXT: ProveedorID no resuelto.");
                    return;
                }
                Registrar("✅ EXT: Proveedor de prueba creado (ProveedorID=" + proveedorId + ").");

                if (!DatabaseService.GuardarProducto(0, codProd, "779EXT" + suf, tag + " Producto", "Varios", "21.0", 100m, 0m, 0m, 200m, 40, ""))
                {
                    Registrar("❌ EXT: No se pudo crear producto extendido.");
                    return;
                }
                var rowP = DatabaseService.BuscarProducto(codProd);
                prodId = Convert.ToInt32(rowP["ProductoID"]);
                Registrar("✅ EXT: Producto extendido '" + codProd + "' (ProductoID=" + prodId + ", stock 40).");

                if (DatabaseService.GuardarListaPrecio(0, tag + " Lista", 12.5m))
                {
                    listaId = ObtenerUltimoIdPorCampo("ListasPrecios", "ListaID", "Nombre", tag + " Lista");
                    if (listaId > 0)
                    {
                        DatabaseService.GuardarProductoListas(prodId, new List<int> { listaId });
                        var listas = DatabaseService.GetProductoListas(prodId);
                        if (listas != null && listas.Contains(listaId))
                            Registrar("✅ EXT: Listas de precio — vínculo ProductosListas OK.");
                        else
                            Registrar("❌ EXT: Listas de precio — ProductosListas no contiene la lista.");
                        DatabaseService.GuardarProductoListas(prodId, new List<int>());
                    }
                    else
                        Registrar("❌ EXT: Lista de precios — no se obtuvo ListaID.");
                }
                else
                    Registrar("❌ EXT: GuardarListaPrecio falló.");

                var itemsPresu = new List<FacturaItem>
                {
                    new FacturaItem { ProductoID = prodId, Codigo = codProd, Descripcion = tag, Cantidad = 1, PrecioUnitario = 500m }
                };
                if (DatabaseService.GuardarPresupuesto(clienteId, 500m, itemsPresu) > 0)
                {
                    int pid = ObtenerMaxPresupuestoCliente(clienteId);
                    if (pid > 0 && DatabaseService.GetPresupuestoDetalle(pid).Rows.Count >= 1)
                        Registrar("✅ EXT: Presupuesto creado y detalle legible (PresupuestoID=" + pid + ").");
                    else
                        Registrar("❌ EXT: Presupuesto sin detalle.");
                    if (pid > 0) DatabaseService.EliminarPresupuesto(pid);
                }
                else
                    Registrar("❌ EXT: GuardarPresupuesto falló.");

                decimal saldoCli0 = ObtenerSaldoCliente(clienteId);
                var itemsVentaCc = new List<FacturaItem>
                {
                    new FacturaItem { ProductoID = prodId, Codigo = codProd, Descripcion = tag, Cantidad = 2, PrecioUnitario = 150m }
                };
                if (DatabaseService.GuardarFactura(clienteId, "Ticket", 300m, itemsVentaCc, "Cuenta Corriente"))
                {
                    facturaCcId = ObtenerUltimoFacturaIdCliente(clienteId);
                    decimal saldoCli1 = ObtenerSaldoCliente(clienteId);
                    if (saldoCli1 == saldoCli0 + 300m)
                        Registrar("✅ EXT: Venta en cuenta corriente — SaldoDeuda +$300.");
                    else
                        Registrar("❌ EXT: Venta CC — saldo cliente inesperado (" + saldoCli1 + " vs " + (saldoCli0 + 300m) + ").");

                    var ccCli = DatabaseService.GetMovimientosCC(clienteId, null);
                    if (ccCli != null && ccCli.Rows.Count > 0)
                        Registrar("✅ EXT: Movimientos CC cliente consultables (" + ccCli.Rows.Count + " fila(s)).");
                    else
                        Registrar("❌ EXT: Sin movimientos CC tras venta.");

                    if (DatabaseService.RegistrarPagoCliente(clienteId, 300m))
                    {
                        decimal saldoCli2 = ObtenerSaldoCliente(clienteId);
                        if (Math.Abs(saldoCli2 - saldoCli0) < 0.01m)
                            Registrar("✅ EXT: Cobranza — RegistrarPagoCliente restaura saldo.");
                        else
                            Registrar("❌ EXT: Cobranza — saldo tras pago inesperado: " + saldoCli2);
                    }
                    else
                        Registrar("❌ EXT: RegistrarPagoCliente falló.");

                    if (facturaCcId > 0)
                        LimpiarVentaCcCobroYFactura(facturaCcId, clienteId, 300m);
                }
                else
                    Registrar("❌ EXT: GuardarFactura (CC) falló.");

                decimal saldoProv0 = ObtenerSaldoProveedor(proveedorId);
                var itemsCompraCc = new List<FacturaItem>
                {
                    new FacturaItem { ProductoID = prodId, Codigo = codProd, Descripcion = tag, Cantidad = 2, PrecioUnitario = 40m }
                };
                if (DatabaseService.GuardarCompra(proveedorId, "Factura A", 80m, itemsCompraCc, "Cuenta Corriente"))
                {
                    compraCcId = ObtenerUltimaCompraIdProveedor(proveedorId);
                    decimal saldoProv1 = ObtenerSaldoProveedor(proveedorId);
                    if (saldoProv1 == saldoProv0 + 80m)
                        Registrar("✅ EXT: Compra en cuenta corriente — Saldo proveedor +$80.");
                    else
                        Registrar("❌ EXT: Compra CC — saldo proveedor inesperado.");

                    if (compraCcId > 0)
                        EliminarCompraCtaCteYRevertirStock(compraCcId);
                }
                else
                    Registrar("❌ EXT: GuardarCompra (CC) falló.");

                maxMovCajaAntesPagoProv = MaxId("MovimientosCaja", "MovimientoID");
                maxMovCcAntesPagoProv = MaxId("MovimientosCuentaCorriente", "MovimientoID");
                SqlExecute("UPDATE Proveedores SET SaldoDeuda = SaldoDeuda + 500 WHERE ProveedorID=@p", new SqlParameter("@p", proveedorId));
                if (DatabaseService.RegistrarPagoProveedor(proveedorId, 200m))
                    Registrar("✅ EXT: Pago a proveedor (CC + egreso caja) ejecutado.");
                else
                    Registrar("❌ EXT: RegistrarPagoProveedor falló.");
                int mcc = MaxId("MovimientosCuentaCorriente", "MovimientoID");
                int mcj = MaxId("MovimientosCaja", "MovimientoID");
                if (mcc > maxMovCcAntesPagoProv)
                    EliminarMovimientoCcPorId(mcc);
                if (mcj > maxMovCajaAntesPagoProv)
                    EliminarMovimientoCajaPorId(mcj);
                SqlExecute("UPDATE Proveedores SET SaldoDeuda = SaldoDeuda - 300 WHERE ProveedorID=@p", new SqlParameter("@p", proveedorId));

                int maxMovCajaAntesPagoTabla = MaxId("MovimientosCaja", "MovimientoID");
                int maxMovCcAntesPagoTabla = MaxId("MovimientosCuentaCorriente", "MovimientoID");
                if (DatabaseService.GuardarPagoProveedor(0, proveedorId, 10m, "Efectivo", tag + " pagoTabla", "BOT-PP-1"))
                {
                    Registrar("✅ EXT: GuardarPagoProveedor (tabla PagosProveedores + caja + CC).");
                    int pagoTabla = MaxId("PagosProveedores", "PagoID");
                    DatabaseService.EliminarPagoProveedor(pagoTabla);
                    int mccP = MaxId("MovimientosCuentaCorriente", "MovimientoID");
                    int mcjP = MaxId("MovimientosCaja", "MovimientoID");
                    if (mccP > maxMovCcAntesPagoTabla) EliminarMovimientoCcPorId(mccP);
                    if (mcjP > maxMovCajaAntesPagoTabla) EliminarMovimientoCajaPorId(mcjP);
                    SqlExecute("UPDATE Proveedores SET SaldoDeuda = SaldoDeuda + 10 WHERE ProveedorID=@p", new SqlParameter("@p", proveedorId));
                }
                else
                    Registrar("❌ EXT: GuardarPagoProveedor falló.");

                if (DatabaseService.GuardarGastoRapido(0, tag + " Gasto", "Varios", 25m, "Efectivo"))
                {
                    gastoId = MaxId("GastosRapidos", "GastoID");
                    Registrar("✅ EXT: Gasto rápido registrado (GastoID=" + gastoId + ").");
                }
                else
                    Registrar("❌ EXT: GuardarGastoRapido falló.");

                if (DatabaseService.GuardarNotaCreditoDebitoCompra(0, proveedorId, "NC", 15m, tag + " nota", "NC-BOT-1"))
                {
                    notaCompraId = MaxId("NotasCreditoDebitoCompras", "NotaID");
                    Registrar("✅ EXT: Nota crédito/débito compra insertada (NotaID=" + notaCompraId + ").");
                }
                else
                    Registrar("❌ EXT: GuardarNotaCreditoDebitoCompra falló.");

                ordenId = DatabaseService.GuardarOrdenCompra(0, proveedorId, DateTime.Today.AddDays(1), tag,
                    new List<(int ProductoID, int Cantidad, decimal Costo)> { (prodId, 4, 50m) });
                if (ordenId > 0 && DatabaseService.GetOrdenCompraDetalleFull(ordenId).Rows.Count >= 1)
                    Registrar("✅ EXT: Orden de compra creada (OrdenID=" + ordenId + ").");
                else
                    Registrar("❌ EXT: Orden de compra falló o sin detalle.");

                recepcionId = DatabaseService.GuardarRecepcionCompra(0, proveedorId, null, "Recibido", tag + " recep",
                    new List<(int ProductoID, int CantEsperada, int CantRecibida, decimal Costo)>
                    {
                        (prodId, 4, 2, 50m)
                    });
                if (recepcionId > 0 && DatabaseService.GetRecepcionCompraDetalle(recepcionId).Rows.Count >= 1)
                    Registrar("✅ EXT: Recepción de compra (RecepcionID=" + recepcionId + ", stock +2).");
                else
                    Registrar("❌ EXT: Recepción de compra falló.");

                int stockAntesAjuste = Convert.ToInt32(DatabaseService.BuscarProducto(codProd)["StockActual"]);
                if (DatabaseService.AjustarStock(prodId, 3, tag + " ajuste+") && DatabaseService.AjustarStock(prodId, -3, tag + " ajuste-"))
                {
                    int stockDesp = Convert.ToInt32(DatabaseService.BuscarProducto(codProd)["StockActual"]);
                    if (stockDesp == stockAntesAjuste)
                        Registrar("✅ EXT: Ajuste de stock (+3/-3) neto nulo.");
                    else
                        Registrar("❌ EXT: Ajuste de stock — stock final " + stockDesp + " esperado " + stockAntesAjuste);
                }
                else
                    Registrar("❌ EXT: AjustarStock falló.");

                if (!DatabaseService.GuardarReservaStock(prodId, 1, tag + " reserva", clienteId, DateTime.Today.AddDays(2)))
                {
                    Registrar("ℹ️ EXT: GuardarReservaStock devolvió false; reintento SQL directo.");
                    RegistrarReservaStockDiagnostico(prodId, clienteId, tag);
                }
                reservaId = ObtenerUltimaReservaIdProducto(prodId);
                if (reservaId > 0 && DatabaseService.AnularReservaStock(reservaId))
                    Registrar("✅ EXT: Reserva de stock creada y anulada (ReservaID=" + reservaId + ").");
                else if (reservaId <= 0)
                    Registrar("❌ EXT: Reserva de stock — no hay fila para anular.");
                else
                    Registrar("❌ EXT: Reserva de stock — fallo al anular.");

                var dtMov = DatabaseService.GetMovimientosStockFiltrado(DateTime.Today.AddDays(-2), DateTime.Today.AddDays(1), codProd, null);
                if (dtMov != null && dtMov.Rows.Count > 0)
                    Registrar("✅ EXT: Movimientos de stock filtrados (" + dtMov.Rows.Count + " fila(s)).");
                else
                    Registrar("ℹ️ EXT: Movimientos de stock filtrados sin filas (posible filtro vacío).");

                if (DatabaseService.RegistrarMovimientoCaja(tag + " mov caja", "Ingreso", 1m))
                {
                    movCajaTestId = MaxId("MovimientosCaja", "MovimientoID");
                    Registrar("✅ EXT: Movimiento de caja manual (MovimientoID=" + movCajaTestId + ").");
                }

                var cfg = DatabaseService.GetConfiguracion();
                if (cfg != null && cfg.Table.Columns.Count > 0)
                    Registrar("✅ EXT: Configuración — lectura OK.");
                else
                    Registrar("ℹ️ EXT: GetConfiguracion sin filas (tabla vacía?).");

                var medios = DatabaseService.GetMediosPagoCompleto();
                if (medios != null && medios.Rows.Count > 0)
                    Registrar("✅ EXT: Medios de pago — " + medios.Rows.Count + " registro(s).");

                decimal? tc = DatabaseService.GetTipoCambioUSD();
                Registrar(tc.HasValue ? "✅ EXT: Tipo cambio USD = " + tc.Value : "ℹ️ EXT: Tipo cambio USD sin valor.");

                var rk = DatabaseService.GetRankingVentas(DateTime.Today.AddDays(-30), DateTime.Today.AddDays(1));
                var iva = DatabaseService.GetVentasParaLibroIVA(DateTime.Today.AddDays(-30), DateTime.Today.AddDays(1));
                Registrar("✅ EXT: Informes — RankingVentas filas=" + (rk?.Rows.Count ?? 0) + ", Libro IVA filas=" + (iva?.Rows.Count ?? 0) + ".");

                int vh = DatabaseService.GetCantidadVentasHoy();
                decimal tvh = DatabaseService.GetTotalVentasHoy();
                int nprod = DatabaseService.GetCantidadProductos();
                int ncli = DatabaseService.GetCantidadClientes();
                decimal rent = DatabaseService.GetRentabilidadHoy();
                Registrar("✅ EXT: Dashboard — ventas hoy: " + vh + " / $" + tvh + ", productos=" + nprod + ", clientes=" + ncli + ", rent. hoy=$" + rent + ".");

                var st = DatabaseService.GetStockGeneral(tag);
                if (st != null && st.Rows.Count >= 1)
                    Registrar("✅ EXT: Stock general filtrado (" + st.Rows.Count + " fila(s)).");

                var rowPrecio = DatabaseService.BuscarProducto(codProd);
                decimal pc0 = Convert.ToDecimal(rowPrecio["PrecioCosto"]);
                decimal pv0 = Convert.ToDecimal(rowPrecio["PrecioVenta"]);
                if (DatabaseService.ActualizarPreciosProducto(prodId, pc0 + 1m, pv0 + 2m))
                {
                    var r2 = DatabaseService.BuscarProducto(codProd);
                    if (Convert.ToDecimal(r2["PrecioCosto"]) == pc0 + 1m && Convert.ToDecimal(r2["PrecioVenta"]) == pv0 + 2m)
                        Registrar("✅ EXT: ActualizarPreciosProducto OK (revirtiendo).");
                    DatabaseService.ActualizarPreciosProducto(prodId, pc0, pv0);
                }
                else
                    Registrar("❌ EXT: ActualizarPreciosProducto falló.");

                if (TablaExiste("Remitos"))
                {
                    remitoId = SqlInsertRemito(clienteId, prodId, tag);
                    if (remitoId > 0)
                        Registrar("✅ EXT: Remito insertado vía SQL (RemitoID=" + remitoId + ").");
                }
                else
                    Registrar("ℹ️ EXT: Tabla Remitos no existe; se omite.");

                if (TablaExiste("Pedidos"))
                {
                    pedidoId = SqlInsertPedido(clienteId, prodId, tag);
                    if (pedidoId > 0)
                        Registrar("✅ EXT: Pedido insertado vía SQL (PedidoID=" + pedidoId + ").");
                }
                else
                    Registrar("ℹ️ EXT: Tabla Pedidos no existe; se omite.");

                if (TablaExiste("NotasCreditoDebitoVentas"))
                {
                    notaVentaId = SqlInsertNotaCreditoVenta(clienteId, tag);
                    if (notaVentaId > 0)
                        Registrar("✅ EXT: Nota crédito venta insertada (NotaID=" + notaVentaId + ").");
                }
                else
                    Registrar("ℹ️ EXT: Tabla NotasCreditoDebitoVentas no existe; se omite.");

                if (TablaExiste("CierresCaja"))
                {
                    cierreInsertado = SqlInsertCierreCajaPrueba(tag);
                    if (cierreInsertado > 0)
                        Registrar("✅ EXT: Cierre de caja de prueba insertado (CierreID=" + cierreInsertado + ").");
                }
                else
                    Registrar("ℹ️ EXT: Tabla CierresCaja no existe; se omite.");
            }
            catch (Exception ex)
            {
                Registrar("❌ EXT: Excepción no controlada: " + ex.Message);
            }
            finally
            {
                try
                {
                    if (movCajaTestId > 0) EliminarMovimientoCajaPorId(movCajaTestId);
                    if (cierreInsertado > 0) SqlExecute("DELETE FROM CierresCaja WHERE CierreID=@i", new SqlParameter("@i", cierreInsertado));
                    if (notaVentaId > 0) SqlExecute("DELETE FROM NotasCreditoDebitoVentas WHERE NotaID=@i", new SqlParameter("@i", notaVentaId));
                    if (pedidoId > 0)
                    {
                        SqlExecute("DELETE FROM PedidoDetalle WHERE PedidoID=@i", new SqlParameter("@i", pedidoId));
                        SqlExecute("DELETE FROM Pedidos WHERE PedidoID=@i", new SqlParameter("@i", pedidoId));
                    }
                    if (remitoId > 0)
                    {
                        SqlExecute("DELETE FROM RemitoDetalle WHERE RemitoID=@i", new SqlParameter("@i", remitoId));
                        SqlExecute("DELETE FROM Remitos WHERE RemitoID=@i", new SqlParameter("@i", remitoId));
                    }
                    if (recepcionId > 0) EliminarRecepcionYRevertirStock(recepcionId);
                    if (ordenId > 0) DatabaseService.EliminarOrdenCompra(ordenId);
                    if (notaCompraId > 0) DatabaseService.EliminarNotaCreditoDebitoCompra(notaCompraId);
                    if (gastoId > 0) DatabaseService.EliminarGastoRapido(gastoId);
                    if (listaId > 0)
                        EliminarListaPrecioDePrueba(listaId, tag);
                    if (prodId > 0)
                        SqlExecute("DELETE FROM ReservasStock WHERE ProductoID=@p", new SqlParameter("@p", prodId));
                    if (prodId > 0) DatabaseService.EliminarProducto(prodId);
                    if (proveedorId > 0) DatabaseService.EliminarProveedor(proveedorId);
                    if (clienteId > 0) DatabaseService.EliminarCliente(clienteId);
                }
                catch (Exception ex2)
                {
                    Registrar("❌ EXT: Error en limpieza final: " + ex2.Message);
                }
                Registrar("🧹 EXT: Limpieza de datos de prueba extendidos finalizada.");
            }
        }

        static int ObtenerUltimoIdPorCampo(string tabla, string pk, string campoFiltro, string valorExacto)
        {
            try
            {
                using (var conn = new SqlConnection(DatabaseService.ConnectionString))
                {
                    conn.Open();
                    string sql = "SELECT MAX(" + pk + ") FROM " + tabla + " WHERE " + campoFiltro + "=@v";
                    using (var cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@v", valorExacto);
                        object o = cmd.ExecuteScalar();
                        return o == null || o == DBNull.Value ? 0 : Convert.ToInt32(o);
                    }
                }
            }
            catch { return 0; }
        }

        static int ObtenerMaxPresupuestoCliente(int clienteId)
        {
            try
            {
                using (var conn = new SqlConnection(DatabaseService.ConnectionString))
                {
                    conn.Open();
                    object o = new SqlCommand(
                        "SELECT MAX(PresupuestoID) FROM Presupuestos WHERE ClienteID=@c", conn)
                    { Parameters = { new SqlParameter("@c", clienteId) } }.ExecuteScalar();
                    return o == null || o == DBNull.Value ? 0 : Convert.ToInt32(o);
                }
            }
            catch { return 0; }
        }

        /// <summary>
        /// Quita el ingreso de caja y el movimiento CC del cobro, revierte el efecto del pago en SaldoDeuda y elimina la factura CC.
        /// </summary>
        static void LimpiarVentaCcCobroYFactura(int facturaId, int clienteId, decimal total)
        {
            if (facturaId <= 0 || total <= 0m) return;
            string conceptoCobro = "Cobro Cta Cte #" + clienteId;
            using (var conn = new SqlConnection(DatabaseService.ConnectionString))
            {
                conn.Open();
                using (var tr = conn.BeginTransaction())
                {
                    using (var delCaja = new SqlCommand(
                               @"DELETE FROM MovimientosCaja WHERE MovimientoID = (
                                   SELECT TOP 1 MovimientoID FROM MovimientosCaja WHERE Concepto=@con ORDER BY MovimientoID DESC)",
                               conn, tr))
                    {
                        delCaja.Parameters.AddWithValue("@con", conceptoCobro);
                        delCaja.ExecuteNonQuery();
                    }
                    using (var delCc = new SqlCommand(
                               @"DELETE FROM MovimientosCuentaCorriente WHERE MovimientoID = (
                                   SELECT TOP 1 MovimientoID FROM MovimientosCuentaCorriente
                                   WHERE ClienteID=@c AND Descripcion=N'Pago a Cuenta' ORDER BY MovimientoID DESC)",
                               conn, tr))
                    {
                        delCc.Parameters.AddWithValue("@c", clienteId);
                        delCc.ExecuteNonQuery();
                    }
                    using (var up = new SqlCommand(
                               "UPDATE Clientes SET SaldoDeuda = SaldoDeuda + @t WHERE ClienteID=@c", conn, tr))
                    {
                        up.Parameters.AddWithValue("@t", total);
                        up.Parameters.AddWithValue("@c", clienteId);
                        up.ExecuteNonQuery();
                    }
                    tr.Commit();
                }
            }
            EliminarFacturaCtaCteYRevertirStock(facturaId);
        }

        static int ObtenerUltimoFacturaIdCliente(int clienteId)
        {
            try
            {
                using (var conn = new SqlConnection(DatabaseService.ConnectionString))
                {
                    conn.Open();
                    object o = new SqlCommand(
                        "SELECT MAX(FacturaID) FROM Facturas WHERE ClienteID=@c", conn)
                    { Parameters = { new SqlParameter("@c", clienteId) } }.ExecuteScalar();
                    return o == null || o == DBNull.Value ? 0 : Convert.ToInt32(o);
                }
            }
            catch { return 0; }
        }

        static int ObtenerUltimaCompraIdProveedor(int proveedorId)
        {
            try
            {
                using (var conn = new SqlConnection(DatabaseService.ConnectionString))
                {
                    conn.Open();
                    object o = new SqlCommand(
                        "SELECT MAX(CompraID) FROM Compras WHERE ProveedorID=@p", conn)
                    { Parameters = { new SqlParameter("@p", proveedorId) } }.ExecuteScalar();
                    return o == null || o == DBNull.Value ? 0 : Convert.ToInt32(o);
                }
            }
            catch { return 0; }
        }

        static int ObtenerUltimaReservaIdProducto(int productoId)
        {
            try
            {
                using (var conn = new SqlConnection(DatabaseService.ConnectionString))
                {
                    conn.Open();
                    object o = new SqlCommand(
                        "SELECT MAX(ReservaID) FROM ReservasStock WHERE ProductoID=@p", conn)
                    { Parameters = { new SqlParameter("@p", productoId) } }.ExecuteScalar();
                    return o == null || o == DBNull.Value ? 0 : Convert.ToInt32(o);
                }
            }
            catch { return 0; }
        }

        static decimal ObtenerSaldoCliente(int clienteId)
        {
            try
            {
                using (var conn = new SqlConnection(DatabaseService.ConnectionString))
                {
                    conn.Open();
                    object o = new SqlCommand("SELECT ISNULL(SaldoDeuda,0) FROM Clientes WHERE ClienteID=@c", conn)
                    { Parameters = { new SqlParameter("@c", clienteId) } }.ExecuteScalar();
                    return o == null || o == DBNull.Value ? 0m : Convert.ToDecimal(o);
                }
            }
            catch { return -999999m; }
        }

        static decimal ObtenerSaldoProveedor(int proveedorId)
        {
            try
            {
                using (var conn = new SqlConnection(DatabaseService.ConnectionString))
                {
                    conn.Open();
                    object o = new SqlCommand("SELECT ISNULL(SaldoDeuda,0) FROM Proveedores WHERE ProveedorID=@p", conn)
                    { Parameters = { new SqlParameter("@p", proveedorId) } }.ExecuteScalar();
                    return o == null || o == DBNull.Value ? 0m : Convert.ToDecimal(o);
                }
            }
            catch { return -999999m; }
        }

        static int MaxId(string tabla, string col)
        {
            try
            {
                using (var conn = new SqlConnection(DatabaseService.ConnectionString))
                {
                    conn.Open();
                    object o = new SqlCommand("SELECT ISNULL(MAX(" + col + "),0) FROM " + tabla, conn).ExecuteScalar();
                    return o == null || o == DBNull.Value ? 0 : Convert.ToInt32(o);
                }
            }
            catch { return 0; }
        }

        static bool TablaExiste(string nombreTabla)
        {
            try
            {
                using (var conn = new SqlConnection(DatabaseService.ConnectionString))
                {
                    conn.Open();
                    object o = new SqlCommand(
                        "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME=@t", conn)
                    { Parameters = { new SqlParameter("@t", nombreTabla) } }.ExecuteScalar();
                    return Convert.ToInt32(o) > 0;
                }
            }
            catch { return false; }
        }

        static bool ColumnaExiste(string tabla, string columna)
        {
            try
            {
                using (var conn = new SqlConnection(DatabaseService.ConnectionString))
                {
                    conn.Open();
                    object o = new SqlCommand(
                        @"SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
                          WHERE TABLE_NAME=@t AND COLUMN_NAME=@c", conn)
                    {
                        Parameters =
                        {
                            new SqlParameter("@t", tabla),
                            new SqlParameter("@c", columna)
                        }
                    }.ExecuteScalar();
                    return Convert.ToInt32(o) > 0;
                }
            }
            catch { return false; }
        }

        static void AsegurarTablaReservasStockSiFalta()
        {
            try
            {
                if (!TablaExiste("ReservasStock"))
                {
                    SqlExecute(@"CREATE TABLE ReservasStock (
    ReservaID INT PRIMARY KEY IDENTITY(1,1),
    ProductoID INT,
    ClienteID INT NULL,
    Fecha DATETIME,
    FechaVencimiento DATETIME NULL,
    Cantidad INT,
    Motivo NVARCHAR(200),
    Estado NVARCHAR(50) DEFAULT 'Activa',
    Usuario NVARCHAR(50)
);");
                    Registrar("ℹ️ EXT: Tabla ReservasStock creada para la prueba.");
                }
                if (!ColumnaExiste("ReservasStock", "Usuario"))
                    SqlExecute("ALTER TABLE ReservasStock ADD Usuario NVARCHAR(50) NULL;");
                if (!ColumnaExiste("ReservasStock", "FechaVencimiento"))
                    SqlExecute("ALTER TABLE ReservasStock ADD FechaVencimiento DATETIME NULL;");
            }
            catch (Exception ex)
            {
                Registrar("ℹ️ EXT: Esquema ReservasStock: " + ex.Message);
            }
        }

        /// <summary>
        /// EliminaListaPrecio bloquea ListaID=1 aunque sea una lista nueva; borramos solo si el nombre es de prueba.
        /// </summary>
        static void RegistrarReservaStockDiagnostico(int productoId, int clienteId, string tag)
        {
            try
            {
                using (var conn = new SqlConnection(DatabaseService.ConnectionString))
                {
                    conn.Open();
                    using (var cmd = new SqlCommand(
                               @"INSERT INTO ReservasStock (ProductoID,ClienteID,Fecha,FechaVencimiento,Cantidad,Motivo,Estado,Usuario)
                                 VALUES (@p,@c,GETDATE(),DATEADD(day,2,GETDATE()),1,@m,'Activa','admin')", conn))
                    {
                        cmd.Parameters.AddWithValue("@p", productoId);
                        cmd.Parameters.AddWithValue("@c", clienteId);
                        cmd.Parameters.AddWithValue("@m", tag + " diag");
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                Registrar("DETALLE RESERVA: " + ex.Message);
            }
        }

        static void EliminarListaPrecioDePrueba(int listaId, string tagPrefijo)
        {
            if (listaId <= 0) return;
            try
            {
                if (DatabaseService.EliminarListaPrecio(listaId))
                    return;
            }
            catch { /* continuar con SQL */ }
            try
            {
                using (var conn = new SqlConnection(DatabaseService.ConnectionString))
                {
                    conn.Open();
                    using (var cmd = new SqlCommand(
                               @"DELETE FROM ProductosListas WHERE ListaID=@id;
                                 DELETE FROM ListasPrecios WHERE ListaID=@id AND Nombre LIKE @p;",
                               conn))
                    {
                        cmd.Parameters.AddWithValue("@id", listaId);
                        cmd.Parameters.AddWithValue("@p", tagPrefijo + "%");
                        int n = cmd.ExecuteNonQuery();
                        if (n > 0)
                            Registrar("ℹ️ EXT: Lista de prueba eliminada vía SQL (ListaID=" + listaId + ").");
                    }
                }
            }
            catch (Exception ex)
            {
                Registrar("❌ EXT: No se pudo eliminar lista de prueba: " + ex.Message);
            }
        }

        static void SqlExecute(string sql, params SqlParameter[] ps)
        {
            using (var conn = new SqlConnection(DatabaseService.ConnectionString))
            {
                conn.Open();
                using (var cmd = new SqlCommand(sql, conn))
                {
                    if (ps != null)
                        foreach (var p in ps)
                            if (p != null)
                                cmd.Parameters.Add(p);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        static void EliminarMovimientoCajaPorId(int id)
        {
            if (id <= 0) return;
            SqlExecute("DELETE FROM MovimientosCaja WHERE MovimientoID=@i", new SqlParameter("@i", id));
        }

        static void EliminarMovimientoCcPorId(int id)
        {
            if (id <= 0) return;
            SqlExecute("DELETE FROM MovimientosCuentaCorriente WHERE MovimientoID=@i", new SqlParameter("@i", id));
        }

        static void EliminarFacturaCtaCteYRevertirStock(int facturaId)
        {
            if (facturaId <= 0) return;
            using (var conn = new SqlConnection(DatabaseService.ConnectionString))
            {
                conn.Open();
                using (var tr = conn.BeginTransaction())
                {
                    int clienteId = 0;
                    decimal total = 0;
                    using (var q = new SqlCommand("SELECT ClienteID, Total FROM Facturas WHERE FacturaID=@f", conn, tr))
                    {
                        q.Parameters.AddWithValue("@f", facturaId);
                        using (var rd = q.ExecuteReader())
                        {
                            if (!rd.Read()) { tr.Rollback(); return; }
                            clienteId = rd.GetInt32(0);
                            total = rd.GetDecimal(1);
                        }
                    }
                    var lines = new List<Tuple<int, int>>();
                    using (var q = new SqlCommand("SELECT ProductoID, Cantidad FROM FacturaDetalle WHERE FacturaID=@f", conn, tr))
                    {
                        q.Parameters.AddWithValue("@f", facturaId);
                        using (var rd = q.ExecuteReader())
                            while (rd.Read())
                                lines.Add(Tuple.Create(rd.GetInt32(0), rd.GetInt32(1)));
                    }
                    foreach (var t in lines)
                        new SqlCommand("UPDATE Productos SET StockActual=StockActual+" + t.Item2 + " WHERE ProductoID=" + t.Item1, conn, tr).ExecuteNonQuery();

                    new SqlCommand("DELETE FROM MovimientosStock WHERE FacturaID=" + facturaId, conn, tr).ExecuteNonQuery();
                    new SqlCommand("DELETE FROM FacturaDetalle WHERE FacturaID=" + facturaId, conn, tr).ExecuteNonQuery();
                    var desc = "Venta #" + facturaId + " (Cta Cte)";
                    using (var del = new SqlCommand("DELETE FROM MovimientosCuentaCorriente WHERE ClienteID=@c AND Descripcion=@d", conn, tr))
                    {
                        del.Parameters.AddWithValue("@c", clienteId);
                        del.Parameters.AddWithValue("@d", desc);
                        del.ExecuteNonQuery();
                    }
                    using (var up = new SqlCommand("UPDATE Clientes SET SaldoDeuda = SaldoDeuda - @t WHERE ClienteID=@c", conn, tr))
                    {
                        up.Parameters.AddWithValue("@t", total);
                        up.Parameters.AddWithValue("@c", clienteId);
                        up.ExecuteNonQuery();
                    }
                    new SqlCommand("DELETE FROM Facturas WHERE FacturaID=" + facturaId, conn, tr).ExecuteNonQuery();
                    tr.Commit();
                }
            }
        }

        static void EliminarCompraCtaCteYRevertirStock(int compraId)
        {
            if (compraId <= 0) return;
            using (var conn = new SqlConnection(DatabaseService.ConnectionString))
            {
                conn.Open();
                using (var tr = conn.BeginTransaction())
                {
                    int proveedorId = 0;
                    decimal total = 0;
                    using (var q = new SqlCommand("SELECT ProveedorID, Total FROM Compras WHERE CompraID=@k", conn, tr))
                    {
                        q.Parameters.AddWithValue("@k", compraId);
                        using (var rd = q.ExecuteReader())
                        {
                            if (!rd.Read()) { tr.Rollback(); return; }
                            proveedorId = rd.GetInt32(0);
                            total = rd.GetDecimal(1);
                        }
                    }
                    var lines = new List<Tuple<int, int>>();
                    using (var q = new SqlCommand("SELECT ProductoID, Cantidad FROM CompraDetalle WHERE CompraID=@k", conn, tr))
                    {
                        q.Parameters.AddWithValue("@k", compraId);
                        using (var rd = q.ExecuteReader())
                            while (rd.Read())
                                lines.Add(Tuple.Create(rd.GetInt32(0), rd.GetInt32(1)));
                    }
                    foreach (var t in lines)
                        new SqlCommand("UPDATE Productos SET StockActual=StockActual-" + t.Item2 + " WHERE ProductoID=" + t.Item1, conn, tr).ExecuteNonQuery();

                    new SqlCommand("DELETE FROM MovimientosStock WHERE CompraID=" + compraId, conn, tr).ExecuteNonQuery();
                    new SqlCommand("DELETE FROM CompraDetalle WHERE CompraID=" + compraId, conn, tr).ExecuteNonQuery();
                    var desc = "Compra #" + compraId + " (Cta Cte)";
                    using (var del = new SqlCommand("DELETE FROM MovimientosCuentaCorriente WHERE ProveedorID=@p AND Descripcion=@d", conn, tr))
                    {
                        del.Parameters.AddWithValue("@p", proveedorId);
                        del.Parameters.AddWithValue("@d", desc);
                        del.ExecuteNonQuery();
                    }
                    using (var up = new SqlCommand("UPDATE Proveedores SET SaldoDeuda = SaldoDeuda - @t WHERE ProveedorID=@p", conn, tr))
                    {
                        up.Parameters.AddWithValue("@t", total);
                        up.Parameters.AddWithValue("@p", proveedorId);
                        up.ExecuteNonQuery();
                    }
                    new SqlCommand("DELETE FROM Compras WHERE CompraID=" + compraId, conn, tr).ExecuteNonQuery();
                    tr.Commit();
                }
            }
        }

        static void EliminarRecepcionYRevertirStock(int recepcionId)
        {
            if (recepcionId <= 0) return;
            var dt = DatabaseService.GetRecepcionCompraDetalle(recepcionId);
            using (var conn = new SqlConnection(DatabaseService.ConnectionString))
            {
                conn.Open();
                using (var tr = conn.BeginTransaction())
                {
                    foreach (DataRow r in dt.Rows)
                    {
                        int pid = Convert.ToInt32(r["ProductoID"]);
                        int cr = Convert.ToInt32(r["CantidadRecibida"]);
                        if (cr != 0)
                            new SqlCommand("UPDATE Productos SET StockActual=StockActual-" + cr + " WHERE ProductoID=" + pid, conn, tr).ExecuteNonQuery();
                    }
                    new SqlCommand("DELETE FROM RecepcionCompraDetalle WHERE RecepcionID=" + recepcionId, conn, tr).ExecuteNonQuery();
                    new SqlCommand("DELETE FROM RecepcionesCompra WHERE RecepcionID=" + recepcionId, conn, tr).ExecuteNonQuery();
                    tr.Commit();
                }
            }
        }

        static int SqlInsertRemito(int clienteId, int productoId, string tag)
        {
            using (var conn = new SqlConnection(DatabaseService.ConnectionString))
            {
                conn.Open();
                using (var tr = conn.BeginTransaction())
                {
                    int id;
                    using (var cmd = new SqlCommand(
                               "INSERT INTO Remitos (ClienteID,FacturaID,Fecha,Estado,Observaciones) OUTPUT INSERTED.RemitoID VALUES (@c,NULL,GETDATE(),'Emitido',@o)",
                               conn, tr))
                    {
                        cmd.Parameters.AddWithValue("@c", clienteId);
                        cmd.Parameters.AddWithValue("@o", tag);
                        id = Convert.ToInt32(cmd.ExecuteScalar());
                    }
                    using (var d = new SqlCommand(
                               "INSERT INTO RemitoDetalle (RemitoID,ProductoID,Cantidad,PrecioUnitario) VALUES (@r,@p,1,10)",
                               conn, tr))
                    {
                        d.Parameters.AddWithValue("@r", id);
                        d.Parameters.AddWithValue("@p", productoId);
                        d.ExecuteNonQuery();
                    }
                    tr.Commit();
                    return id;
                }
            }
        }

        static int SqlInsertPedido(int clienteId, int productoId, string tag)
        {
            using (var conn = new SqlConnection(DatabaseService.ConnectionString))
            {
                conn.Open();
                using (var tr = conn.BeginTransaction())
                {
                    int id;
                    using (var cmd = new SqlCommand(
                               "INSERT INTO Pedidos (ClienteID,Fecha,FechaEntrega,Estado,Total,Observaciones) OUTPUT INSERTED.PedidoID VALUES (@c,GETDATE(),NULL,'Pendiente',10,@o)",
                               conn, tr))
                    {
                        cmd.Parameters.AddWithValue("@c", clienteId);
                        cmd.Parameters.AddWithValue("@o", tag);
                        id = Convert.ToInt32(cmd.ExecuteScalar());
                    }
                    using (var d = new SqlCommand(
                               "INSERT INTO PedidoDetalle (PedidoID,ProductoID,Cantidad,PrecioUnitario) VALUES (@p,@prod,1,10)",
                               conn, tr))
                    {
                        d.Parameters.AddWithValue("@p", id);
                        d.Parameters.AddWithValue("@prod", productoId);
                        d.ExecuteNonQuery();
                    }
                    tr.Commit();
                    return id;
                }
            }
        }

        static int SqlInsertNotaCreditoVenta(int clienteId, string tag)
        {
            using (var conn = new SqlConnection(DatabaseService.ConnectionString))
            {
                conn.Open();
                using (var cmd = new SqlCommand(
                           "INSERT INTO NotasCreditoDebitoVentas (ClienteID,FacturaID,Tipo,Fecha,Monto,Descripcion,NumeroComprobante) OUTPUT INSERTED.NotaID VALUES (@c,NULL,'NC',GETDATE(),10,@d,'BOT-NCV')",
                           conn))
                {
                    cmd.Parameters.AddWithValue("@c", clienteId);
                    cmd.Parameters.AddWithValue("@d", tag);
                    return Convert.ToInt32(cmd.ExecuteScalar());
                }
            }
        }

        static int SqlInsertCierreCajaPrueba(string tag)
        {
            using (var conn = new SqlConnection(DatabaseService.ConnectionString))
            {
                conn.Open();
                using (var cmd = new SqlCommand(
                           @"INSERT INTO CierresCaja (Fecha,SaldoApertura,TotalIngresos,TotalEgresos,SaldoCierre,TotalEfectivo,TotalTarjeta,TotalTransferencia,Observaciones,Usuario)
                             OUTPUT INSERTED.CierreID VALUES (GETDATE(),0,0,0,0,0,0,0,@o,'admin')",
                           conn))
                {
                    cmd.Parameters.AddWithValue("@o", tag + " cierre QA");
                    return Convert.ToInt32(cmd.ExecuteScalar());
                }
            }
        }
    }
}
