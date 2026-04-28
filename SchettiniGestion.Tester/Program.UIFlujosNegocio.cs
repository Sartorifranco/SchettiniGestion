using System;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Input;
using FlaUI.Core.Definitions;
using FlaUI.Core.Tools;
using FlaUI.Core.WindowsAPI;
using FlaUI.UIA3;
using SchettiniGestion;
using UiWindow = FlaUI.Core.AutomationElements.Window;

namespace SchettiniGestion.Tester
{
    /// <summary>
    /// Segundo escalón: flujos de negocio en UI (modales, POS sin confirmar impresión, caja).
    /// </summary>
    partial class Program
    {
        static void EjecutarFlujosNegocioProfundos(Application app, UIA3Automation automation, UiWindow principal)
        {
            if (string.Equals(Environment.GetEnvironmentVariable("SCHPOS_SKIP_UI_FLOWS"), "1", StringComparison.Ordinal))
            {
                Registrar("ℹ️ UI-FLOWS: Omitido (variable SCHPOS_SKIP_UI_FLOWS=1).");
                return;
            }

            Registrar("");
            Registrar("--- UI — FLUJOS DE NEGOCIO (paso 2) ---");

            try
            {
                FlujoCajaPopupIngresoCancelar(app, automation, principal);
                FlujoVentasHistorialBuscarSiAplica(app, automation, principal);
                FlujoPosAyudaAtajos(app, automation, principal);
                FlujoPosPresupuestoAbrirCobroYCancelar(app, automation, principal);
                Registrar("✅ UI-FLOWS: Bloque de flujos de negocio finalizado (ver líneas anteriores por resultado de cada paso).");
            }
            catch (Exception ex)
            {
                Registrar("❌ UI-FLOWS: Error general en flujos de negocio: " + ex.Message);
            }
        }

        static void IrMenuLateralYEsperar(Application app, UIA3Automation automation, UiWindow principal, string patronMenu)
        {
            var btn = BuscarBotonMenu(principal, automation, patronMenu);
            if (btn == null)
                throw new InvalidOperationException("No se encontró el menú lateral que contenga \"" + patronMenu + "\".");
            btn.AsButton().Invoke();
            app.WaitWhileBusy(TimeSpan.FromSeconds(5));
            Thread.Sleep(500);
            CerrarModalesInformativosSiHay(app, automation, principal);
        }

        static void FlujoCajaPopupIngresoCancelar(Application app, UIA3Automation automation, UiWindow principal)
        {
            try
            {
                IrMenuLateralYEsperar(app, automation, principal, "Caja");
                var btnIng = principal.FindFirstDescendant(automation.ConditionFactory.ByAutomationId("btnIngreso"));
                if (btnIng == null)
                {
                    Registrar("⚠️ UI-FLOW-CAJA: No se encontró btnIngreso (AutomationId).");
                    return;
                }
                btnIng.AsButton().Invoke();
                app.WaitWhileBusy(TimeSpan.FromSeconds(2));
                Thread.Sleep(400);
                var cancel = principal.FindAllDescendants(automation.ConditionFactory.ByControlType(ControlType.Button))
                    .FirstOrDefault(b => string.Equals((b.Name ?? "").Trim(), "Cancelar", StringComparison.OrdinalIgnoreCase));
                if (cancel == null)
                {
                    Registrar("❌ UI-FLOW-CAJA: Popup abierto pero no hay botón Cancelar visible en UIA.");
                    return;
                }
                cancel.AsButton().Invoke();
                app.WaitWhileBusy(TimeSpan.FromSeconds(2));
                Thread.Sleep(300);
                Registrar("✅ UI-FLOW-CAJA: Ingreso → popup → Cancelar (sin guardar movimiento).");
            }
            catch (Exception ex)
            {
                Registrar("⚠️ UI-FLOW-CAJA: " + ex.Message);
            }
        }

        static void FlujoVentasHistorialBuscarSiAplica(Application app, UIA3Automation automation, UiWindow principal)
        {
            try
            {
                IrMenuLateralYEsperar(app, automation, principal, "Ventas");
                var buscarHist = principal.FindFirstDescendant(automation.ConditionFactory.ByAutomationId("btnBuscar"));
                if (buscarHist == null)
                {
                    Registrar("ℹ️ UI-FLOW-VENTAS: Vista actual no es historial de ventas (sin btnBuscar); se omite búsqueda en grilla.");
                    return;
                }
                buscarHist.AsButton().Invoke();
                app.WaitWhileBusy(TimeSpan.FromSeconds(4));
                Thread.Sleep(500);
                RegistrarAuditoriaSuperficie(automation, principal, "VENTAS — Tras BUSCAR en historial",
                    System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Reporte_QA_UI_flow_ventas_historial.png"));
                Registrar("✅ UI-FLOW-VENTAS: Pulsado BUSCAR en historial de ventas.");
            }
            catch (Exception ex)
            {
                Registrar("ℹ️ UI-FLOW-VENTAS: " + ex.Message);
            }
        }

        static void FlujoPosAyudaAtajos(Application app, UIA3Automation automation, UiWindow principal)
        {
            try
            {
                IrMenuLateralYEsperar(app, automation, principal, "Ventas");
                var ayuda = principal.FindFirstDescendant(automation.ConditionFactory.ByAutomationId("btnAyudaAtajos"));
                if (ayuda == null)
                {
                    Registrar("ℹ️ UI-FLOW-AYUDA: No hay botón btnAyudaAtajos (no es facturación POS o build distinta).");
                    return;
                }
                ayuda.AsButton().Invoke();
                app.WaitWhileBusy(TimeSpan.FromSeconds(3));
                Thread.Sleep(500);
                UiWindow winAyuda = Retry.WhileNull(
                    () => app.GetAllTopLevelWindows(automation).FirstOrDefault(w =>
                        w != null && (w.Title ?? "").IndexOf("Atajo", StringComparison.OrdinalIgnoreCase) >= 0),
                    TimeSpan.FromMilliseconds(300),
                    TimeSpan.FromSeconds(12)).Result;
                if (winAyuda == null)
                {
                    Registrar("❌ UI-FLOW-AYUDA: No apareció la ventana de atajos.");
                    return;
                }
                var cerrar = winAyuda.FindFirstDescendant(automation.ConditionFactory.ByControlType(ControlType.Button)
                    .And(automation.ConditionFactory.ByName("Cerrar")));
                if (cerrar != null)
                    cerrar.AsButton().Invoke();
                else
                    winAyuda.Close();
                app.WaitWhileBusy(TimeSpan.FromSeconds(2));
                Thread.Sleep(300);
                Registrar("✅ UI-FLOW-AYUDA: Ventana «" + (winAyuda.Title ?? "") + "» cerrada.");
            }
            catch (Exception ex)
            {
                Registrar("⚠️ UI-FLOW-AYUDA: " + ex.Message);
            }
        }

        static string ObtenerCodigoProductoParaUiFlow()
        {
            string cfg = ConfigurationManager.AppSettings["UITest_CodigoProducto"];
            if (!string.IsNullOrWhiteSpace(cfg))
                return cfg.Trim();
            try
            {
                DataTable dt = DatabaseService.GetProductos("");
                if (dt.Rows.Count == 0) return null;
                foreach (DataRow row in dt.Rows)
                {
                    string cod = row["Codigo"] != DBNull.Value ? row["Codigo"].ToString() : "";
                    if (!string.IsNullOrWhiteSpace(cod)) return cod;
                }
            }
            catch { }
            return null;
        }

        static void FlujoPosPresupuestoAbrirCobroYCancelar(Application app, UIA3Automation automation, UiWindow principal)
        {
            try
            {
                IrMenuLateralYEsperar(app, automation, principal, "Ventas");
                if (principal.FindFirstDescendant(automation.ConditionFactory.ByAutomationId("btnGuardarFactura")) == null)
                {
                    Registrar("ℹ️ UI-FLOW-POS: Sin facturación POS en esta vista; omito cobro simulado.");
                    return;
                }

                string codigo = ObtenerCodigoProductoParaUiFlow();
                if (string.IsNullOrEmpty(codigo))
                {
                    Registrar("⚠️ UI-FLOW-POS: No hay producto en catálogo ni UITest_CodigoProducto en App.config.");
                    return;
                }

                var tab = principal.FindFirstDescendant(automation.ConditionFactory.ByControlType(ControlType.Tab));
                if (tab != null)
                {
                    try
                    {
                        tab.AsTab().SelectTabItem(0);
                        app.WaitWhileBusy(TimeSpan.FromSeconds(2));
                        Thread.Sleep(350);
                    }
                    catch { /* ya en primera pestaña */ }
                }

                var cmbTipo = principal.FindFirstDescendant(automation.ConditionFactory.ByAutomationId("cmbTipoComprobante"));
                if (cmbTipo != null)
                {
                    try
                    {
                        cmbTipo.AsComboBox().Select(4);
                        app.WaitWhileBusy(TimeSpan.FromSeconds(1));
                        Thread.Sleep(200);
                    }
                    catch
                    {
                        Registrar("⚠️ UI-FLOW-POS: No se pudo seleccionar «Presupuesto» en cmbTipoComprobante (índice 4).");
                    }
                }

                var txtProd = principal.FindFirstDescendant(automation.ConditionFactory.ByAutomationId("txtBuscarProducto"));
                if (txtProd == null)
                {
                    Registrar("❌ UI-FLOW-POS: No se encontró txtBuscarProducto.");
                    return;
                }
                txtProd.AsTextBox().Enter(codigo);
                txtProd.Focus();
                app.WaitWhileBusy(TimeSpan.FromSeconds(2));
                Thread.Sleep(700);

                var lista = principal.FindFirstDescendant(automation.ConditionFactory.ByAutomationId("lstSugerenciasProducto"));
                if (lista != null)
                {
                    var item = lista.FindFirstDescendant(automation.ConditionFactory.ByControlType(ControlType.ListItem));
                    if (item != null)
                        item.Click();
                    else
                        Keyboard.Press(VirtualKeyShort.DOWN);
                }
                else
                    Keyboard.Press(VirtualKeyShort.DOWN);

                Thread.Sleep(400);
                app.WaitWhileBusy(TimeSpan.FromSeconds(2));

                var btnAdd = principal.FindFirstDescendant(automation.ConditionFactory.ByAutomationId("btnAgregarProducto"));
                if (btnAdd == null)
                {
                    Registrar("❌ UI-FLOW-POS: No se encontró btnAgregarProducto.");
                    return;
                }
                btnAdd.AsButton().Invoke();
                app.WaitWhileBusy(TimeSpan.FromSeconds(2));
                Thread.Sleep(400);

                var cobrar = principal.FindFirstDescendant(automation.ConditionFactory.ByAutomationId("btnGuardarFactura"));
                if (cobrar == null)
                {
                    Registrar("❌ UI-FLOW-POS: No se encontró btnGuardarFactura.");
                    return;
                }
                cobrar.AsButton().Invoke();
                app.WaitWhileBusy(TimeSpan.FromSeconds(3));
                Thread.Sleep(600);

                UiWindow winCobro = Retry.WhileNull(
                    () => app.GetAllTopLevelWindows(automation).FirstOrDefault(w =>
                        w != null && (w.Title ?? "").IndexOf("Cobro", StringComparison.OrdinalIgnoreCase) >= 0),
                    TimeSpan.FromMilliseconds(300),
                    TimeSpan.FromSeconds(18)).Result;

                if (winCobro == null)
                {
                    CerrarModalesInformativosSiHay(app, automation, principal);
                    Registrar("⚠️ UI-FLOW-POS: No apareció ventana «Cobro» (validación AFIP/tipo o mensaje bloqueante). Revisar capturas y CustomMessageBox.");
                    return;
                }

                try
                {
                    winCobro.CaptureToFile(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Reporte_QA_UI_flow_cobro_modal.png"));
                    Registrar("ℹ️ UI-FLOW-POS: Captura modal cobro → Reporte_QA_UI_flow_cobro_modal.png");
                }
                catch { }

                var cancelCobro = winCobro.FindFirstDescendant(automation.ConditionFactory.ByControlType(ControlType.Button)
                    .And(automation.ConditionFactory.ByName("Cancelar")));
                if (cancelCobro == null)
                {
                    Registrar("❌ UI-FLOW-POS: Modal Cobro sin botón Cancelar en UIA.");
                    winCobro.Close();
                    return;
                }
                cancelCobro.AsButton().Invoke();
                app.WaitWhileBusy(TimeSpan.FromSeconds(3));
                Thread.Sleep(400);
                CerrarModalesInformativosSiHay(app, automation, principal);
                Registrar("✅ UI-FLOW-POS: Presupuesto → línea en carrito → COBRAR → modal → Cancelar (sin persistir venta). Producto probado: \"" + codigo + "\".");
            }
            catch (Exception ex)
            {
                Registrar("⚠️ UI-FLOW-POS: " + ex.Message);
                CerrarModalesInformativosSiHay(app, automation, principal);
            }
        }
    }
}
