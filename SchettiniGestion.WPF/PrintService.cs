using Newtonsoft.Json;
using QRCoder;
using SchettiniGestion;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using ZXing;
using ZXing.Common;
using WinDrawing = System.Drawing;
using WinPrinting = System.Drawing.Printing;

namespace SchettiniGestion.WPF
{
    public static class PrintService
    {
        private static bool USAR_MOTOR_GRAFICO_PARA_TICKETS = true;

        #region MÉTODOS PÚBLICOS
        public static void ImprimirPresupuesto(int presupuestoID)
        {
            try
            {
                DataRow cabecera = DatabaseService.GetPresupuestoPorID(presupuestoID);
                if (cabecera == null) { MessageBox.Show("Error: No se encontró el presupuesto."); return; }
                DataTable items = DatabaseService.GetPresupuestoDetalle(presupuestoID);
                GenerarDocumentoA4ConItems("PRESUPUESTO", "PresupuestoID", cabecera, items,
                    Convert.ToDecimal(cabecera["Total"]), "Documento no válido como factura fiscal.");
            }
            catch (Exception ex) { MessageBox.Show("Error crítico al imprimir: " + ex.Message); }
        }

        public static void ImprimirRemito(int remitoID)
        {
            try
            {
                DataRow cabecera = DatabaseService.GetRemitoPorID(remitoID);
                if (cabecera == null) { MessageBox.Show("Error: No se encontró el remito."); return; }
                DataTable items = DatabaseService.GetRemitoDetalle(remitoID);
                decimal total = items.Rows.Count > 0
                    ? items.AsEnumerable().Sum(r => Convert.ToDecimal(r["Subtotal"]))
                    : 0m;
                GenerarDocumentoA4ConItems("REMITO", "RemitoID", cabecera, items, total,
                    "Comprobante de entrega. No válido como factura fiscal.");
            }
            catch (Exception ex) { MessageBox.Show("Error crítico al imprimir: " + ex.Message); }
        }

        public static void ImprimirPedido(int pedidoID)
        {
            try
            {
                DataRow cabecera = DatabaseService.GetPedidoPorID(pedidoID);
                if (cabecera == null) { MessageBox.Show("Error: No se encontró el pedido."); return; }
                DataTable items = DatabaseService.GetPedidoDetalle(pedidoID);
                string extra = cabecera["FechaEntrega"] != DBNull.Value
                    ? $"Entrega prevista: {Convert.ToDateTime(cabecera["FechaEntrega"]):dd/MM/yyyy}"
                    : null;
                GenerarDocumentoA4ConItems("PEDIDO", "PedidoID", cabecera, items,
                    Convert.ToDecimal(cabecera["Total"]), "Pedido de venta. No válido como factura fiscal.", extra);
            }
            catch (Exception ex) { MessageBox.Show("Error crítico al imprimir: " + ex.Message); }
        }

        public static void ImprimirNotaCreditoDebitoVenta(int notaID)
        {
            try
            {
                DataRow cabecera = DatabaseService.GetNotaVentaPorID(notaID);
                if (cabecera == null) { MessageBox.Show("Error: No se encontró la nota."); return; }
                string tipo = cabecera["Tipo"]?.ToString() ?? "NC";
                string titulo = tipo == "ND" ? "NOTA DE DÉBITO" : "NOTA DE CRÉDITO";
                GenerarDocumentoA4Nota(titulo, cabecera);
            }
            catch (Exception ex) { MessageBox.Show("Error crítico al imprimir: " + ex.Message); }
        }

        public static void ImprimirTicketVenta(string tipo, int nro, string cli, DateTime fec, DataTable items, decimal tot, string cond, string cae = "", string vtoCae = "", string nombreVendedor = null, string clienteCuit = null, string urlQrFiscal = null)
        {
            string letra = "B";
            if (tipo != null)
            {
                if (tipo.IndexOf("Factura", StringComparison.OrdinalIgnoreCase) >= 0)
                    letra = ObtenerLetraFactura(!string.IsNullOrWhiteSpace(clienteCuit) ? clienteCuit : cli);
                else if (tipo.IndexOf("Ticket", StringComparison.OrdinalIgnoreCase) >= 0)
                    letra = "X";
                else if (tipo.Contains("A")) letra = "A";
                else if (tipo.Contains("B")) letra = "B";
                else if (tipo.Contains("C")) letra = "C";
            }

            string tit = tipo?.ToUpper() ?? "TICKET";
            string nroStr = nro > 0 ? nro.ToString("D8") : "(Pendiente)";

            // Armar Pie Fiscal
            string pie = "";
            // Si venía en cond (versión vieja) o en parámetro nuevo
            if (cond != null && cond.Contains("CAE:"))
            {
                string[] p = cond.Split(new[] { "CAE:" }, StringSplitOptions.None);
                cond = p[0].Trim();
                if (p.Length > 1) pie = "CAE: " + p[1].Trim();
            }
            else if (!string.IsNullOrEmpty(cae))
            {
                pie = $"CAE: {cae}\nVto CAE: {vtoCae}";
            }

            // QR fiscal ARCA obligatorio cuando hay CAE
            if (string.IsNullOrWhiteSpace(urlQrFiscal) && !string.IsNullOrWhiteSpace(cae))
            {
                DataRow conf = DatabaseService.GetConfiguracion();
                string cuitEmisor = conf?["CUIT"]?.ToString() ?? "";
                string condicionIva = conf != null && conf.Table.Columns.Contains("CondicionIVAEmpresa")
                    ? conf["CondicionIVAEmpresa"]?.ToString() ?? "" : "";
                int.TryParse(conf?["PuntoVenta"]?.ToString()?.Trim(), out int ptoVta);
                int tipoAfip = ArcaQrHelper.ResolverTipoComprobanteAfip(tipo, letra, clienteCuit, condicionIva);
                urlQrFiscal = ArcaQrHelper.ConstruirUrl(fec, cuitEmisor, ptoVta, tipoAfip, nro, tot, cae, clienteCuit);
            }

            if (USAR_MOTOR_GRAFICO_PARA_TICKETS)
                ImprimirTicketGrafico(tit, nroStr, cli, fec, items, tot, cond, letra, pie, nombreVendedor, urlQrFiscal);
            else
                MessageBox.Show("Motor A4 no activo.");
        }

        public static void ImprimirFactura(int facturaId)
        {
            EmitirComprobanteVenta(facturaId, null);
        }

        public static void EmitirComprobanteVenta(int facturaId, string destinoForzado = null)
        {
            try
            {
                DataRow cab = DatabaseService.GetFacturaPorID(facturaId);
                if (cab == null) { MessageBox.Show("No se encontró la factura."); return; }

                string tipo = cab["TipoComprobante"]?.ToString() ?? "Ticket";
                string destino = destinoForzado ?? DatabaseService.GetDestinoImpresionVenta();
                if (destino == "Preguntar")
                {
                    destino = PreguntarDestinoImpresion();
                    if (string.IsNullOrEmpty(destino)) return;
                }

                destino = ResolverDestinoEfectivo(destino, tipo);

                DataTable items = DatabaseService.GetFacturaDetalle(facturaId);
                int nro = cab["NumeroComprobanteAFIP"] != DBNull.Value && cab["NumeroComprobanteAFIP"] != null
                    ? Convert.ToInt32(cab["NumeroComprobanteAFIP"]) : facturaId;
                string cli = cab["ClienteNombre"]?.ToString() ?? "";
                DateTime fec = Convert.ToDateTime(cab["Fecha"]);
                decimal tot = Convert.ToDecimal(cab["Total"]);
                string cond = cab.Table.Columns.Contains("CondicionTicket") ? cab["CondicionTicket"]?.ToString() ?? "" : "";
                string cae = cab["CAE"]?.ToString() ?? "";
                string vto = cab["VencimientoCAE"]?.ToString() ?? "";
                string nombreVendedor = cab.Table.Columns.Contains("NombrePersonal")
                    ? cab["NombrePersonal"]?.ToString()?.Trim() ?? "" : "";
                string clienteCuit = cab.Table.Columns.Contains("ClienteCUIT")
                    ? cab["ClienteCUIT"]?.ToString()?.Trim() ?? "" : "";
                string letraTmp = tipo.Equals("Factura", StringComparison.OrdinalIgnoreCase)
                    ? ObtenerLetraFactura(clienteCuit) : "X";
                string urlQr = ArcaQrHelper.ConstruirUrlDesdeFactura(cab, letraTmp);

                switch (destino)
                {
                    case "Archivo":
                        GuardarComprobanteArchivo(facturaId, cab, items, tipo, nro, cli, fec, tot, cond, cae, vto, urlQr);
                        break;
                    case "A4":
                        string tituloA4 = tipo.Equals("Factura", StringComparison.OrdinalIgnoreCase) ? "FACTURA" : "TICKET";
                        string extraA4 = !string.IsNullOrEmpty(cae) ? $"CAE: {cae}  Vto: {vto}" : cond;
                        string pieA4 = tipo.Equals("Factura", StringComparison.OrdinalIgnoreCase)
                            ? "Comprobante fiscal."
                            : "Comprobante no válido como factura fiscal.";
                        GenerarDocumentoA4ConItems(tituloA4, "FacturaID", cab, items, tot, pieA4, extraA4, urlQr);
                        break;
                    default:
                        ImprimirTicketVenta(tipo, nro, cli, fec, items, tot, cond, cae, vto, nombreVendedor, clienteCuit, urlQr);
                        break;
                }
            }
            catch (Exception ex) { MessageBox.Show("Error al emitir comprobante: " + ex.Message); }
        }

        private static string ResolverDestinoEfectivo(string destino, string tipoComprobante)
        {
            if (destino == "A4")
            {
                if (!DatabaseService.TieneImpresoraA4Configurada())
                {
                    if (DatabaseService.TieneImpresoraTicketConfigurada())
                        return "Ticket";
                    return "Archivo";
                }
                return "A4";
            }

            if (destino == "Ticket")
            {
                if (!DatabaseService.TieneImpresoraTicketConfigurada())
                {
                    if (DatabaseService.TieneImpresoraA4Configurada())
                        return "A4";
                    return "Archivo";
                }
                return "Ticket";
            }

            if (destino == "Archivo")
                return "Archivo";

            if (tipoComprobante.Equals("Factura", StringComparison.OrdinalIgnoreCase))
                return DatabaseService.TieneImpresoraA4Configurada() ? "A4" : "Archivo";

            return ResolverDestinoEfectivo("Ticket", tipoComprobante);
        }

        public static string PreguntarDestinoImpresion()
        {
            string seleccion = null;
            var win = new Window
            {
                Title = "Emitir comprobante",
                Width = 540,
                SizeToContent = SizeToContent.Height,
                MinHeight = 420,
                MaxHeight = 620,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                ResizeMode = ResizeMode.NoResize,
                WindowStyle = WindowStyle.None,
                AllowsTransparency = true,
                Background = Brushes.Transparent
            };

            var panelBg = (Brush)Application.Current.TryFindResource("PanelBackgroundBrush")
                ?? new SolidColorBrush(Color.FromRgb(45, 45, 48));
            var borderBrush = (Brush)Application.Current.TryFindResource("BorderColor") ?? Brushes.Gray;
            var textPrimary = (Brush)Application.Current.TryFindResource("TextPrimary") ?? Brushes.White;
            var textSecondary = (Brush)Application.Current.TryFindResource("TextSecondary") ?? Brushes.LightGray;
            var primaryBtn = (Brush)Application.Current.TryFindResource("PrimaryColor") ?? new SolidColorBrush(Color.FromRgb(0, 122, 204));
            var surfaceBrush = (Brush)Application.Current.TryFindResource("SurfaceDark") ?? Brushes.DimGray;
            var hoverBrush = (Brush)Application.Current.TryFindResource("HoverBackground") ?? Brushes.LightGray;

            var root = new Border
            {
                Background = panelBg,
                CornerRadius = new CornerRadius(10),
                BorderBrush = borderBrush,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(24),
                Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    BlurRadius = 18,
                    ShadowDepth = 0,
                    Opacity = 0.45,
                    Color = Colors.Black
                }
            };

            var sp = new StackPanel();
            sp.Children.Add(new TextBlock
            {
                Text = "Emitir comprobante",
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Foreground = textPrimary,
                Margin = new Thickness(0, 0, 0, 8)
            });
            sp.Children.Add(new TextBlock
            {
                Text = "¿Cómo desea emitir el comprobante?",
                FontSize = 15,
                Foreground = textSecondary,
                TextWrapping = TextWrapping.Wrap,
                LineHeight = 22,
                MinHeight = 24,
                Padding = new Thickness(0, 2, 0, 4),
                Margin = new Thickness(0, 0, 0, 20)
            });

            Button CrearBoton(string texto, string valor, bool primario = false)
            {
                var btnBg = primario ? primaryBtn : surfaceBrush;
                var btn = new Button
                {
                    Content = texto,
                    MinHeight = 60,
                    FontSize = 16,
                    FontWeight = FontWeights.SemiBold,
                    Margin = new Thickness(0, 0, 0, 12),
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    Padding = new Thickness(16, 12, 16, 12),
                    Cursor = System.Windows.Input.Cursors.Hand,
                    Background = btnBg,
                    Foreground = primario ? Brushes.White : textPrimary,
                    BorderBrush = borderBrush,
                    BorderThickness = new Thickness(1)
                };
                if (!primario)
                {
                    btn.MouseEnter += (s, e) => btn.Background = hoverBrush;
                    btn.MouseLeave += (s, e) => btn.Background = surfaceBrush;
                }
                btn.Click += (s, e) => { seleccion = valor; win.DialogResult = true; };
                return btn;
            }

            sp.Children.Add(CrearBoton("🖨️  Impresora térmica (ticket)", "Ticket", true));
            sp.Children.Add(CrearBoton("📄  Impresora A4 (formato documento)", "A4"));
            sp.Children.Add(CrearBoton("💾  Guardar como PDF", "Archivo"));

            var btnCancel = new Button
            {
                Content = "No emitir",
                MinHeight = 52,
                FontSize = 15,
                Margin = new Thickness(0, 4, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Padding = new Thickness(16, 10, 16, 10),
                Cursor = System.Windows.Input.Cursors.Hand,
                Background = Brushes.Transparent,
                Foreground = textPrimary,
                BorderBrush = borderBrush,
                BorderThickness = new Thickness(1)
            };
            btnCancel.Click += (s, e) => { win.DialogResult = false; };
            sp.Children.Add(btnCancel);

            root.Child = sp;
            win.Content = root;
            win.ShowDialog();
            return seleccion;
        }

        // --- NUEVO: IMPRIMIR CIERRE DE CAJA (Z) ---
        public static void ImprimirCierreZ(DateTime fecha, System.Collections.Generic.Dictionary<string, decimal> totales, decimal totalFinal)
        {
            try
            {
                WinPrinting.PrintDocument doc = new WinPrinting.PrintDocument();
                doc.PrintController = new WinPrinting.StandardPrintController();
                doc.PrintPage += (s, e) =>
                {
                    var g = e.Graphics;
                    float w = ObtenerAnchoTicketPixels(DatabaseService.GetOpcionesImpresionTicket().AnchoMm);
                    float y = 0;
                    WinDrawing.Font fT = new WinDrawing.Font("Arial", 12, WinDrawing.FontStyle.Bold);
                    WinDrawing.Font fN = new WinDrawing.Font("Consolas", 9);
                    WinDrawing.Font fB = new WinDrawing.Font("Arial", 14, WinDrawing.FontStyle.Bold);

                    DibujarTextoCentrado(g, "CIERRE DE CAJA (Z)", fT, w, ref y);
                    g.DrawString($"Fecha: {fecha:dd/MM/yyyy}", fN, WinDrawing.Brushes.Black, 0, y); y += 15;
                    g.DrawString($"Impreso: {DateTime.Now:HH:mm}", fN, WinDrawing.Brushes.Black, 0, y); y += 15;
                    DibujarLinea(g, ref y, w);

                    foreach (var item in totales)
                    {
                        g.DrawString(item.Key, fN, WinDrawing.Brushes.Black, 0, y);
                        g.DrawString(item.Value.ToString("C2"), fN, WinDrawing.Brushes.Black, w - 70, y);
                        y += 20;
                    }

                    DibujarLinea(g, ref y, w);
                    y += 5;
                    g.DrawString("TOTAL RECAUDADO:", fT, WinDrawing.Brushes.Black, 0, y); y += 25;
                    DibujarTextoCentrado(g, totalFinal.ToString("C2"), fB, w, ref y);
                    y += 20;
                    DibujarTextoCentrado(g, ".", fN, w, ref y);
                };

                var (impresoraTicket, _) = DatabaseService.GetImpresoras();
                ImprimirDocumentoTicket(doc, impresoraTicket);
            }
            catch (Exception ex) { MessageBox.Show("Error imprimiendo Z: " + ex.Message); }
        }
        // ------------------------------------------
        #endregion

        private static void GenerarDocumentoA4ConItems(string tituloDocumento, string idColumn, DataRow cabecera, DataTable items, decimal total, string pieLegal, string lineaExtra = null, string urlQrFiscal = null)
        {
            try
            {
                FlowDocument doc = CrearDocumentoBase();
                doc.Blocks.Add(CrearEncabezadoDocumento(tituloDocumento, idColumn, cabecera, lineaExtra));
                doc.Blocks.Add(CrearBloqueCliente(cabecera));
                doc.Blocks.Add(CrearTablaItems(items));
                doc.Blocks.Add(CrearBloqueTotal(total));
                if (!string.IsNullOrWhiteSpace(urlQrFiscal))
                {
                    var bloqueQr = CrearBloqueQrFiscal(urlQrFiscal);
                    if (bloqueQr != null) doc.Blocks.Add(bloqueQr);
                }
                doc.Blocks.Add(new Paragraph(new Run(pieLegal)) { TextAlignment = TextAlignment.Center, FontSize = 10, Foreground = Brushes.Gray, Margin = new Thickness(0, 40, 0, 0) });
                MostrarDialogoImpresion(doc, $"{tituloDocumento}_{cabecera[idColumn]}");
            }
            catch (Exception ex) { MessageBox.Show("Error generando PDF: " + ex.Message); }
        }

        private static Block CrearBloqueQrFiscal(string urlQr)
        {
            try
            {
                byte[] png = ArcaQrHelper.GenerarPngBytes(urlQr, 5);
                if (png == null || png.Length == 0) return null;
                var bi = new BitmapImage();
                using (var ms = new MemoryStream(png))
                {
                    bi.BeginInit();
                    bi.CacheOption = BitmapCacheOption.OnLoad;
                    bi.StreamSource = ms;
                    bi.EndInit();
                    bi.Freeze();
                }
                var img = new System.Windows.Controls.Image
                {
                    Source = bi,
                    Width = 140,
                    Height = 140,
                    Stretch = Stretch.Uniform
                };
                var container = new BlockUIContainer(img) { Margin = new Thickness(0, 16, 0, 4) };
                return container;
            }
            catch { return null; }
        }

        private static void GenerarDocumentoA4Nota(string tituloDocumento, DataRow cabecera)
        {
            try
            {
                int notaId = Convert.ToInt32(cabecera["NotaID"]);
                DataTable itemsDetalle = DatabaseService.GetNotaVentaDetalle(notaId);

                FlowDocument doc = CrearDocumentoBase();
                doc.Blocks.Add(CrearEncabezadoDocumento(tituloDocumento, "NotaID", cabecera));
                doc.Blocks.Add(CrearBloqueCliente(cabecera));

                doc.Blocks.Add(CrearBloqueReferenciaNota(cabecera));

                if (itemsDetalle.Rows.Count > 0)
                {
                    // Documento estructurado por ítems (igual que una factura), no un párrafo con todo junto.
                    doc.Blocks.Add(CrearTablaItems(itemsDetalle));
                }
                else
                {
                    // Notas anteriores a esta mejora: no tienen detalle estructurado, se
                    // muestra el texto original guardado en su momento.
                    Paragraph pDetalle = new Paragraph { FontSize = 12, Margin = new Thickness(0, 4, 0, 14) };
                    pDetalle.Inlines.Add(new Run("Descripción: ") { FontWeight = FontWeights.Bold });
                    pDetalle.Inlines.Add(new Run(cabecera["Descripcion"]?.ToString() ?? "—"));
                    doc.Blocks.Add(pDetalle);
                }

                doc.Blocks.Add(CrearBloqueTotal(Convert.ToDecimal(cabecera["Monto"])));
                doc.Blocks.Add(new Paragraph(new Run("Documento no válido como factura fiscal.")) { TextAlignment = TextAlignment.Center, FontSize = 10, Foreground = Brushes.Gray, Margin = new Thickness(0, 40, 0, 0) });
                MostrarDialogoImpresion(doc, $"{tituloDocumento}_{cabecera["NotaID"]}");
            }
            catch (Exception ex) { MessageBox.Show("Error generando PDF: " + ex.Message); }
        }

        private static Block CrearBloqueReferenciaNota(DataRow cabecera)
        {
            var section = new Section();
            Paragraph p = new Paragraph { FontSize = 11, Margin = new Thickness(0, 0, 0, 12) };

            bool tieneReferencia = false;
            if (cabecera.Table.Columns.Contains("FacturaID") && cabecera["FacturaID"] != DBNull.Value)
            {
                DataRow factura = DatabaseService.GetFacturaPorID(Convert.ToInt32(cabecera["FacturaID"]));
                if (factura != null)
                {
                    string tipoComp = factura.Table.Columns.Contains("TipoComprobante") ? factura["TipoComprobante"]?.ToString() ?? "Comprobante" : "Comprobante";
                    int nroComp = factura["NumeroComprobanteAFIP"] != DBNull.Value && factura["NumeroComprobanteAFIP"] != null
                        ? Convert.ToInt32(factura["NumeroComprobanteAFIP"]) : Convert.ToInt32(cabecera["FacturaID"]);
                    DateTime fechaComp = Convert.ToDateTime(factura["Fecha"]);
                    p.Inlines.Add(new Run("Comprobante que modifica: ") { FontWeight = FontWeights.Bold });
                    p.Inlines.Add(new Run($"{tipoComp} N° {nroComp:D8} del {fechaComp:dd/MM/yyyy}"));
                    tieneReferencia = true;
                }
            }

            if (cabecera.Table.Columns.Contains("NumeroComprobante") && cabecera["NumeroComprobante"] != DBNull.Value
                && !string.IsNullOrWhiteSpace(cabecera["NumeroComprobante"].ToString()))
            {
                if (tieneReferencia) p.Inlines.Add(new LineBreak());
                p.Inlines.Add(new Run("Comprobante asociado: ") { FontWeight = FontWeights.Bold });
                p.Inlines.Add(new Run(cabecera["NumeroComprobante"].ToString()));
                tieneReferencia = true;
            }

            string motivo = ExtraerMotivo(cabecera["Descripcion"]?.ToString());
            if (!string.IsNullOrWhiteSpace(motivo))
            {
                if (tieneReferencia) p.Inlines.Add(new LineBreak());
                p.Inlines.Add(new Run("Motivo: ") { FontWeight = FontWeights.Bold });
                p.Inlines.Add(new Run(motivo));
                tieneReferencia = true;
            }

            if (!tieneReferencia)
                p.Inlines.Add(new Run(cabecera["Descripcion"]?.ToString() ?? ""));

            section.Blocks.Add(p);
            return section;
        }

        private static string ExtraerMotivo(string descripcion)
        {
            if (string.IsNullOrWhiteSpace(descripcion)) return null;
            const string marca = "Motivo:";
            int idx = descripcion.IndexOf(marca, StringComparison.OrdinalIgnoreCase);
            if (idx < 0) return null;
            return descripcion.Substring(idx + marca.Length).Trim();
        }

        private const double AnchoPaginaA4 = 793;
        private const double PaddingPaginaA4 = 30;
        private static double AnchoContenidoA4 => AnchoPaginaA4 - (PaddingPaginaA4 * 2);

        private static FlowDocument CrearDocumentoBase()
        {
            return new FlowDocument
            {
                PagePadding = new Thickness(30),
                ColumnWidth = double.PositiveInfinity,
                FontFamily = new FontFamily("Arial"),
                FontSize = 11,
                PageWidth = 793
            };
        }

        private static Block CrearEncabezadoDocumento(string tituloDocumento, string idColumn, DataRow cabecera, string lineaExtra = null)
        {
            DataRow conf = DatabaseService.GetConfiguracion();
            string razonSocial    = conf?["RazonSocial"]?.ToString()?.Trim() ?? "";
            string nombreFantasia = conf?["NombreFantasia"]?.ToString()?.Trim() ?? "";
            string cuit           = conf?["CUIT"]?.ToString() ?? "";
            string dir            = conf?["Direccion"]?.ToString() ?? "";
            string tel            = conf?["Telefono"]?.ToString() ?? "";
            string email          = conf?["Email"]?.ToString() ?? "";
            string puntoVenta     = conf?["PuntoVenta"]?.ToString()?.Trim() ?? "";
            string condicionIva   = conf != null && conf.Table.Columns.Contains("CondicionIVAEmpresa")
                ? conf["CondicionIVAEmpresa"]?.ToString()?.Trim() ?? "" : "";

            if (string.IsNullOrWhiteSpace(razonSocial))
                razonSocial = nombreFantasia;
            if (string.IsNullOrWhiteSpace(razonSocial))
                razonSocial = "Mi Negocio";

            bool mostrarLogo = conf != null
                && conf.Table.Columns.Contains("LogoEnA4")
                && conf["LogoEnA4"] != DBNull.Value
                && Convert.ToBoolean(conf["LogoEnA4"]);
            string logoPath = (conf != null && conf.Table.Columns.Contains("LogoPath"))
                ? conf["LogoPath"]?.ToString() ?? "" : "";

            var contenedor = new Section();

            // ══════════════════════════════════════════════════════
            //  ENCABEZADO — tabla de 2 columnas (empresa | documento)
            // ══════════════════════════════════════════════════════
            Table tbl = new Table { CellSpacing = 0, BorderThickness = new Thickness(0), Margin = new Thickness(0, 0, 0, 0) };
            tbl.Columns.Add(new TableColumn { Width = new GridLength(2, GridUnitType.Star) });
            tbl.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });

            var rg  = new TableRowGroup();
            var row = new TableRow();

            // ── Celda izquierda: logo + datos de empresa ──────────
            var cellLeft = new TableCell { Padding = new Thickness(0, 0, 16, 0) };

            // Logo via MemoryStream + Freeze — única forma confiable en contexto de impresión FlowDocument
            ImageSource logoSrc = CargarImagenParaImpresion(mostrarLogo ? logoPath : null);
            if (logoSrc != null)
                cellLeft.Blocks.Add(new BlockUIContainer(
                    new Image { Source = logoSrc, Height = 72, HorizontalAlignment = HorizontalAlignment.Left, Stretch = Stretch.Uniform })
                { Margin = new Thickness(0, 0, 0, 6) });

            // Nombre legal / razón social
            cellLeft.Blocks.Add(new Paragraph(new Run(razonSocial.ToUpper()))
            { FontSize = 18, FontWeight = FontWeights.Bold, Foreground = Brushes.Black, Margin = new Thickness(0, 0, 0, 3) });

            // Datos fiscales / contacto
            var pInfo = new Paragraph { FontSize = 10, LineHeight = 16, Foreground = Brushes.DimGray, Margin = new Thickness(0) };
            void AgregarLinea(string texto) { if (!string.IsNullOrWhiteSpace(texto)) { if (pInfo.Inlines.Count > 0) pInfo.Inlines.Add(new LineBreak()); pInfo.Inlines.Add(new Run(texto)); } }
            if (!string.IsNullOrWhiteSpace(nombreFantasia) && !nombreFantasia.Equals(razonSocial, StringComparison.OrdinalIgnoreCase))
                AgregarLinea(nombreFantasia);
            if (!string.IsNullOrWhiteSpace(cuit))   AgregarLinea($"CUIT: {cuit}");
            if (!string.IsNullOrWhiteSpace(condicionIva)) AgregarLinea($"IVA: {condicionIva}");
            if (!string.IsNullOrWhiteSpace(dir))    AgregarLinea($"Domicilio: {dir}");
            if (!string.IsNullOrWhiteSpace(tel))    AgregarLinea($"Tel: {tel}");
            if (!string.IsNullOrWhiteSpace(email))  AgregarLinea(email);
            if (pInfo.Inlines.Count > 0) cellLeft.Blocks.Add(pInfo);

            row.Cells.Add(cellLeft);

            // ── Celda derecha: tipo + número + fecha (recuadro sobrio) ──
            var cellRight = new TableCell
            {
                Padding         = new Thickness(12, 8, 12, 8),
                Background      = Brushes.White,
                BorderBrush     = Brushes.Black,
                BorderThickness = new Thickness(1),
                TextAlignment   = TextAlignment.Right
            };

            cellRight.Blocks.Add(new Paragraph(new Run(tituloDocumento.ToUpper()))
            { FontSize = 16, FontWeight = FontWeights.Bold, Foreground = Brushes.Black, Margin = new Thickness(0), TextAlignment = TextAlignment.Right });

            cellRight.Blocks.Add(new Paragraph(new Run($"N°  {int.Parse(cabecera[idColumn].ToString()):D8}"))
            { FontSize = 12, FontWeight = FontWeights.SemiBold, Foreground = Brushes.Black, Margin = new Thickness(0, 4, 0, 0), TextAlignment = TextAlignment.Right });

            cellRight.Blocks.Add(new Paragraph(new Run($"{Convert.ToDateTime(cabecera["Fecha"]):dd/MM/yyyy  HH:mm}"))
            { FontSize = 10, Foreground = Brushes.Black, Margin = new Thickness(0, 3, 0, 0), TextAlignment = TextAlignment.Right });

            if (!string.IsNullOrWhiteSpace(puntoVenta) && DatabaseService.GetOpcionesImpresionTicket().MostrarPuntoVenta)
                cellRight.Blocks.Add(new Paragraph(new Run($"P. Venta: {puntoVenta.PadLeft(4, '0')}"))
                { FontSize = 10, Foreground = Brushes.Black, Margin = new Thickness(0, 3, 0, 0), TextAlignment = TextAlignment.Right });

            string nombrePersonal = cabecera.Table.Columns.Contains("NombrePersonal")
                ? cabecera["NombrePersonal"]?.ToString()?.Trim() ?? "" : "";
            if (DatabaseService.GetOpcionesImpresionTicket().MostrarVendedor && !string.IsNullOrWhiteSpace(nombrePersonal))
                cellRight.Blocks.Add(new Paragraph(new Run($"Atendido por: {nombrePersonal}"))
                { FontSize = 10, Foreground = Brushes.Black, Margin = new Thickness(0, 3, 0, 0), TextAlignment = TextAlignment.Right });

            if (!string.IsNullOrWhiteSpace(lineaExtra))
                cellRight.Blocks.Add(new Paragraph(new Run(lineaExtra))
                { FontSize = 10, Foreground = Brushes.Black, Margin = new Thickness(0, 3, 0, 0), TextAlignment = TextAlignment.Right });

            row.Cells.Add(cellRight);
            rg.Rows.Add(row);
            tbl.RowGroups.Add(rg);
            contenedor.Blocks.Add(tbl);

            // Línea divisoria sobria
            Table tblLinea = new Table { CellSpacing = 0 };
            tblLinea.Columns.Add(new TableColumn());
            var rgL = new TableRowGroup();
            var rowL = new TableRow();
            var cellL = new TableCell
            {
                BorderBrush     = Brushes.Black,
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding         = new Thickness(0, 0, 0, 1)
            };
            cellL.Blocks.Add(new Paragraph() { Margin = new Thickness(0), LineHeight = 1 });
            rowL.Cells.Add(cellL);
            rgL.Rows.Add(rowL);
            tblLinea.RowGroups.Add(rgL);
            contenedor.Blocks.Add(tblLinea);

            contenedor.Blocks.Add(new Paragraph() { Margin = new Thickness(0, 10, 0, 0) });
            return contenedor;
        }

        private static Block CrearBloqueCliente(DataRow cabecera)
        {
            var section = new Section();
            Paragraph pCliente = new Paragraph { FontSize = 11 };
            pCliente.Inlines.Add(new Run("CLIENTE: ") { FontWeight = FontWeights.Bold });
            pCliente.Inlines.Add(new Run(cabecera["ClienteNombre"].ToString().ToUpper()));
            pCliente.Inlines.Add(new LineBreak());
            pCliente.Inlines.Add(new Run($"CUIT: {cabecera["ClienteCUIT"]}    |    IVA: {cabecera["ClienteIVA"]}"));
            string dir = cabecera["ClienteDireccion"].ToString();
            if (dir != "-") pCliente.Inlines.Add(new Run($"    |    Dir: {dir}"));
            section.Blocks.Add(pCliente);
            section.Blocks.Add(new BlockUIContainer(new Separator { Margin = new Thickness(0, 5, 0, 15), Background = Brushes.LightGray }));
            return section;
        }

        private static Block CrearTablaItems(DataTable items)
        {
            bool mostrarCodigo = DatabaseService.GetOpcionesImpresionTicket().MostrarCodigo;

            double ancho = AnchoContenidoA4;
            double colCant = 50;
            double colCod = mostrarCodigo ? 80 : 0;
            double colUnit = 95;
            double colTotal = 95;
            double colDesc = ancho - colCant - colCod - colUnit - colTotal;

            Table table = new Table
            {
                CellSpacing = 0,
                BorderBrush = Brushes.Black,
                BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 0, 0, 15)
            };
            table.Columns.Add(new TableColumn { Width = new GridLength(colCant) });
            if (mostrarCodigo)
                table.Columns.Add(new TableColumn { Width = new GridLength(colCod) });
            table.Columns.Add(new TableColumn { Width = new GridLength(colDesc) });
            table.Columns.Add(new TableColumn { Width = new GridLength(colUnit) });
            table.Columns.Add(new TableColumn { Width = new GridLength(colTotal) });

            TableRowGroup groupData = new TableRowGroup();
            TableRow rowTitulos = new TableRow { Background = Brushes.LightGray };
            rowTitulos.Cells.Add(CrearCelda("CANT", TextAlignment.Center, true));
            if (mostrarCodigo)
                rowTitulos.Cells.Add(CrearCelda("CÓDIGO", TextAlignment.Left, true));
            rowTitulos.Cells.Add(CrearCelda("DESCRIPCIÓN", TextAlignment.Left, true));
            rowTitulos.Cells.Add(CrearCelda("UNITARIO", TextAlignment.Right, true));
            rowTitulos.Cells.Add(CrearCelda("TOTAL", TextAlignment.Right, true));
            groupData.Rows.Add(rowTitulos);

            foreach (DataRow item in items.Rows)
            {
                TableRow r = new TableRow();
                r.Cells.Add(CrearCelda(Convert.ToDecimal(item["Cantidad"]).ToString("0.##"), TextAlignment.Center));
                if (mostrarCodigo)
                    r.Cells.Add(CrearCelda(item.Table.Columns.Contains("Codigo") ? item["Codigo"]?.ToString() ?? "" : "", TextAlignment.Left));
                r.Cells.Add(CrearCelda(item["Descripcion"].ToString(), TextAlignment.Left));
                r.Cells.Add(CrearCelda(Convert.ToDecimal(item["PrecioUnitario"]).ToString("C2"), TextAlignment.Right));
                r.Cells.Add(CrearCelda(Convert.ToDecimal(item["Subtotal"]).ToString("C2"), TextAlignment.Right));
                groupData.Rows.Add(r);
            }
            table.RowGroups.Add(groupData);
            return table;
        }

        private static Paragraph CrearBloqueTotal(decimal total)
        {
            Paragraph pTotal = new Paragraph { TextAlignment = TextAlignment.Right, Margin = new Thickness(0, 16, 0, 0) };
            pTotal.Inlines.Add(new Run("TOTAL:  ") { FontSize = 14, FontWeight = FontWeights.SemiBold });
            pTotal.Inlines.Add(new Run(total.ToString("C2")) { FontSize = 22, FontWeight = FontWeights.Bold });
            return pTotal;
        }

        private static void MostrarDialogoImpresion(FlowDocument doc, string jobName)
        {
            var (_, impresoraA4) = DatabaseService.GetImpresoras();

            if (!string.IsNullOrWhiteSpace(impresoraA4))
            {
                var pd = new PrintDialog();
                bool encontrada = false;
                foreach (var queue in new System.Printing.LocalPrintServer().GetPrintQueues())
                {
                    if (string.Equals(queue.FullName, impresoraA4, StringComparison.OrdinalIgnoreCase)
                        || string.Equals(queue.Name, impresoraA4, StringComparison.OrdinalIgnoreCase)
                        || queue.FullName.IndexOf(impresoraA4, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        pd.PrintQueue = queue;
                        encontrada = true;
                        break;
                    }
                }
                if (!encontrada)
                {
                    MessageBox.Show(
                        $"La impresora A4 configurada no está disponible:\n{impresoraA4}\n\nSeleccione otra impresora.",
                        "Impresora no encontrada", MessageBoxButton.OK, MessageBoxImage.Warning);
                    if (pd.ShowDialog() != true) return;
                }
                doc.PageHeight = pd.PrintableAreaHeight;
                doc.PageWidth  = pd.PrintableAreaWidth;
                doc.PagePadding = new Thickness(30);
                doc.ColumnGap  = 0;
                doc.ColumnWidth = pd.PrintableAreaWidth;
                pd.PrintDocument(((IDocumentPaginatorSource)doc).DocumentPaginator, jobName);
            }
            else
            {
                // Sin impresora configurada: mostrar diálogo
                PrintDialog pd = new PrintDialog();
                if (pd.ShowDialog() == true)
                {
                    doc.PageHeight  = pd.PrintableAreaHeight;
                    doc.PageWidth   = pd.PrintableAreaWidth;
                    doc.PagePadding = new Thickness(30);
                    doc.ColumnGap   = 0;
                    doc.ColumnWidth = pd.PrintableAreaWidth;
                    pd.PrintDocument(((IDocumentPaginatorSource)doc).DocumentPaginator, jobName);
                }
            }
        }

        private static TableCell CrearCelda(string texto, TextAlignment alineacion, bool negrita = false)
        {
            var p = new Paragraph(new Run(texto ?? ""))
            {
                TextAlignment = alineacion,
                Margin = new Thickness(0),
                LineHeight = 14
            };
            if (negrita) p.FontWeight = FontWeights.Bold;
            return new TableCell(p)
            {
                Padding = new Thickness(6, 5, 6, 5),
                BorderBrush = Brushes.Gray,
                BorderThickness = new Thickness(0, 0, 1, 1)
            };
        }

        private static void ImprimirTicketGrafico(string t, string n, string c, DateTime f, DataTable i, decimal tot, string extra, string l, string pie, string nombreVendedor = null, string urlQrFiscal = null)
        {
            try
            {
                var (impresoraTicket, _) = DatabaseService.GetImpresoras();

                WinPrinting.PrintDocument doc = new WinPrinting.PrintDocument();
                doc.PrintController = new WinPrinting.StandardPrintController();
                doc.PrintPage += (s, e) =>
                {
                    var g = e.Graphics;
                    float y = 10f;
                    DibujarTicketGDI(g, t, n, c ?? "", f, i, tot, extra, l, pie, ref y, nombreVendedor, urlQrFiscal);
                };

                ImprimirDocumentoTicket(doc, impresoraTicket);
            }
            catch (Exception x) { MessageBox.Show("Error Ticket: " + x.Message); }
        }

        private static void GuardarComprobanteArchivo(int facturaId, DataRow cab, DataTable items, string tipo, int nro, string cli, DateTime fec, decimal tot, string cond, string cae, string vto, string urlQrFiscal = null)
        {
            string letra = "X";
            if (tipo != null && tipo.IndexOf("Factura", StringComparison.OrdinalIgnoreCase) >= 0)
                letra = ObtenerLetraFactura(cli);

            string tit = tipo?.ToUpper() ?? "TICKET";
            string pieFiscal = "";
            if (cond != null && cond.Contains("CAE:"))
            {
                string[] p = cond.Split(new[] { "CAE:" }, StringSplitOptions.None);
                cond = p[0].Trim();
                if (p.Length > 1) pieFiscal = "CAE: " + p[1].Trim();
            }
            else if (!string.IsNullOrEmpty(cae))
                pieFiscal = $"CAE: {cae}    Vto CAE: {vto}";

            string pieLegal = tipo.Equals("Factura", StringComparison.OrdinalIgnoreCase)
                ? "Comprobante fiscal autorizado por ARCA."
                : "Documento no válido como factura fiscal.";

            var opciones = DatabaseService.GetOpcionesImpresionTicket();
            string nombreBase = $"{tit.Replace(" ", "_")}_{(nro > 0 ? nro.ToString("D8") : facturaId.ToString("D8"))}_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
            string ruta;

            string carpeta = DatabaseService.GetCarpetaArchivosComprobantes();
            if (!string.IsNullOrWhiteSpace(carpeta) && Directory.Exists(carpeta))
                ruta = Path.Combine(carpeta, nombreBase);
            else
            {
                var dlg = new SaveFileDialog
                {
                    Filter = "Documento PDF (*.pdf)|*.pdf|Todos los archivos|*.*",
                    FileName = nombreBase,
                    Title = "Guardar comprobante PDF"
                };
                if (dlg.ShowDialog() != true) return;
                ruta = dlg.FileName;
            }

            // CUIT/CAE/Punto de venta/QR son obligatorios en comprobantes con CAE.
            if (string.IsNullOrWhiteSpace(urlQrFiscal) && !string.IsNullOrWhiteSpace(cae))
                urlQrFiscal = ArcaQrHelper.ConstruirUrlDesdeFactura(cab, letra);

            PdfComprobanteGenerator.GenerarComprobanteVenta(
                ruta, cab, items, tit, letra, nro > 0 ? nro : facturaId, tot,
                opciones.MostrarFormaPago ? cond : null,
                pieFiscal, // pie fiscal siempre (CAE)
                pieLegal,
                opciones.MostrarCodigo,
                urlQrFiscal);

            MessageBox.Show(
                $"Comprobante PDF guardado en:\n{ruta}\n\nPodés enviarlo por WhatsApp o correo.",
                "PDF guardado", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private static string ObtenerLetraFactura(string cuitCliente)
        {
            DataRow config = DatabaseService.GetConfiguracion();
            string condicionEmisor = config != null && config.Table.Columns.Contains("CondicionIVAEmpresa")
                ? config["CondicionIVAEmpresa"]?.ToString() ?? ""
                : "";

            if (condicionEmisor.IndexOf("monotrib", StringComparison.OrdinalIgnoreCase) >= 0)
                return "C";

            string cuitLimpio = cuitCliente?.Replace("-", "").Trim() ?? "";
            return cuitLimpio.Length >= 11 && !cuitLimpio.Contains("00000000") ? "A" : "B";
        }

        private static float ObtenerAnchoTicketPixels(int anchoMm)
        {
            if (anchoMm <= 58) return 200f;
            return 260f;
        }

        private static void ImprimirDocumentoTicket(WinPrinting.PrintDocument doc, string impresoraTicket)
        {
            if (!string.IsNullOrWhiteSpace(impresoraTicket))
            {
                string nombreInstalado = ResolverNombreImpresora(impresoraTicket);
                if (nombreInstalado == null)
                {
                    MessageBox.Show(
                        $"La impresora de tickets configurada no está disponible:\n{impresoraTicket}\n\nSeleccione otra impresora.",
                        "Impresora no encontrada", MessageBoxButton.OK, MessageBoxImage.Warning);
                    var pd = new System.Windows.Forms.PrintDialog();
                    pd.Document = doc;
                    if (pd.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
                    doc.Print();
                    return;
                }
                // Cambiar PrinterName reinicia PaperSize/PageSettings en varios drivers térmicos.
                // Si el documento ya fue preparado con esa impresora, no volver a asignarla.
                if (!string.Equals(doc.PrinterSettings.PrinterName, nombreInstalado, StringComparison.OrdinalIgnoreCase))
                    doc.PrinterSettings.PrinterName = nombreInstalado;
                doc.Print();
            }
            else
            {
                var pd = new System.Windows.Forms.PrintDialog();
                pd.Document = doc;
                if (pd.ShowDialog() == System.Windows.Forms.DialogResult.OK) doc.Print();
            }
        }

        private static string ResolverNombreImpresora(string nombreConfigurado)
        {
            if (string.IsNullOrWhiteSpace(nombreConfigurado)) return null;
            string parcial = null;
            foreach (string instalada in WinPrinting.PrinterSettings.InstalledPrinters)
            {
                if (string.Equals(instalada, nombreConfigurado, StringComparison.OrdinalIgnoreCase))
                    return instalada;
                if (parcial == null && instalada.IndexOf(nombreConfigurado, StringComparison.OrdinalIgnoreCase) >= 0)
                    parcial = instalada;
            }
            return parcial;
        }

        private static void PrepararImpresoraAntesDeConfigurarPagina(WinPrinting.PrintDocument doc, string nombreConfigurado)
        {
            string instalada = ResolverNombreImpresora(nombreConfigurado);
            if (instalada != null &&
                !string.Equals(doc.PrinterSettings.PrinterName, instalada, StringComparison.OrdinalIgnoreCase))
                doc.PrinterSettings.PrinterName = instalada;
        }

        public static void ImprimirPaginaDePrueba(string nombreImpresora, string tipo)
        {
            try
            {
                DataRow conf    = DatabaseService.GetConfiguracion();
                string fan      = conf?["NombreFantasia"]?.ToString() ?? "Mi Negocio";
                string dir      = conf?["Direccion"]?.ToString() ?? "";
                string tel      = conf?["Telefono"]?.ToString() ?? "";
                string cuit     = conf?["CUIT"]?.ToString() ?? "";
                bool mostrarLog = tipo == "Ticket"
                    ? (conf != null && conf.Table.Columns.Contains("LogoEnTicket") && conf["LogoEnTicket"] != DBNull.Value && Convert.ToBoolean(conf["LogoEnTicket"]))
                    : (conf != null && conf.Table.Columns.Contains("LogoEnA4")     && conf["LogoEnA4"]     != DBNull.Value && Convert.ToBoolean(conf["LogoEnA4"]));
                string logoPath = (conf != null && conf.Table.Columns.Contains("LogoPath")) ? conf["LogoPath"]?.ToString() ?? "" : "";

                WinPrinting.PrintDocument doc = new WinPrinting.PrintDocument();
                doc.PrinterSettings.PrinterName = nombreImpresora;
                doc.PrintController = new WinPrinting.StandardPrintController();

                if (string.Equals(tipo, "Etiqueta", StringComparison.OrdinalIgnoreCase))
                {
                    var opEtiq = DatabaseService.GetOpcionesEtiqueta();
                    bool horizontalPrueba = string.Equals(opEtiq.Orientacion, "Horizontal", StringComparison.OrdinalIgnoreCase);
                    PrepararImpresoraAntesDeConfigurarPagina(doc, nombreImpresora);
                    AplicarTamanoEtiqueta(doc, opEtiq.AnchoMm, opEtiq.AltoMm);

                    var itemPrueba = new EtiquetaPrintItem
                    {
                        Descripcion = "Producto de prueba",
                        Codigo = "PRUEBA",
                        CodigoBarra = "7790001000019",
                        PrecioVenta = 1234.50m,
                        Marca = "SCHPOS",
                        Cantidad = 1
                    };
                    doc.PrintPage += (s, e) =>
                    {
                        if (horizontalPrueba)
                            DibujarEtiquetaRotadaGDI(e.Graphics, opEtiq, itemPrueba);
                        else
                            DibujarEtiquetaGDI(e.Graphics, opEtiq, itemPrueba);
                        e.HasMorePages = false;
                    };
                    ImprimirDocumentoTicket(doc, nombreImpresora);
                    MessageBox.Show($"Etiqueta de prueba ({opEtiq.AnchoMm}×{opEtiq.AltoMm} mm) enviada a:\n{nombreImpresora}",
                        "Prueba de impresión", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                doc.PrintPage += (s, e) =>
                {
                    var g = e.Graphics;
                    string razonSocial = conf?["RazonSocial"]?.ToString()?.Trim() ?? "";
                    if (string.IsNullOrWhiteSpace(razonSocial))
                        razonSocial = fan;
                    string condicionIva = conf != null && conf.Table.Columns.Contains("CondicionIVAEmpresa")
                        ? conf["CondicionIVAEmpresa"]?.ToString()?.Trim() ?? "" : "";

                    if (tipo == "Ticket")
                    {
                        ConfigurarGraphicsTicketTermico(g);
                        float w = ObtenerAnchoTicketPixels(DatabaseService.GetOpcionesImpresionTicket().AnchoMm);
                        float y = 14;
                        var fT = new WinDrawing.Font("Lucida Console", 10f, WinDrawing.FontStyle.Bold);
                        var fN = new WinDrawing.Font("Lucida Console", 9f, WinDrawing.FontStyle.Regular);
                        var fS = new WinDrawing.Font("Lucida Console", 8f, WinDrawing.FontStyle.Regular);

                        if (mostrarLog && !string.IsNullOrWhiteSpace(logoPath) && System.IO.File.Exists(logoPath))
                        {
                            try
                            {
                                byte[] logoBytes = System.IO.File.ReadAllBytes(logoPath);
                                using (var ms2 = new System.IO.MemoryStream(logoBytes))
                                using (var bmp = new WinDrawing.Bitmap(ms2))
                                {
                                    float maxH = 50f;
                                    float ratio = bmp.Width / (float)bmp.Height;
                                    float lh = maxH, lw = lh * ratio;
                                    if (lw > w - 20) { lw = w - 20; lh = lw / ratio; }
                                    g.DrawImage(bmp, (w - lw) / 2f, y, lw, lh);
                                    y += lh + 8f;
                                }
                            }
                            catch { }
                        }

                        DibujarTextoCentrado(g, razonSocial.ToUpper(), fT, w, ref y);
                        if (!string.IsNullOrWhiteSpace(dir)) DibujarTextoCentrado(g, dir, fS, w, ref y);
                        if (!string.IsNullOrWhiteSpace(tel)) DibujarTextoCentrado(g, $"Tel: {tel}", fS, w, ref y);
                        if (!string.IsNullOrWhiteSpace(cuit)) DibujarTextoCentrado(g, $"CUIT: {cuit}", fS, w, ref y);
                        DibujarLinea(g, ref y, w);
                        DibujarTextoCentrado(g, "PÁGINA DE PRUEBA DE IMPRESIÓN", fT, w, ref y);
                        y += 6;
                        DibujarTextoCentrado(g, $"Impresora: {nombreImpresora}", fN, w, ref y);
                        DibujarTextoCentrado(g, $"Fecha: {DateTime.Now:dd/MM/yyyy  HH:mm}", fN, w, ref y);
                        DibujarTextoCentrado(g, "Logo: " + (mostrarLog && System.IO.File.Exists(logoPath) ? "OK" : "No configurado"), fS, w, ref y);
                        DibujarLinea(g, ref y, w);
                        DibujarTextoCentrado(g, "SCHPOS — Configuración correcta", fT, w, ref y);
                        return;
                    }

                    // A4: diseño sobrio a ancho completo
                    float x = e.MarginBounds.Left;
                    float wA4 = e.MarginBounds.Width;
                    float yA4 = e.MarginBounds.Top;
                    var fTitulo = new WinDrawing.Font("Arial", 14, WinDrawing.FontStyle.Bold);
                    var fSub = new WinDrawing.Font("Arial", 10, WinDrawing.FontStyle.Bold);
                    var fDet = new WinDrawing.Font("Arial", 9);
                    var fPie = new WinDrawing.Font("Arial", 8);

                    if (mostrarLog && !string.IsNullOrWhiteSpace(logoPath) && System.IO.File.Exists(logoPath))
                    {
                        try
                        {
                            byte[] logoBytes = System.IO.File.ReadAllBytes(logoPath);
                            using (var ms2 = new System.IO.MemoryStream(logoBytes))
                            using (var bmp = new WinDrawing.Bitmap(ms2))
                            {
                                float maxH = 72f;
                                float ratio = bmp.Width / (float)bmp.Height;
                                float lh = maxH, lw = lh * ratio;
                                g.DrawImage(bmp, x, yA4, lw, lh);
                                yA4 += lh + 8f;
                            }
                        }
                        catch { }
                    }

                    g.DrawString(razonSocial.ToUpper(), fTitulo, WinDrawing.Brushes.Black, x, yA4);
                    yA4 += g.MeasureString(razonSocial, fTitulo).Height + 2;
                    if (!string.IsNullOrWhiteSpace(cuit))
                    {
                        g.DrawString($"CUIT: {cuit}", fDet, WinDrawing.Brushes.Black, x, yA4);
                        yA4 += 14;
                    }
                    if (!string.IsNullOrWhiteSpace(condicionIva))
                    {
                        g.DrawString($"IVA: {condicionIva}", fDet, WinDrawing.Brushes.Black, x, yA4);
                        yA4 += 14;
                    }
                    if (!string.IsNullOrWhiteSpace(dir))
                    {
                        g.DrawString($"Domicilio: {dir}", fDet, WinDrawing.Brushes.Black, x, yA4);
                        yA4 += 14;
                    }
                    if (!string.IsNullOrWhiteSpace(tel))
                    {
                        g.DrawString($"Tel: {tel}", fDet, WinDrawing.Brushes.Black, x, yA4);
                        yA4 += 14;
                    }

                    float cajaW = 210f;
                    float cajaX = x + wA4 - cajaW;
                    float cajaY = e.MarginBounds.Top;
                    g.DrawRectangle(WinDrawing.Pens.Black, cajaX, cajaY, cajaW, 88);
                    g.DrawString("PÁGINA DE PRUEBA", fSub, WinDrawing.Brushes.Black, cajaX + 10, cajaY + 10);
                    g.DrawString($"Impresora:", fPie, WinDrawing.Brushes.Black, cajaX + 10, cajaY + 32);
                    g.DrawString(nombreImpresora, fDet, WinDrawing.Brushes.Black, new WinDrawing.RectangleF(cajaX + 10, cajaY + 44, cajaW - 20, 30));
                    g.DrawString(DateTime.Now.ToString("dd/MM/yyyy HH:mm"), fDet, WinDrawing.Brushes.Black, cajaX + 10, cajaY + 68);

                    yA4 = Math.Max(yA4, cajaY + 100);
                    g.DrawLine(WinDrawing.Pens.Black, x, yA4, x + wA4, yA4);
                    yA4 += 16;

                    g.DrawString("Verificación de impresión A4", fSub, WinDrawing.Brushes.Black, x, yA4);
                    yA4 += 18;
                    g.DrawString("Este documento confirma que la impresora A4 está correctamente configurada en SCHPOS.", fDet, WinDrawing.Brushes.Black, new WinDrawing.RectangleF(x, yA4, wA4, 40));
                    yA4 += 36;
                    g.DrawString("Logo: " + (mostrarLog && System.IO.File.Exists(logoPath) ? "OK" : "No configurado"), fDet, WinDrawing.Brushes.Black, x, yA4);
                    yA4 += 20;
                    g.DrawLine(WinDrawing.Pens.Black, x, yA4, x + wA4, yA4);
                    yA4 += 12;
                    g.DrawString("SCHPOS — Configuración correcta", fSub, WinDrawing.Brushes.Black, x, yA4);
                };
                doc.Print();
                MessageBox.Show($"Página de prueba enviada a:\n{nombreImpresora}", "Prueba de impresión", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            }
            catch (Exception ex) { MessageBox.Show("Error al imprimir prueba: " + ex.Message); }
        }

        /// <summary>
        /// Configura el render GDI para tickets térmicos: texto nítido (sin antialias borroso).
        /// </summary>
        private static void ConfigurarGraphicsTicketTermico(WinDrawing.Graphics g)
        {
            g.SmoothingMode = WinDrawing.Drawing2D.SmoothingMode.None;
            g.InterpolationMode = WinDrawing.Drawing2D.InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = WinDrawing.Drawing2D.PixelOffsetMode.Half;
            g.CompositingQuality = WinDrawing.Drawing2D.CompositingQuality.HighSpeed;
            g.TextRenderingHint = WinDrawing.Text.TextRenderingHint.SingleBitPerPixelGridFit;
            try { g.TextContrast = 0; } catch { /* no disponible en algunos drivers */ }
        }

        private static void DibujarTicketGDI(WinDrawing.Graphics g, string tit, string nro, string cli, DateTime fec, DataTable its, decimal tot, string extra, string let, string pie, ref float y, string nombreVendedor = null, string urlQrFiscal = null)
        {
            ConfigurarGraphicsTicketTermico(g);

            var opciones = DatabaseService.GetOpcionesImpresionTicket();
            // Datos fiscales obligatorios: siempre se imprimen (no dependen de checkboxes).
            opciones.MostrarCuit = true;
            opciones.MostrarPieFiscal = true;
            opciones.MostrarPuntoVenta = true;

            float w = ObtenerAnchoTicketPixels(opciones.AnchoMm);
            bool angosto = opciones.AnchoMm <= 58;

            // Fuentes monoespaciadas + bold para mejor nitidez en térmica
            WinDrawing.Font fT  = new WinDrawing.Font("Lucida Console", angosto ? 9f : 10f, WinDrawing.FontStyle.Bold);
            WinDrawing.Font fN  = new WinDrawing.Font("Lucida Console", angosto ? 8f : 9f, WinDrawing.FontStyle.Regular);
            WinDrawing.Font fC  = new WinDrawing.Font("Lucida Console", angosto ? 7f : 8f, WinDrawing.FontStyle.Regular);
            WinDrawing.Font fB  = new WinDrawing.Font("Lucida Console", angosto ? 12f : 14f, WinDrawing.FontStyle.Bold);
            WinDrawing.Font fSub = new WinDrawing.Font("Lucida Console", angosto ? 7f : 8f, WinDrawing.FontStyle.Regular);

            DataRow conf = DatabaseService.GetConfiguracion();
            string fan   = conf?["NombreFantasia"]?.ToString() ?? "Mi Negocio";
            string dir   = conf?["Direccion"]?.ToString() ?? "";
            string tel   = conf?["Telefono"]?.ToString() ?? "";
            string cuit  = conf?["CUIT"]?.ToString() ?? "";
            string condicionIva = conf != null && conf.Table.Columns.Contains("CondicionIVAEmpresa")
                ? conf["CondicionIVAEmpresa"]?.ToString()?.Trim() ?? "" : "";
            string puntoVenta = conf?["PuntoVenta"]?.ToString()?.Trim() ?? "";

            bool mostrarLogo = opciones.MostrarLogo;
            string logoPath  = conf != null && conf.Table.Columns.Contains("LogoPath")
                               ? conf["LogoPath"]?.ToString() ?? "" : "";

            if (mostrarLogo && !string.IsNullOrWhiteSpace(logoPath) && System.IO.File.Exists(logoPath))
            {
                try
                {
                    byte[] logoBytes = System.IO.File.ReadAllBytes(logoPath);
                    using (var ms = new System.IO.MemoryStream(logoBytes))
                    using (var bmp = new WinDrawing.Bitmap(ms))
                    {
                        float maxH  = angosto ? 45f : 55f;
                        float ratio = bmp.Width / (float)bmp.Height;
                        float lh = maxH, lw = lh * ratio;
                        if (lw > w - 16) { lw = w - 16; lh = lw / ratio; }
                        g.DrawImage(bmp, (w - lw) / 2f, y, lw, lh);
                        y += lh + 6f;
                    }
                }
                catch { }
            }

            DibujarTextoCentrado(g, fan.ToUpper(), fT, w, ref y);

            if (opciones.MostrarDireccion && !string.IsNullOrWhiteSpace(dir))   DibujarTextoCentrado(g, dir, fSub, w, ref y);
            if (opciones.MostrarTelefono && !string.IsNullOrWhiteSpace(tel))    DibujarTextoCentrado(g, $"Tel: {tel}", fSub, w, ref y);
            if (opciones.MostrarCuit && !string.IsNullOrWhiteSpace(cuit))   DibujarTextoCentrado(g, $"CUIT: {cuit}", fSub, w, ref y);
            if (opciones.MostrarCuit && !string.IsNullOrWhiteSpace(condicionIva)) DibujarTextoCentrado(g, $"IVA: {condicionIva}", fSub, w, ref y);

            DibujarLinea(g, ref y, w);

            g.DrawString($"{tit}  —  Letra: {let}", fN, WinDrawing.Brushes.Black, 0, y); y += 14;
            g.DrawString($"N°: {nro}", fN, WinDrawing.Brushes.Black, 0, y); y += 14;
            g.DrawString($"Fecha: {fec:dd/MM/yyyy  HH:mm}", fN, WinDrawing.Brushes.Black, 0, y); y += 14;

            if (opciones.MostrarPuntoVenta && !string.IsNullOrWhiteSpace(puntoVenta))
            {
                g.DrawString($"P. Venta: {puntoVenta.PadLeft(4, '0')}", fN, WinDrawing.Brushes.Black, 0, y); y += 14;
            }
            if (opciones.MostrarVendedor && !string.IsNullOrWhiteSpace(nombreVendedor))
            {
                string vend = nombreVendedor.Length > (angosto ? 28 : 38) ? nombreVendedor.Substring(0, angosto ? 28 : 38) : nombreVendedor;
                g.DrawString($"Atendido por: {vend}", fN, WinDrawing.Brushes.Black, 0, y); y += 14;
            }

            if (opciones.MostrarCliente)
            {
                string cliTxt = cli ?? "";
                int maxCli = angosto ? 28 : 38;
                if (cliTxt.Length > maxCli) cliTxt = cliTxt.Substring(0, maxCli);
                g.DrawString($"Cliente: {cliTxt}", fN, WinDrawing.Brushes.Black, 0, y); y += 14;
            }

            DibujarLinea(g, ref y, w);

            int maxDesc = opciones.MostrarCodigo ? (angosto ? 14 : 18) : (angosto ? 20 : 24);
            string encabezado = opciones.MostrarCodigo ? "Cant  Cód  Descripción" : "Cant  Descripción";
            g.DrawString(encabezado, fC, WinDrawing.Brushes.DimGray, 0, y);
            DibujarTextoDerecha(g, "Total", fC, w, 2, ref y, false);
            y += 12;

            foreach (DataRow r in its.Rows)
            {
                string d = r.Table.Columns.Contains("Descripcion") ? r["Descripcion"].ToString() : r["Producto"].ToString();
                if (d.Length > maxDesc) d = d.Substring(0, maxDesc);

                string cod = opciones.MostrarCodigo && r.Table.Columns.Contains("Codigo")
                    ? (r["Codigo"]?.ToString() ?? "").Trim() : "";
                if (opciones.MostrarCodigo && cod.Length > 8) cod = cod.Substring(0, 8);

                string linea = opciones.MostrarCodigo
                    ? $"{r["Cantidad"],2}x {cod,-8} {d}"
                    : $"{r["Cantidad"],2}x  {d}";
                g.DrawString(linea, fN, WinDrawing.Brushes.Black, 0, y);

                string subtotalStr = Convert.ToDecimal(r["Subtotal"]).ToString("N2");
                DibujarTextoDerecha(g, subtotalStr, fN, w, 2, ref y, false);
                y += 14;
            }
            DibujarLinea(g, ref y, w);

            y += 4;
            g.DrawString("TOTAL  A  PAGAR:", fT, WinDrawing.Brushes.Black, 0, y); y += 18;
            string totalStr = $"${tot:N2}";
            WinDrawing.SizeF sT = g.MeasureString(totalStr, fB);
            g.DrawString(totalStr, fB, WinDrawing.Brushes.Black, (w - sT.Width) / 2, y);
            y += sT.Height + 4;

            if (opciones.MostrarFormaPago && !string.IsNullOrEmpty(extra))
            {
                int maxPago = angosto ? 28 : 35;
                if (extra.Length > maxPago)
                {
                    g.DrawString("Pago: " + extra.Substring(0, maxPago), fC, WinDrawing.Brushes.Black, 0, y); y += 12;
                    g.DrawString(extra.Substring(maxPago), fC, WinDrawing.Brushes.Black, 0, y); y += 12;
                }
                else
                {
                    g.DrawString("Pago: " + extra, fC, WinDrawing.Brushes.Black, 0, y); y += 15;
                }
            }

            if (opciones.MostrarPieFiscal && !string.IsNullOrEmpty(pie))
            {
                DibujarLinea(g, ref y, w);
                foreach (string lineaPie in pie.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries))
                    DibujarTextoCentrado(g, lineaPie.Trim(), fC, w, ref y);
            }

            // QR fiscal ARCA obligatorio cuando el comprobante tiene CAE.
            if (!string.IsNullOrWhiteSpace(urlQrFiscal))
            {
                try
                {
                    using (var qrBmp = ArcaQrHelper.GenerarBitmap(urlQrFiscal, angosto ? 3 : 4))
                    {
                        if (qrBmp != null)
                        {
                            y += 6;
                            float side = Math.Min(w - 8f, angosto ? 110f : 140f);
                            float xQr = (w - side) / 2f;
                            // NearestNeighbor mantiene módulos nítidos en térmica
                            var oldInterp = g.InterpolationMode;
                            g.InterpolationMode = WinDrawing.Drawing2D.InterpolationMode.NearestNeighbor;
                            g.DrawImage(qrBmp, xQr, y, side, side);
                            g.InterpolationMode = oldInterp;
                            y += side + 4f;
                            DibujarTextoCentrado(g, "Escaneá el QR para verificar en ARCA", fC, w, ref y);
                        }
                    }
                }
                catch { /* no bloquear la impresión si falla el QR */ }
            }

            if (opciones.MostrarGracias)
            {
                y += 10;
                DibujarTextoCentrado(g, "Gracias por su compra", fC, w, ref y);
            }
            DibujarTextoCentrado(g, ".", fC, w, ref y);
        }

        private static void DibujarTextoDerecha(WinDrawing.Graphics g, string texto, WinDrawing.Font f, float w, float margen, ref float y, bool avanzarY)
        {
            WinDrawing.SizeF s = g.MeasureString(texto, f);
            g.DrawString(texto, f, WinDrawing.Brushes.Black, w - s.Width - margen, y);
            if (avanzarY) y += s.Height;
        }

        private static void DibujarLinea(WinDrawing.Graphics g, ref float y, float w) { y += 3; g.DrawLine(new WinDrawing.Pen(WinDrawing.Color.Black) { DashStyle = WinDrawing.Drawing2D.DashStyle.Dash }, 2, y, w - 2, y); y += 5; }
        private static void DibujarTextoCentrado(WinDrawing.Graphics g, string t, WinDrawing.Font f, float w, ref float y) { WinDrawing.SizeF s = g.MeasureString(t, f); g.DrawString(t, f, WinDrawing.Brushes.Black, (w - s.Width) / 2, y); y += s.Height; }

        #region ETIQUETAS

        public static void ImprimirEtiquetas(IList<EtiquetaPrintItem> items, OpcionesEtiqueta opciones = null)
        {
            if (items == null || items.Count == 0)
            {
                MessageBox.Show("No hay productos para imprimir.");
                return;
            }

            opciones = opciones ?? DatabaseService.GetOpcionesEtiqueta();
            var cola = new List<EtiquetaPrintItem>();
            foreach (var it in items)
            {
                if (it == null) continue;
                int n = Math.Max(1, it.Cantidad);
                for (int i = 0; i < n; i++)
                    cola.Add(it);
            }
            if (cola.Count == 0)
            {
                MessageBox.Show("Indicá al menos 1 etiqueta.");
                return;
            }

            try
            {
                if (string.Equals(opciones.ModoImpresion, "A4", StringComparison.OrdinalIgnoreCase))
                {
                    ImprimirEtiquetasA4(cola, opciones);
                    return;
                }
                if (string.Equals(opciones.ModoImpresion, "Cartel", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(opciones.ModoImpresion, "Gondola", StringComparison.OrdinalIgnoreCase))
                {
                    ImprimirCartelesYGondolas(cola, opciones);
                    return;
                }

                string impresora = DatabaseService.GetImpresoraEtiquetas();
                var doc = new WinPrinting.PrintDocument();
                doc.PrintController = new WinPrinting.StandardPrintController();
                PrepararImpresoraAntesDeConfigurarPagina(doc, impresora);

                // Papel físico SIEMPRE Ancho×Alto (ej. 55×44). La orientación Horizontal
                // rota el contenido por software; no se declara 44×55 porque muchos drivers
                // térmicos rechazan ese formulario y emiten página en blanco.
                bool horizontal = string.Equals(opciones.Orientacion, "Horizontal", StringComparison.OrdinalIgnoreCase);
                AplicarTamanoEtiqueta(doc, opciones.AnchoMm, opciones.AltoMm);

                int idx = 0;
                doc.PrintPage += (s, e) =>
                {
                    if (horizontal)
                        DibujarEtiquetaRotadaGDI(e.Graphics, opciones, cola[idx]);
                    else
                        DibujarEtiquetaGDI(e.Graphics, opciones, cola[idx]);
                    idx++;
                    e.HasMorePages = idx < cola.Count;
                };

                ImprimirDocumentoTicket(doc, impresora);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al imprimir etiquetas: " + ex.Message);
            }
        }

        private static void ImprimirEtiquetasA4(IList<EtiquetaPrintItem> cola, OpcionesEtiqueta op)
        {
            string impresora = DatabaseService.GetImpresoraEtiquetas();
            var doc = new WinPrinting.PrintDocument();
            doc.PrintController = new WinPrinting.StandardPrintController();
            PrepararImpresoraAntesDeConfigurarPagina(doc, impresora);
            doc.DefaultPageSettings.PaperSize = new WinPrinting.PaperSize("A4", 827, 1169);
            doc.DefaultPageSettings.Landscape = string.Equals(op.Orientacion, "Horizontal", StringComparison.OrdinalIgnoreCase);
            doc.DefaultPageSettings.Margins = new WinPrinting.Margins(0, 0, 0, 0);

            int idx = 0;
            doc.PrintPage += (s, e) =>
            {
                var g = e.Graphics;
                g.PageUnit = WinDrawing.GraphicsUnit.Millimeter;
                float pageW = doc.DefaultPageSettings.Landscape ? 297f : 210f;
                float pageH = doc.DefaultPageSettings.Landscape ? 210f : 297f;
                float x0 = Math.Max(0, op.MargenIzquierdoMm);
                float y = Math.Max(0, op.MargenSuperiorMm);
                float x = x0;
                float labelW = Math.Max(10, op.AnchoMm);
                float labelH = Math.Max(10, op.AltoMm);
                float gapH = Math.Max(0, op.GapHorizontalMm);
                float gapV = Math.Max(0, op.GapVerticalMm);
                int col = 0;
                int maxCols = Math.Max(1, op.Columnas);

                while (idx < cola.Count)
                {
                    if (col >= maxCols || x + labelW > pageW - op.MargenDerechoMm + 0.1f)
                    {
                        col = 0;
                        x = x0;
                        y += labelH + gapV;
                    }
                    if (y + labelH > pageH - op.MargenInferiorMm + 0.1f)
                        break;

                    var state = g.Save();
                    g.TranslateTransform(x, y);
                    DibujarEtiquetaGDI(g, op, cola[idx]);
                    g.Restore(state);
                    using (var pen = new WinDrawing.Pen(WinDrawing.Color.LightGray, 0.1f))
                        g.DrawRectangle(pen, x, y, labelW, labelH);

                    idx++;
                    col++;
                    x += labelW + gapH;
                }
                e.HasMorePages = idx < cola.Count;
            };

            ImprimirDocumentoTicket(doc, impresora);
        }

        private static void ImprimirCartelesYGondolas(IList<EtiquetaPrintItem> cola, OpcionesEtiqueta op)
        {
            string impresora = DatabaseService.GetImpresoraEtiquetas();
            var doc = new WinPrinting.PrintDocument();
            doc.PrintController = new WinPrinting.StandardPrintController();
            PrepararImpresoraAntesDeConfigurarPagina(doc, impresora);
            doc.DefaultPageSettings.PaperSize = new WinPrinting.PaperSize("A4", 827, 1169);
            doc.DefaultPageSettings.Landscape = string.Equals(op.Orientacion, "Horizontal", StringComparison.OrdinalIgnoreCase)
                || string.Equals(op.ModoImpresion, "Gondola", StringComparison.OrdinalIgnoreCase);
            doc.DefaultPageSettings.Margins = new WinPrinting.Margins(0, 0, 0, 0);

            int idx = 0;
            doc.PrintPage += (s, e) =>
            {
                DibujarCartelGDI(e.Graphics, op, cola[idx]);
                idx++;
                e.HasMorePages = idx < cola.Count;
            };

            ImprimirDocumentoTicket(doc, impresora);
        }

        private static void AplicarTamanoEtiqueta(WinPrinting.PrintDocument doc, int anchoMm, int altoMm)
        {
            anchoMm = Math.Max(10, Math.Min(300, anchoMm));
            altoMm = Math.Max(10, Math.Min(300, altoMm));
            // PaperSize usa centésimas de pulgada
            int w = (int)Math.Round(anchoMm / 25.4 * 100.0);
            int h = (int)Math.Round(altoMm / 25.4 * 100.0);

            // Preferir un formulario ya registrado en el driver (55×44, etc.).
            WinPrinting.PaperSize paper = null;
            foreach (WinPrinting.PaperSize existente in doc.PrinterSettings.PaperSizes)
            {
                if (Math.Abs(existente.Width - w) <= 2 && Math.Abs(existente.Height - h) <= 2)
                {
                    paper = existente;
                    break;
                }
            }
            if (paper == null)
                paper = new WinPrinting.PaperSize($"Etiqueta{anchoMm}x{altoMm}", w, h);

            doc.DefaultPageSettings.Landscape = false;
            doc.DefaultPageSettings.PaperSize = paper;
            doc.DefaultPageSettings.Margins = new WinPrinting.Margins(0, 0, 0, 0);
            doc.PrinterSettings.DefaultPageSettings.Landscape = false;
            doc.PrinterSettings.DefaultPageSettings.PaperSize = paper;
            doc.PrinterSettings.DefaultPageSettings.Margins = new WinPrinting.Margins(0, 0, 0, 0);
            // Algunos drivers restauran su tamaño predeterminado justo antes de imprimir.
            doc.QueryPageSettings += (s, e) =>
            {
                e.PageSettings.Landscape = false;
                e.PageSettings.PaperSize = paper;
                e.PageSettings.Margins = new WinPrinting.Margins(0, 0, 0, 0);
            };
        }

        private static void DibujarEtiquetaGDI(WinDrawing.Graphics g, OpcionesEtiqueta op, EtiquetaPrintItem item)
        {
            if (g == null || item == null) return;
            g.PageUnit = WinDrawing.GraphicsUnit.Millimeter;
            g.SmoothingMode = WinDrawing.Drawing2D.SmoothingMode.None;
            g.InterpolationMode = WinDrawing.Drawing2D.InterpolationMode.NearestNeighbor;

            float w = Math.Max(10, op.AnchoMm);
            float h = Math.Max(10, op.AltoMm);
            float margin = Math.Max(0.8f, Math.Min(w, h) * 0.04f);
            float y = margin;
            float contentW = w - margin * 2;

            float fontDesc = h <= 28 ? 2.2f : (h <= 40 ? 2.6f : 3.2f);
            float fontSec = h <= 28 ? 1.8f : 2.2f;
            float fontPrecio = h <= 28 ? 2.8f : 3.4f;

            using (var fDesc = new WinDrawing.Font("Arial", fontDesc, WinDrawing.FontStyle.Bold, WinDrawing.GraphicsUnit.Millimeter))
            using (var fSec = new WinDrawing.Font("Arial", fontSec, WinDrawing.FontStyle.Regular, WinDrawing.GraphicsUnit.Millimeter))
            using (var fPrecio = new WinDrawing.Font("Arial", fontPrecio, WinDrawing.FontStyle.Bold, WinDrawing.GraphicsUnit.Millimeter))
            {
                if (op.MostrarDescripcion && !string.IsNullOrWhiteSpace(item.Descripcion))
                {
                    string desc = TruncarTextoEtiqueta(g, item.Descripcion.Trim(), fDesc, contentW);
                    g.DrawString(desc, fDesc, WinDrawing.Brushes.Black, margin, y);
                    y += g.MeasureString(desc, fDesc).Height + 0.3f;
                }

                if (op.MostrarDescripcionExtra && !string.IsNullOrWhiteSpace(item.DescripcionExtra))
                {
                    string descExtra = TruncarTextoEtiqueta(g, item.DescripcionExtra.Trim(), fSec, contentW);
                    g.DrawString(descExtra, fSec, WinDrawing.Brushes.Black, margin, y);
                    y += g.MeasureString(descExtra, fSec).Height + 0.2f;
                }

                if (op.MostrarMarca && !string.IsNullOrWhiteSpace(item.Marca))
                {
                    string marca = TruncarTextoEtiqueta(g, item.Marca.Trim(), fSec, contentW);
                    g.DrawString(marca, fSec, WinDrawing.Brushes.Black, margin, y);
                    y += g.MeasureString(marca, fSec).Height + 0.2f;
                }

                if (op.MostrarCodigo && !string.IsNullOrWhiteSpace(item.Codigo))
                {
                    string cod = "Cod: " + item.Codigo.Trim();
                    g.DrawString(TruncarTextoEtiqueta(g, cod, fSec, contentW), fSec, WinDrawing.Brushes.Black, margin, y);
                    y += g.MeasureString("X", fSec).Height + 0.2f;
                }

                if (op.MostrarCodigoBarras)
                {
                    string data = !string.IsNullOrWhiteSpace(item.CodigoBarra) ? item.CodigoBarra.Trim()
                        : (!string.IsNullOrWhiteSpace(item.Codigo) ? item.Codigo.Trim() : "");
                    if (!string.IsNullOrWhiteSpace(data))
                    {
                        float barH = Math.Max(6f, h - y - (op.MostrarPrecio ? fontPrecio + 1.5f : margin) - margin);
                        barH = Math.Min(barH, h * 0.45f);
                        using (var bmp = GenerarBitmapCodigoBarras(data, (int)(contentW * 12), (int)(barH * 12)))
                        {
                            if (bmp != null)
                                g.DrawImage(bmp, margin, y, contentW, barH);
                        }
                        y += barH + 0.4f;
                        // Texto humano debajo del código
                        string human = TruncarTextoEtiqueta(g, data, fSec, contentW);
                        var sz = g.MeasureString(human, fSec);
                        g.DrawString(human, fSec, WinDrawing.Brushes.Black, margin + (contentW - sz.Width) / 2f, y);
                        y += sz.Height + 0.2f;
                    }
                }

                if (op.MostrarPrecio)
                {
                    string precio = item.PrecioVenta.ToString("C2");
                    var sz = g.MeasureString(precio, fPrecio);
                    float py = Math.Max(y, h - margin - sz.Height);
                    g.DrawString(precio, fPrecio, WinDrawing.Brushes.Black, margin + (contentW - sz.Width) / 2f, py);
                }
            }
        }

        /// <summary>
        /// Dibuja la etiqueta girada 90° para el modo "Horizontal" del impresor directo
        /// de etiquetas. El papel físico permanece Ancho×Alto; se renderiza el contenido
        /// en un bitmap Ancho×Alto, se rota 90° y se estampa centrado dentro de esa misma
        /// área física (sin pedir al driver un papel 44×55).
        /// </summary>
        private static void DibujarEtiquetaRotadaGDI(WinDrawing.Graphics gPagina, OpcionesEtiqueta op, EtiquetaPrintItem item)
        {
            if (gPagina == null || item == null) return;

            const float dpi = 300f;
            float fisicoW = Math.Max(10, op.AnchoMm);
            float fisicoH = Math.Max(10, op.AltoMm);
            // Lienzo lógico Alto×Ancho: al rotar 90° queda exactamente Ancho×Alto físico.
            float logicaW = fisicoH;
            float logicaH = fisicoW;
            int wPx = Math.Max(1, (int)Math.Round(logicaW / 25.4 * dpi));
            int hPx = Math.Max(1, (int)Math.Round(logicaH / 25.4 * dpi));

            var opLogica = new OpcionesEtiqueta
            {
                AnchoMm = (int)Math.Round(logicaW),
                AltoMm = (int)Math.Round(logicaH),
                MostrarDescripcion = op.MostrarDescripcion,
                MostrarDescripcionExtra = op.MostrarDescripcionExtra,
                MostrarMarca = op.MostrarMarca,
                MostrarCodigo = op.MostrarCodigo,
                MostrarCodigoBarras = op.MostrarCodigoBarras,
                MostrarPrecio = op.MostrarPrecio
            };

            using (var bmp = new WinDrawing.Bitmap(wPx, hPx))
            {
                bmp.SetResolution(dpi, dpi);
                using (var gBmp = WinDrawing.Graphics.FromImage(bmp))
                {
                    gBmp.Clear(WinDrawing.Color.White);
                    DibujarEtiquetaGDI(gBmp, opLogica, item);
                }

                bmp.RotateFlip(WinDrawing.RotateFlipType.Rotate90FlipNone);

                gPagina.PageUnit = WinDrawing.GraphicsUnit.Millimeter;
                gPagina.DrawImage(bmp, 0f, 0f, fisicoW, fisicoH);
            }
        }

        private static void DibujarCartelGDI(WinDrawing.Graphics g, OpcionesEtiqueta op, EtiquetaPrintItem item)
        {
            if (g == null || item == null) return;
            g.PageUnit = WinDrawing.GraphicsUnit.Millimeter;
            g.SmoothingMode = WinDrawing.Drawing2D.SmoothingMode.AntiAlias;
            float pageW = string.Equals(op.Orientacion, "Horizontal", StringComparison.OrdinalIgnoreCase)
                || string.Equals(op.ModoImpresion, "Gondola", StringComparison.OrdinalIgnoreCase) ? 297f : 210f;
            float pageH = pageW > 210f ? 210f : 297f;
            float margin = Math.Max(8, op.MargenIzquierdoMm);
            float contentW = pageW - margin * 2;
            float y = margin;

            using (var fMarca = new WinDrawing.Font("Arial", 16, WinDrawing.FontStyle.Bold, WinDrawing.GraphicsUnit.Millimeter))
            using (var fNombre = new WinDrawing.Font("Arial", 9, WinDrawing.FontStyle.Bold, WinDrawing.GraphicsUnit.Millimeter))
            using (var fDesc = new WinDrawing.Font("Arial", 5, WinDrawing.FontStyle.Regular, WinDrawing.GraphicsUnit.Millimeter))
            using (var fPrecio = new WinDrawing.Font("Arial", pageW > 210f ? 30 : 26, WinDrawing.FontStyle.Bold, WinDrawing.GraphicsUnit.Millimeter))
            using (var fCodigo = new WinDrawing.Font("Arial", 4, WinDrawing.FontStyle.Regular, WinDrawing.GraphicsUnit.Millimeter))
            {
                if (op.MostrarMarca && !string.IsNullOrWhiteSpace(item.Marca))
                    DibujarTextoCartelCentrado(g, item.Marca.ToUpperInvariant(), fMarca, margin, contentW, ref y);

                if (op.MostrarDescripcion && !string.IsNullOrWhiteSpace(item.Descripcion))
                    DibujarTextoCartelCentrado(g, item.Descripcion, fNombre, margin, contentW, ref y);

                if (op.MostrarDescripcionExtra && !string.IsNullOrWhiteSpace(item.DescripcionExtra))
                    DibujarTextoCartelCentrado(g, item.DescripcionExtra, fDesc, margin, contentW, ref y);

                if (op.MostrarPrecio)
                {
                    string precio = item.PrecioVenta.ToString("C2");
                    var sz = g.MeasureString(precio, fPrecio);
                    g.DrawString(precio, fPrecio, WinDrawing.Brushes.Black, margin + (contentW - sz.Width) / 2f, y + 8);
                    y += sz.Height + 12;
                }

                if (op.MostrarCodigoBarras)
                {
                    string data = !string.IsNullOrWhiteSpace(item.CodigoBarra) ? item.CodigoBarra.Trim() : item.Codigo?.Trim();
                    if (!string.IsNullOrWhiteSpace(data))
                    {
                        float barW = Math.Min(contentW, 120f);
                        float barH = 22f;
                        using (var bmp = GenerarBitmapCodigoBarras(data, (int)(barW * 10), (int)(barH * 10)))
                        {
                            if (bmp != null)
                                g.DrawImage(bmp, margin + (contentW - barW) / 2f, Math.Min(y, pageH - margin - barH - 8), barW, barH);
                        }
                        y += barH + 2;
                        DibujarTextoCartelCentrado(g, data, fCodigo, margin, contentW, ref y);
                    }
                }

                if (op.MostrarCodigo && !string.IsNullOrWhiteSpace(item.Codigo))
                    g.DrawString("Cod: " + item.Codigo, fCodigo, WinDrawing.Brushes.Black, margin, pageH - margin - 6);
            }
        }

        private static void DibujarTextoCartelCentrado(WinDrawing.Graphics g, string texto, WinDrawing.Font font, float x, float w, ref float y)
        {
            if (string.IsNullOrWhiteSpace(texto)) return;
            var rect = new WinDrawing.RectangleF(x, y, w, g.MeasureString(texto, font, (int)w).Height + 6);
            using (var sf = new WinDrawing.StringFormat { Alignment = WinDrawing.StringAlignment.Center, LineAlignment = WinDrawing.StringAlignment.Center })
                g.DrawString(texto, font, WinDrawing.Brushes.Black, rect, sf);
            y += rect.Height;
        }

        private static string TruncarTextoEtiqueta(WinDrawing.Graphics g, string texto, WinDrawing.Font f, float maxW)
        {
            if (string.IsNullOrEmpty(texto)) return "";
            if (g.MeasureString(texto, f).Width <= maxW) return texto;
            string t = texto;
            while (t.Length > 1 && g.MeasureString(t + "…", f).Width > maxW)
                t = t.Substring(0, t.Length - 1);
            return t + "…";
        }

        private static WinDrawing.Bitmap GenerarBitmapCodigoBarras(string data, int widthPx, int heightPx)
        {
            try
            {
                widthPx = Math.Max(80, widthPx);
                heightPx = Math.Max(40, heightPx);
                var writer = new BarcodeWriter
                {
                    Format = BarcodeFormat.CODE_128,
                    Options = new EncodingOptions
                    {
                        Width = widthPx,
                        Height = heightPx,
                        Margin = 0,
                        PureBarcode = true
                    }
                };
                // EAN-13 si aplica
                string digits = new string(data.Where(char.IsDigit).ToArray());
                if (digits.Length == 13)
                {
                    writer.Format = BarcodeFormat.EAN_13;
                    data = digits;
                }
                else if (digits.Length == 8)
                {
                    writer.Format = BarcodeFormat.EAN_8;
                    data = digits;
                }
                return writer.Write(data);
            }
            catch
            {
                try
                {
                    var writer = new BarcodeWriter
                    {
                        Format = BarcodeFormat.CODE_128,
                        Options = new EncodingOptions { Width = widthPx, Height = heightPx, Margin = 0, PureBarcode = true }
                    };
                    return writer.Write(data);
                }
                catch { return null; }
            }
        }

        #endregion

        /// <summary>
        /// Carga una imagen desde disco usando MemoryStream y aplica Freeze().
        /// Es la única forma fiable de usar imágenes en FlowDocument para impresión WPF.
        /// </summary>
        private static ImageSource CargarImagenParaImpresion(string ruta)
        {
            if (string.IsNullOrWhiteSpace(ruta) || !File.Exists(ruta)) return null;
            try
            {
                byte[] bytes = File.ReadAllBytes(ruta);
                using (var ms = new System.IO.MemoryStream(bytes))
                {
                    var bi = new BitmapImage();
                    bi.BeginInit();
                    bi.StreamSource = ms;
                    bi.CacheOption  = BitmapCacheOption.OnLoad;
                    bi.EndInit();
                    bi.Freeze(); // imprescindible para que WPF lo use fuera del hilo de UI
                    return bi;
                }
            }
            catch { return null; }
        }
    }
}