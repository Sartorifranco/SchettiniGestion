using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Input;
using FlaUI.Core.Definitions;
using FlaUI.Core.Tools;
using FlaUI.Core.WindowsAPI;
using FlaUI.UIA3;
using UiWindow = FlaUI.Core.AutomationElements.Window;

namespace SchettiniGestion.Tester
{
    partial class Program
    {
        /// <summary>
        /// Lanza la WPF, recorre login y menú principal comprobando textos visibles y controles (FlaUI / UIA3).
        /// </summary>
        static void EjecutarPruebasFrontendFlaUI()
        {
            Registrar("");
            Registrar("--- FRONTEND (FlaUI / UIA3) ---");

            if (string.Equals(Environment.GetEnvironmentVariable("SCHPOS_SKIP_UI"), "1", StringComparison.Ordinal))
            {
                Registrar("ℹ️ UI: Omitido (variable de entorno SCHPOS_SKIP_UI=1).");
                return;
            }

            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string[] candidatosExe =
            {
                Path.GetFullPath(Path.Combine(baseDir, @"..\..\..\SchettiniGestion.WPF\bin\Debug\SchettiniGestion.WPF.exe")),
                Path.GetFullPath(Path.Combine(baseDir, @"..\..\..\SchettiniGestion.WPF\bin\x86\Debug\SchettiniGestion.WPF.exe")),
                Path.GetFullPath(Path.Combine(baseDir, @"..\..\..\SchettiniGestion.WPF\bin\x64\Debug\SchettiniGestion.WPF.exe"))
            };
            string exeWpf = candidatosExe.FirstOrDefault(File.Exists);
            if (string.IsNullOrEmpty(exeWpf))
            {
                Registrar("⚠️ UI: No se encontró SchettiniGestion.WPF.exe (Debug). Rutas probadas:");
                foreach (var c in candidatosExe)
                    Registrar("   · " + c);
                Registrar("   Compilá el proyecto WPF (p. ej. plataforma x86 Debug) y volvé a ejecutar el tester.");
                return;
            }

            string usuario = ConfigurationManager.AppSettings["UITest_LoginUsuario"] ?? "admin";
            string password = ConfigurationManager.AppSettings["UITest_LoginPassword"]
                               ?? Environment.GetEnvironmentVariable("SCHPOS_UI_PASSWORD")
                               ?? "123456";

            Application app = null;
            try
            {
                if (!string.Equals(Environment.GetEnvironmentVariable("SCHPOS_UI_NO_KILL"), "1", StringComparison.Ordinal))
                {
                    foreach (var proc in Process.GetProcessesByName("SchettiniGestion.WPF"))
                    {
                        try { proc.Kill(); } catch { }
                    }
                    Thread.Sleep(400);
                }

                using (var automation = new UIA3Automation())
                {
                    app = Application.Launch(new ProcessStartInfo
                    {
                        FileName = exeWpf,
                        WorkingDirectory = Path.GetDirectoryName(exeWpf) ?? ""
                    });
                    app.WaitWhileBusy(TimeSpan.FromSeconds(25));
                    Thread.Sleep(3000);

                    UiWindow login = Retry.WhileNull(
                        () => app.GetAllTopLevelWindows(automation).FirstOrDefault(w => EsVentanaLogin(w, automation)),
                        TimeSpan.FromMilliseconds(500),
                        TimeSpan.FromSeconds(120)).Result;

                    if (login == null)
                    {
                        foreach (var w in app.GetAllTopLevelWindows(automation))
                        {
                            if (w == null) continue;
                            if (EsVentanaLogin(w, automation))
                            {
                                login = w;
                                break;
                            }
                        }
                    }

                    if (login == null)
                    {
                        Registrar("❌ UI: No apareció ventana de login. Ventanas:");
                        foreach (var w in app.GetAllTopLevelWindows(automation))
                            if (w != null)
                                Registrar("   · \"" + (w.Title ?? "") + "\"");
                        return;
                    }

                    Registrar("✅ UI: Ventana de credenciales — Título: \"" + (login.Title ?? "") + "\".");

                    Retry.WhileFalse(
                        () =>
                        {
                            var ed = login.FindAllDescendants(automation.ConditionFactory.ByControlType(ControlType.Edit));
                            return ed != null && ed.Length >= 1;
                        },
                        TimeSpan.FromMilliseconds(400),
                        TimeSpan.FromSeconds(20),
                        throwOnTimeout: false);
                    Thread.Sleep(600);

                    RegistrarAuditoriaSuperficie(automation, login, "LOGIN (antes de credenciales)",
                        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Reporte_QA_UI_login_pre.png"));

                    string rutaShotLogin = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Reporte_QA_UI_login.png");
                    try
                    {
                        login.CaptureToFile(rutaShotLogin);
                        Registrar("ℹ️ UI: Captura login guardada en " + rutaShotLogin);
                    }
                    catch (Exception exCap0)
                    {
                        Registrar("ℹ️ UI: No se pudo capturar login inicial: " + exCap0.Message);
                    }

                    var elUsuario = login.FindFirstDescendant(automation.ConditionFactory.ByAutomationId("UITest_Usuario"));
                    if (elUsuario != null)
                    {
                        EscribirEnCampoTextoSeguro(elUsuario, usuario);
                        var elPass = login.FindFirstDescendant(automation.ConditionFactory.ByAutomationId("UITest_Password"));
                        if (elPass != null)
                        {
                            elPass.Focus();
                            Thread.Sleep(120);
                            Keyboard.Type(password);
                            Registrar("✅ UI: Credenciales ingresadas (AutomationId usuario + contraseña).");
                        }
                        else
                        {
                            elUsuario.Focus();
                            Keyboard.Press(VirtualKeyShort.TAB);
                            Keyboard.Release(VirtualKeyShort.TAB);
                            Thread.Sleep(120);
                            Keyboard.Type(password);
                            Registrar("✅ UI: Credenciales ingresadas (AutomationId usuario + TAB + teclado contraseña).");
                        }
                    }
                    else
                    {
                        var edits = login.FindAllDescendants(automation.ConditionFactory.ByControlType(ControlType.Edit));
                        if (edits == null || edits.Length < 1)
                        {
                            Registrar("❌ UI: No hay campo Edit de usuario en login.");
                            return;
                        }
                        if (edits.Length >= 2)
                        {
                            EscribirEnCampoTextoSeguro(edits[0], usuario);
                            edits[1].Focus();
                            Thread.Sleep(120);
                            Keyboard.Type(password);
                            Registrar("✅ UI: Credenciales ingresadas (dos campos Edit detectados).");
                        }
                        else
                        {
                            EscribirEnCampoTextoSeguro(edits[0], usuario);
                            edits[0].Focus();
                            Keyboard.Press(VirtualKeyShort.TAB);
                            Keyboard.Release(VirtualKeyShort.TAB);
                            Thread.Sleep(120);
                            Keyboard.Type(password);
                            Registrar("✅ UI: Credenciales ingresadas (un Edit + TAB + teclado).");
                        }
                    }

                    var btnLogin = login.FindFirstDescendant(automation.ConditionFactory.ByAutomationId("UITest_Ingresar"));
                    if (btnLogin == null)
                        btnLogin = login.FindFirstDescendant(automation.ConditionFactory.ByControlType(ControlType.Button)
                            .And(automation.ConditionFactory.ByName("INGRESAR ➔")));
                    if (btnLogin == null)
                        btnLogin = login.FindAllDescendants(automation.ConditionFactory.ByControlType(ControlType.Button))
                            .FirstOrDefault(b => (b.Name ?? "").IndexOf("INGRESAR", StringComparison.OrdinalIgnoreCase) >= 0);
                    if (btnLogin == null)
                    {
                        Registrar("❌ UI: No se encontró el botón Ingresar.");
                        return;
                    }
                    btnLogin.AsButton().Invoke();
                    app.WaitWhileBusy(TimeSpan.FromSeconds(8));
                    Thread.Sleep(2000);

                    UiWindow principal = Retry.WhileNull(
                        () => BuscarVentanaPrincipal(app, automation),
                        TimeSpan.FromMilliseconds(500),
                        TimeSpan.FromSeconds(60)).Result;

                    if (principal == null)
                    {
                        Registrar("❌ UI: No apareció la ventana principal tras el login. Ventanas del proceso:");
                        foreach (var w in app.GetAllTopLevelWindows(automation))
                            if (w != null)
                                Registrar("   · \"" + (w.Title ?? "") + "\"");
                        return;
                    }

                    Registrar("✅ UI: Ventana principal — \"" + principal.Title + "\".");

                    CerrarModalesInformativosSiHay(app, automation, principal);

                    Registrar("ℹ️ UI: Cabecera — Combo «Pantalla» y botón «Teclado» no se alteran (evitar OSK y diálogos opcionales); solo se verifica presencia.");
                    var hayComboPantalla = principal.FindFirstDescendant(automation.ConditionFactory.ByAutomationId("cmbModoPantalla")) != null
                        || principal.FindFirstDescendant(automation.ConditionFactory.ByControlType(ControlType.ComboBox)) != null;
                    var btnTecladoUi = principal.FindFirstDescendant(automation.ConditionFactory.ByAutomationId("btnTeclado"));
                    var hayTeclado = btnTecladoUi != null || principal.FindAllDescendants(automation.ConditionFactory.ByControlType(ControlType.Button))
                        .Any(b => (b.Name ?? "").IndexOf("Teclado", StringComparison.OrdinalIgnoreCase) >= 0);
                    Registrar(hayComboPantalla ? "✅ UI: Combo de modo de pantalla visible en UIA." : "⚠️ UI: No se detectó ComboBox de modo de pantalla en UIA.");
                    Registrar(hayTeclado ? "✅ UI: Botón Teclado localizado (AutomationId o nombre; no se invoca OSK)." : "⚠️ UI: Botón Teclado no encontrado en UIA.");

                    RegistrarAuditoriaSuperficie(automation, principal, "SHELL — Tras login (Inicio por defecto)",
                        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Reporte_QA_UI_00_shell_inicial.png"));

                    string[] rutasMenu =
                    {
                        "Inicio", "Ventas", "Productos", "Clientes", "Caja", "Usuarios", "Configuración"
                    };
                    foreach (var p in rutasMenu)
                        ProcesarEntradaMenuLateral(app, automation, principal, p);

                    EjecutarFlujosNegocioProfundos(app, automation, principal);

                    try
                    {
                        var btnTema = principal.FindFirstDescendant(automation.ConditionFactory.ByAutomationId("btnTema"));
                        if (btnTema != null)
                        {
                            btnTema.AsButton().Invoke();
                            Thread.Sleep(350);
                            btnTema.AsButton().Invoke();
                            Registrar("✅ UI: Botón de tema pulsado dos veces (claro/oscuro) sin cierre inesperado.");
                        }
                        else
                            Registrar("ℹ️ UI: Botón btnTema no localizado por AutomationId.");
                    }
                    catch (Exception exT)
                    {
                        Registrar("⚠️ UI: Tema — " + exT.Message);
                    }

                    CerrarModalesInformativosSiHay(app, automation, principal);
                    RegistrarAuditoriaSuperficie(automation, principal, "SHELL — Tras recorrido de módulos y tema",
                        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Reporte_QA_UI_principal.png"));

                    EmitirDeclaracionCoberturaUi();

                    var btnCerrar = principal.FindFirstDescendant(automation.ConditionFactory.ByAutomationId("btnCerrarSesion"));
                    if (btnCerrar != null)
                    {
                        btnCerrar.AsButton().Invoke();
                        app.WaitWhileBusy(TimeSpan.FromSeconds(4));
                        Thread.Sleep(500);
                        Registrar("✅ UI: Cerrar sesión ejecutado.");
                    }
                    else
                        Registrar("⚠️ UI: No se encontró btnCerrarSesion.");

                    UiWindow login2 = app.GetAllTopLevelWindows(automation)
                        .FirstOrDefault(w => w != null && w.Title != null && w.Title.IndexOf("Iniciar", StringComparison.OrdinalIgnoreCase) >= 0);
                    if (login2 != null)
                    {
                        var salir = login2.FindAllDescendants(automation.ConditionFactory.ByControlType(ControlType.Button))
                            .FirstOrDefault(b => (b.Name ?? "").IndexOf("Salir", StringComparison.OrdinalIgnoreCase) >= 0);
                        if (salir != null)
                        {
                            salir.AsButton().Invoke();
                            app.WaitWhileBusy(TimeSpan.FromSeconds(3));
                        }
                    }

                    Registrar("✅ UI: Flujo frontend completado.");
                }
            }
            catch (Exception ex)
            {
                Registrar("❌ UI: " + ex.Message);
            }
            finally
            {
                try
                {
                    if (app != null)
                    {
                        app.Close();
                        app.Dispose();
                    }
                }
                catch
                {
                    try
                    {
                        foreach (var p in Process.GetProcessesByName("SchettiniGestion.WPF"))
                            try { p.Kill(); } catch { }
                    }
                    catch { }
                }
            }
        }

        /// <summary>
        /// Ventana principal (shell): título Schettini/Gestión o controles típicos del shell (no es login).
        /// </summary>
        static UiWindow BuscarVentanaPrincipal(Application app, UIA3Automation automation)
        {
            foreach (var w in app.GetAllTopLevelWindows(automation))
            {
                if (w == null || EsVentanaLogin(w, automation)) continue;
                string t = w.Title ?? "";
                if (t.IndexOf("Schettini", StringComparison.OrdinalIgnoreCase) >= 0) return w;
                if (t.IndexOf("Gestión", StringComparison.OrdinalIgnoreCase) >= 0
                    || t.IndexOf("Gestion", StringComparison.OrdinalIgnoreCase) >= 0) return w;
                try
                {
                    if (w.FindFirstDescendant(automation.ConditionFactory.ByAutomationId("btnCerrarSesion")) != null)
                        return w;
                    if (w.FindFirstDescendant(automation.ConditionFactory.ByAutomationId("btnTema")) != null)
                        return w;
                }
                catch
                {
                    /* UIA */
                }
            }
            return null;
        }

        /// <summary>
        /// True si es la ventana shell principal (no login ni modal).
        /// </summary>
        static bool EsVentanaPrincipalShell(UiWindow w, UIA3Automation automation)
        {
            if (w == null || EsVentanaLogin(w, automation)) return false;
            string t = w.Title ?? "";
            if (t.IndexOf("Schettini", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (t.IndexOf("Gestión", StringComparison.OrdinalIgnoreCase) >= 0
                || t.IndexOf("Gestion", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            try
            {
                if (w.FindFirstDescendant(automation.ConditionFactory.ByAutomationId("btnCerrarSesion")) != null)
                    return true;
                if (w.FindFirstDescendant(automation.ConditionFactory.ByAutomationId("btnTema")) != null)
                    return true;
            }
            catch { }
            return false;
        }

        /// <summary>Login: título, AutomationId UITest_* o botón INGRESAR.</summary>
        static bool EsVentanaLogin(UiWindow w, UIA3Automation automation)
        {
            if (w == null) return false;
            string t = w.Title ?? "";
            if (t.IndexOf("Iniciar", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (t.IndexOf("Sesi", StringComparison.OrdinalIgnoreCase) >= 0 && t.Length < 80) return true;
            try
            {
                if (w.FindFirstDescendant(automation.ConditionFactory.ByAutomationId("UITest_Usuario")) != null)
                    return true;
                var btns = w.FindAllDescendants(automation.ConditionFactory.ByControlType(ControlType.Button));
                if (btns != null)
                {
                    foreach (var b in btns)
                    {
                        string n = b.Name ?? "";
                        if (n.IndexOf("INGRESAR", StringComparison.OrdinalIgnoreCase) >= 0)
                            return true;
                    }
                }
            }
            catch
            {
                /* UIA aún no lista descendientes */
            }
            return false;
        }

        static void CerrarModalesInformativosSiHay(Application app, UIA3Automation automation, UiWindow principal)
        {
            foreach (var w in app.GetAllTopLevelWindows(automation))
            {
                if (w == null || EsVentanaLogin(w, automation)) continue;
                if (EsVentanaPrincipalShell(w, automation)) continue;
                try
                {
                    foreach (var nombre in new[] { "Aceptar", "OK", "Entendido" })
                    {
                        var b = w.FindFirstDescendant(automation.ConditionFactory.ByControlType(ControlType.Button)
                            .And(automation.ConditionFactory.ByName(nombre)));
                        if (b != null)
                        {
                            b.AsButton().Invoke();
                            app.WaitWhileBusy(TimeSpan.FromSeconds(2));
                            Thread.Sleep(250);
                            break;
                        }
                    }
                }
                catch { /* ventana ya cerrada */ }
            }
        }

        static string SanitizarNombreArchivo(string slug)
        {
            if (string.IsNullOrEmpty(slug)) return "modulo";
            foreach (char c in Path.GetInvalidFileNameChars())
                slug = slug.Replace(c, '_');
            slug = slug.Replace(" ", "_");
            return slug.Length > 48 ? slug.Substring(0, 48) : slug;
        }

        static double AreaBounding(AutomationElement el)
        {
            try
            {
                var r = el.BoundingRectangle;
                if (r.Width <= 0 || r.Height <= 0) return 0;
                return r.Width * r.Height;
            }
            catch { return 0; }
        }

        sealed class AuditoriaSuperficieResult
        {
            public int BotonesConEtiqueta;
            public int BotonesVisiblesSinEtiqueta;
            public int TextosConNombre;
            public int TextosAmpliosSinNombre;
            public int TabItemsSinNombre;
            public int EditsVisiblesSinNombre;
            public readonly List<string> Hallazgos = new List<string>();
        }

        static AuditoriaSuperficieResult AuditarSuperficie(UIA3Automation automation, AutomationElement root)
        {
            var r = new AuditoriaSuperficieResult();
            foreach (var el in root.FindAllDescendants())
            {
                try
                {
                    bool off = el.Properties.IsOffscreen.ValueOrDefault;
                    var ct = el.Properties.ControlType.ValueOrDefault;
                    string name = el.Properties.Name.ValueOrDefault ?? "";
                    string help = el.Properties.HelpText.ValueOrDefault ?? "";
                    string aid = el.Properties.AutomationId.ValueOrDefault ?? "";
                    double area = AreaBounding(el);

                    if (ct == ControlType.Button && !off && area >= 400)
                    {
                        if (!string.IsNullOrWhiteSpace(name) || !string.IsNullOrWhiteSpace(aid) || !string.IsNullOrWhiteSpace(help))
                            r.BotonesConEtiqueta++;
                        else
                        {
                            r.BotonesVisiblesSinEtiqueta++;
                            if (r.Hallazgos.Count < 35)
                                r.Hallazgos.Add("Botón visible (~" + (int)area + " px²) sin Name / HelpText / AutomationId.");
                        }
                    }
                    else if (ct == ControlType.Text)
                    {
                        if (!string.IsNullOrWhiteSpace(name))
                            r.TextosConNombre++;
                        else if (!off && area >= 2200 && string.IsNullOrWhiteSpace(help))
                        {
                            r.TextosAmpliosSinNombre++;
                            if (r.Hallazgos.Count < 35)
                                r.Hallazgos.Add("Nodo Text en pantalla con área amplia y sin Name UIA (~" + (int)area + " px²); revisar si el texto real está solo en plantilla/imagen.");
                        }
                    }
                    else if (ct == ControlType.TabItem && !off)
                    {
                        if (string.IsNullOrWhiteSpace(name))
                        {
                            r.TabItemsSinNombre++;
                            if (r.Hallazgos.Count < 35)
                                r.Hallazgos.Add("TabItem sin texto accesible en UIA.");
                        }
                    }
                    else if (ct == ControlType.Edit && !off && area >= 500)
                    {
                        if (string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(aid))
                        {
                            r.EditsVisiblesSinNombre++;
                            if (r.Hallazgos.Count < 35)
                                r.Hallazgos.Add("Campo Edit visible sin Name/AutomationId (placeholder puede no exponerse a UIA).");
                        }
                    }
                }
                catch
                {
                    /* nodos UIA incompletos */
                }
            }
            return r;
        }

        static void RegistrarAuditoriaSuperficie(UIA3Automation automation, AutomationElement root, string etiqueta, string rutaCapturaOpcional)
        {
            var res = AuditarSuperficie(automation, root);
            Registrar("ℹ️ UI: [" + etiqueta + "] Botones con etiqueta UIA: " + res.BotonesConEtiqueta
                      + "; botones visibles sin etiqueta: " + res.BotonesVisiblesSinEtiqueta
                      + ". Text UIA con nombre: " + res.TextosConNombre
                      + "; Text amplio sin nombre: " + res.TextosAmpliosSinNombre
                      + ". TabItem sin nombre: " + res.TabItemsSinNombre
                      + ". Edit visible sin nombre/Id: " + res.EditsVisiblesSinNombre + ".");

            foreach (var h in res.Hallazgos)
                Registrar("⚠️ UI: [" + etiqueta + "] " + h);

            if (res.TextosAmpliosSinNombre > 0 || res.TabItemsSinNombre > 0)
                Registrar("❌ UI: [" + etiqueta + "] Hallazgos críticos de texto/pestañas (posible UI ilegible o no accesible).");
            else if (res.BotonesVisiblesSinEtiqueta > 8)
                Registrar("⚠️ UI: [" + etiqueta + "] Muchos botones sin etiqueta UIA (revisar íconos y AutomationProperties).");

            if (!string.IsNullOrEmpty(rutaCapturaOpcional))
            {
                try
                {
                    root.CaptureToFile(rutaCapturaOpcional);
                    Registrar("ℹ️ UI: Captura [" + etiqueta + "] → " + rutaCapturaOpcional);
                }
                catch (Exception ex)
                {
                    Registrar("⚠️ UI: No se pudo capturar [" + etiqueta + "]: " + ex.Message);
                }
            }
        }

        static void RecorrerPestañasInternasYAuditar(Application app, UIA3Automation automation, UiWindow principal, string slugMenu)
        {
            bool recorrido = false;
            foreach (var tabEl in principal.FindAllDescendants(automation.ConditionFactory.ByControlType(ControlType.Tab)))
            {
                try
                {
                    var tab = tabEl.AsTab();
                    if (tab.TabItems == null || tab.TabItems.Length < 2)
                        continue;
                    recorrido = true;
                    for (int i = 0; i < tab.TabItems.Length; i++)
                    {
                        string label = tab.TabItems[i].Name ?? ("#" + i);
                        try
                        {
                            tab.SelectTabItem(i);
                        }
                        catch
                        {
                            try { tab.TabItems[i].Click(); } catch { }
                        }
                        app.WaitWhileBusy(TimeSpan.FromSeconds(4));
                        Thread.Sleep(450);
                        CerrarModalesInformativosSiHay(app, automation, principal);
                        string png = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                            "Reporte_QA_UI_" + SanitizarNombreArchivo(slugMenu) + "_tab" + i + "_" + SanitizarNombreArchivo(label) + ".png");
                        RegistrarAuditoriaSuperficie(automation, principal, "MÓDULO " + slugMenu + " › pestaña \"" + label + "\"", png);
                        Registrar("✅ UI: Pestaña visitada — \"" + label + "\".");
                    }
                }
                catch
                {
                    /* no es un Tab estándar UIA */
                }
            }
            if (recorrido)
                return;

            var cands = principal.FindAllDescendants(automation.ConditionFactory.ByControlType(ControlType.TabItem))
                .Where(t => !t.Properties.IsOffscreen.ValueOrDefault && AreaBounding(t) > 50)
                .ToList();
            if (cands.Count < 2) return;
            double y0 = cands.Average(t => t.BoundingRectangle.Top);
            var franja = cands.Where(t => Math.Abs(t.BoundingRectangle.Top - y0) < 22)
                .OrderBy(t => t.BoundingRectangle.Left)
                .ToList();
            if (franja.Count < 2) return;
            for (int i = 0; i < franja.Count; i++)
            {
                string label = franja[i].Name ?? ("#" + i);
                try { franja[i].Click(); } catch { }
                app.WaitWhileBusy(TimeSpan.FromSeconds(4));
                Thread.Sleep(450);
                CerrarModalesInformativosSiHay(app, automation, principal);
                string png = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                    "Reporte_QA_UI_" + SanitizarNombreArchivo(slugMenu) + "_tabstrip" + i + ".png");
                RegistrarAuditoriaSuperficie(automation, principal, "MÓDULO " + slugMenu + " › cabecera pestaña \"" + label + "\" (heurística)", png);
                Registrar("✅ UI: Pestaña (franja) — \"" + label + "\".");
            }
        }

        static void ProcesarEntradaMenuLateral(Application app, UIA3Automation automation, UiWindow principal, string patron)
        {
            var btn = BuscarBotonMenu(principal, automation, patron);
            if (btn == null)
            {
                Registrar("⚠️ UI: No se encontró botón de menú que contenga \"" + patron + "\" (permisos, licencia o UI distinta).");
                return;
            }
            string etiqueta = btn.Name ?? "(sin nombre UIA)";
            if (string.IsNullOrWhiteSpace(etiqueta) || etiqueta.Length < 2)
                Registrar("❌ UI: Botón de menú con nombre UIA vacío o inválido para patrón \"" + patron + "\".");
            else
                Registrar("✅ UI: Menú — \"" + etiqueta.Trim() + "\".");

            try
            {
                btn.AsButton().Invoke();
                app.WaitWhileBusy(TimeSpan.FromSeconds(5));
                Thread.Sleep(600);
            }
            catch (Exception exBtn)
            {
                Registrar("❌ UI: Error al pulsar menú \"" + patron + "\": " + exBtn.Message);
                return;
            }

            CerrarModalesInformativosSiHay(app, automation, principal);
            string slug = SanitizarNombreArchivo(patron);
            string pngModulo = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Reporte_QA_UI_modulo_" + slug + ".png");
            RegistrarAuditoriaSuperficie(automation, principal, "MÓDULO lateral «" + patron + "» (vista base)", pngModulo);
            RecorrerPestañasInternasYAuditar(app, automation, principal, patron);
        }

        static void EmitirDeclaracionCoberturaUi()
        {
            Registrar("");
            Registrar("--- COBERTURA UI (qué quedó probado y qué no) ---");
            Registrar("✅ UI (automatizado): login (auditoría + captura previa a credenciales); shell tras login; cada ítem visible del menú lateral;");
            Registrar("   auditoría UIA por vista; capturas PNG por módulo y por cada pestaña interna (Ventas/Facturación, Usuarios, Configuración); tema; cierre de sesión.");
            Registrar("✅ UI (heurísticas): botones visibles sin Name/HelpText/AutomationId; Text UIA de gran superficie sin nombre;");
            Registrar("   TabItem sin nombre; Edit sin nombre (posible texto «invisible» para asistencias / pruebas automáticas).");
            Registrar("ℹ️ UI (no ejecutado a propósito): «Salir del sistema» (confirmación destructiva); apertura del teclado en pantalla;");
            Registrar("   cambio de modo «dos monitores» en el Combo (puede mostrar MessageBox); ventanas modales de negocio no abiertas salvo OK genérico.");
            Registrar("ℹ️ UI (límite de la tecnología UIA): contenido dibujado fuera de controles (DirectX), HTML embebido, filas vacías de DataGrid hasta cargar datos,");
            Registrar("   y textos compuestos solo con Run pueden no reflejarse como un solo nodo Text con Name.");
            Registrar("ℹ️ UI (recomendación humana): revisión visual de las capturas Reporte_QA_UI_*.png y recorrido manual de flujos de cobro, NC, impresión y cierre de caja.");
            Registrar("ℹ️ UI (paso 2 — flujos): bloque «UI — FLUJOS DE NEGOCIO» en este reporte; omitible con SCHPOS_SKIP_UI_FLOWS=1; producto POS configurable con UITest_CodigoProducto en App.config.");
        }

        static AutomationElement BuscarBotonMenu(UiWindow principal, UIA3Automation automation, string patron)
        {
            var botones = principal.FindAllDescendants(automation.ConditionFactory.ByControlType(ControlType.Button));
            if (botones == null) return null;
            return botones.FirstOrDefault(b =>
            {
                string n = b.Name ?? "";
                return n.IndexOf(patron, StringComparison.OrdinalIgnoreCase) >= 0;
            });
        }

        /// <summary>
        /// Evita InvalidOperationException cuando el patrón Text de UIA aún no está listo para Enter de FlaUI.
        /// </summary>
        static void EscribirEnCampoTextoSeguro(AutomationElement el, string texto)
        {
            if (el == null || texto == null) return;
            try
            {
                el.Focus();
                Thread.Sleep(80);
                el.AsTextBox().Enter(texto);
            }
            catch
            {
                try
                {
                    el.Focus();
                    el.Click();
                }
                catch { /* */ }
                Thread.Sleep(150);
                Keyboard.Type(texto);
            }
        }
    }
}
