using SchettiniGestion;
using System;
using System.Data;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace SchettiniGestion.WPF
{
	public static class PrintService
	{
		// Colores y Estilos
		private static readonly SolidColorBrush BrushBorde = Brushes.Black;
		private static readonly SolidColorBrush BrushFondoGris = new SolidColorBrush(Color.FromRgb(230, 230, 230)); // Gris claro
		private static readonly FontFamily FontFiscal = new FontFamily("Arial"); // Fuente moderna para ticket
		private static readonly FontFamily FontFactura = new FontFamily("Calibri"); // Fuente formal para A4

		// --- MÉTODOS PÚBLICOS ---

		public static void ImprimirPresupuesto(int presupuestoID, string clienteNombre, DateTime fecha, DataTable items, decimal total)
		{
			// Para presupuestos usamos el diseño A4 si la impresora es grande, o Ticket si es chica
			ImprimirDocumentoGenerico("PRESUPUESTO", presupuestoID.ToString(), clienteNombre, fecha, items, total, "Válido por 7 días", "X");
		}

		public static void ImprimirTicketVenta(string tipoComprobante, int nroComprobante, string clienteNombre, DateTime fecha, DataTable items, decimal total, string condicionVenta)
		{
			// Detectar letra (A, B, X)
			string letra = "X";
			if (tipoComprobante.Contains("A")) letra = "A";
			if (tipoComprobante.Contains("B")) letra = "B";

			string titulo = tipoComprobante.ToUpper();
			string nroStr = nroComprobante > 0 ? nroComprobante.ToString("D8") : "(Pendiente)";

			// Si hay CAE en el texto de condición (lo guardamos ahí temporalmente), lo separamos
			string pieFiscal = "";
			if (condicionVenta.Contains("CAE:"))
			{
				// Separamos la condición del CAE para mostrarlo bonito abajo
				string[] partes = condicionVenta.Split(new[] { "CAE:" }, StringSplitOptions.None);
				condicionVenta = partes[0].Trim();
				if (partes.Length > 1) pieFiscal = "CAE: " + partes[1].Trim();
			}

			ImprimirDocumentoGenerico(titulo, nroStr, clienteNombre, fecha, items, total, condicionVenta, letra, pieFiscal);
		}

		// --- MOTOR DE DECISIÓN ---

		private static void ImprimirDocumentoGenerico(string tituloDoc, string numeroDoc, string cliente, DateTime fecha, DataTable items, decimal total, string infoExtra, string letra, string pieFiscal = "")
		{
			try
			{
				PrintDialog pd = new PrintDialog();

				if (pd.ShowDialog() == true)
				{
					double anchoPapel = pd.PrintableAreaWidth;
					double altoPapel = pd.PrintableAreaHeight;

					FlowDocument doc = new FlowDocument();
					doc.PageWidth = anchoPapel;
					doc.PageHeight = altoPapel;
					doc.ColumnWidth = anchoPapel;
					doc.FontFamily = FontFactura;

					// Decidir diseño (A4 > 500px)
					if (anchoPapel > 500)
					{
						DibujarFacturaA4(doc, anchoPapel, tituloDoc, numeroDoc, cliente, fecha, items, total, infoExtra, letra, pieFiscal);
					}
					else
					{
						doc.PagePadding = new Thickness(2); // Margen mínimo para ticket
						DibujarTicket80mm(doc, anchoPapel, tituloDoc, numeroDoc, cliente, fecha, items, total, infoExtra, pieFiscal);
					}

					IDocumentPaginatorSource idpSource = doc;
					pd.PrintDocument(idpSource.DocumentPaginator, $"{tituloDoc} #{numeroDoc}");
				}
			}
			catch (Exception ex) { MessageBox.Show($"Error al imprimir: {ex.Message}"); }
		}

		// =================================================================================================
		// DISEÑO 1: TICKET 80MM (Estilo Moderno como la foto)
		// =================================================================================================
		private static void DibujarTicket80mm(FlowDocument doc, double anchoDisponible, string titulo, string numero, string cliente, DateTime fecha, DataTable items, decimal total, string infoExtra, string pieFiscal)
		{
			doc.FontFamily = FontFiscal;
			doc.FontSize = 9;
			if (anchoDisponible <= 0) anchoDisponible = 280;

			DataRow config = DatabaseService.GetConfiguracion();
			string empresa = "CASA SCHETTINI";
			string direccion = "";
			string cuit = "";
			string logoPath = "";

			if (config != null)
			{
				empresa = !string.IsNullOrEmpty(config["NombreFantasia"].ToString()) ? config["NombreFantasia"].ToString() : config["RazonSocial"].ToString();
				direccion = config["Direccion"].ToString();
				cuit = config["CUIT"].ToString();
				logoPath = config["LogoPath"].ToString();
			}

			Paragraph p = new Paragraph() { TextAlignment = TextAlignment.Center, Margin = new Thickness(0) };

			// 1. LOGO
			/* Si quieres logo en el ticket, descomenta esto:
            if (!string.IsNullOrEmpty(logoPath) && System.IO.File.Exists(logoPath))
            {
                try { p.Inlines.Add(new Image() { Width = 100, Source = new BitmapImage(new Uri(logoPath)) }); p.Inlines.Add(new LineBreak()); } catch { }
            }
            */

			// 2. ENCABEZADO CENTRADO
			p.Inlines.Add(new Run(empresa.ToUpper()) { FontWeight = FontWeights.Bold, FontSize = 11 });
			p.Inlines.Add(new LineBreak());
			p.Inlines.Add(new Run("CASA CENTRAL"));
			p.Inlines.Add(new LineBreak());
			if (!string.IsNullOrEmpty(cuit)) { p.Inlines.Add(new Run($"CUIT: {cuit}")); p.Inlines.Add(new LineBreak()); }
			if (!string.IsNullOrEmpty(direccion)) { p.Inlines.Add(new Run(direccion)); p.Inlines.Add(new LineBreak()); }
			p.Inlines.Add(new Run("IVA RESPONSABLE INSCRIPTO"));
			p.Inlines.Add(new LineBreak());
			p.Inlines.Add(new Run("--------------------------------------------------")); // Separador
			p.Inlines.Add(new LineBreak());

			// DATOS COMPROBANTE
			p.Inlines.Add(new Run($"{titulo} N° {numero}") { FontWeight = FontWeights.Bold });
			p.Inlines.Add(new LineBreak());
			p.Inlines.Add(new Run($"FECHA: {fecha:dd/MM/yyyy}  HORA: {fecha:HH:mm}"));
			p.Inlines.Add(new LineBreak());
			p.Inlines.Add(new Run("--------------------------------------------------"));
			p.Inlines.Add(new LineBreak());
			doc.Blocks.Add(p);

			// DATOS CLIENTE (Alineado a la izquierda)
			Paragraph pCli = new Paragraph() { Margin = new Thickness(0) };
			pCli.Inlines.Add(new Run($"CLIENTE: {cliente.ToUpper()}"));
			pCli.Inlines.Add(new LineBreak());
			pCli.Inlines.Add(new Run($"CONDICIÓN: {infoExtra.Replace("CONDICIÓN: ", "")}"));
			doc.Blocks.Add(pCli);

			doc.Blocks.Add(new Paragraph(new Run("--------------------------------------------------")) { Margin = new Thickness(0), TextAlignment = TextAlignment.Center });

			// 3. GRILLA PRODUCTOS (Estilo Foto: Cant x Precio en linea 1, Descripcion en linea 2)
			Table t = new Table() { CellSpacing = 0, Margin = new Thickness(0) };
			t.Columns.Add(new TableColumn() { Width = new GridLength(1, GridUnitType.Star) }); // Descripcion (ocupa todo)
			t.Columns.Add(new TableColumn() { Width = new GridLength(70) }); // Total a la derecha

			TableRowGroup rg = new TableRowGroup();

			// Encabezados simples
			TableRow h = new TableRow();
			h.Cells.Add(new TableCell(new Paragraph(new Run("DESCRIPCIÓN / CANT x PRECIO")) { FontWeight = FontWeights.Bold }));
			h.Cells.Add(new TableCell(new Paragraph(new Run("TOTAL")) { FontWeight = FontWeights.Bold, TextAlignment = TextAlignment.Right }));
			rg.Rows.Add(h);

			foreach (DataRow row in items.Rows)
			{
				decimal cant = Convert.ToDecimal(row["Cantidad"]);
				decimal unit = 0;
				if (row.Table.Columns.Contains("PrecioUnitario")) unit = Convert.ToDecimal(row["PrecioUnitario"]);
				decimal sub = Convert.ToDecimal(row["Subtotal"]);
				string desc = row["Descripcion"].ToString();

				// FILA 1: Descripción
				TableRow r1 = new TableRow();
				r1.Cells.Add(new TableCell(new Paragraph(new Run(desc))) { ColumnSpan = 2 });
				rg.Rows.Add(r1);

				// FILA 2: Cant x Precio ......... Total
				TableRow r2 = new TableRow();
				r2.Cells.Add(new TableCell(new Paragraph(new Run($"{cant:N2} x {unit:N2}")) { FontSize = 8, Foreground = Brushes.Gray }));
				r2.Cells.Add(new TableCell(new Paragraph(new Run($"{sub:N2}")) { TextAlignment = TextAlignment.Right, FontWeight = FontWeights.Bold }));
				rg.Rows.Add(r2);
			}
			t.RowGroups.Add(rg);
			doc.Blocks.Add(t);

			doc.Blocks.Add(new Paragraph(new Run("--------------------------------------------------")) { Margin = new Thickness(0), TextAlignment = TextAlignment.Center });

			// 4. TOTALES
			Table tTot = new Table() { CellSpacing = 0 };
			tTot.Columns.Add(new TableColumn() { Width = new GridLength(1, GridUnitType.Star) });
			tTot.Columns.Add(new TableColumn() { Width = new GridLength(100) });
			TableRowGroup rgTot = new TableRowGroup();

			// Subtotal (Simulado para visual)
			decimal neto = total / 1.21m;
			decimal iva = total - neto;

			AgregarFilaTotal(rgTot, "SUBTOTAL:", total, false);
			// AgregarFilaTotal(rgTot, "IVA:", iva, false); // Opcional en ticket B
			AgregarFilaTotal(rgTot, "TOTAL:", total, true); // Total Grande

			tTot.RowGroups.Add(rgTot);
			doc.Blocks.Add(tTot);

			// 5. PIE FISCAL (QR y CAE)
			Paragraph pPie = new Paragraph() { TextAlignment = TextAlignment.Center, Margin = new Thickness(0, 10, 0, 0) };
			if (!string.IsNullOrEmpty(pieFiscal))
			{
				pPie.Inlines.Add(new Run(pieFiscal) { FontWeight = FontWeights.Bold });
				pPie.Inlines.Add(new LineBreak());
			}
			pPie.Inlines.Add(new Run("\n¡MUCHAS GRACIAS POR SU COMPRA!"));
			pPie.Inlines.Add(new LineBreak());
			pPie.Inlines.Add(new Run("Software: Schettini Gestión"));
			doc.Blocks.Add(pPie);
		}

		private static void AgregarFilaTotal(TableRowGroup rg, string label, decimal val, bool isBig)
		{
			TableRow r = new TableRow();
			r.Cells.Add(new TableCell(new Paragraph(new Run(label)) { TextAlignment = TextAlignment.Right, FontWeight = isBig ? FontWeights.Bold : FontWeights.Normal }));
			r.Cells.Add(new TableCell(new Paragraph(new Run($"$ {val:N2}")) { TextAlignment = TextAlignment.Right, FontWeight = isBig ? FontWeights.Black : FontWeights.Normal, FontSize = isBig ? 14 : 9 }));
			rg.Rows.Add(r);
		}


		// =================================================================================================
		// DISEÑO 2: FACTURA A4 (Réplica Exacta del PDF enviado)
		// =================================================================================================
		private static void DibujarFacturaA4(FlowDocument doc, double anchoDisponible, string titulo, string numero, string cliente, DateTime fecha, DataTable items, decimal total, string infoExtra, string letra, string pieFiscal)
		{
			doc.PagePadding = new Thickness(40);
			doc.FontSize = 10;
			double anchoUtil = anchoDisponible - 80;

			DataRow config = DatabaseService.GetConfiguracion();
			string empresa = "CASA SCHETTINI";
			string dirEmpresa = "";
			string telEmpresa = "";
			string cuitEmpresa = "";
			string logoPath = "";

			if (config != null)
			{
				empresa = !string.IsNullOrEmpty(config["RazonSocial"].ToString()) ? config["RazonSocial"].ToString() : config["NombreFantasia"].ToString();
				dirEmpresa = config["Direccion"].ToString();
				telEmpresa = config["Telefono"].ToString();
				cuitEmpresa = config["CUIT"].ToString();
				logoPath = config["LogoPath"].ToString();
			}

			// --- ESTRUCTURA PRINCIPAL: CABECERA CON 3 ZONAS ---
			// ZONA 1: Logo y Datos Emisor (Izq) | ZONA 2: Letra (Centro) | ZONA 3: Datos Comprobante (Der)

			Table headerT = new Table();
			headerT.Columns.Add(new TableColumn() { Width = new GridLength(45, GridUnitType.Star) }); // Izq
			headerT.Columns.Add(new TableColumn() { Width = new GridLength(10, GridUnitType.Star) }); // Centro (Letra)
			headerT.Columns.Add(new TableColumn() { Width = new GridLength(45, GridUnitType.Star) }); // Der
			TableRowGroup hGrp = new TableRowGroup();
			TableRow hRow = new TableRow();

			// 1. IZQUIERDA (Logo + Empresa)
			StackPanel pnlIzq = new StackPanel();
			// Logo
			if (!string.IsNullOrEmpty(logoPath) && System.IO.File.Exists(logoPath))
			{
				try { pnlIzq.Children.Add(new Image() { Height = 60, HorizontalAlignment = HorizontalAlignment.Left, Source = new BitmapImage(new Uri(logoPath)), Margin = new Thickness(0, 0, 0, 10) }); } catch { }
			}
			// Datos Empresa
			pnlIzq.Children.Add(new TextBlock() { Text = empresa, FontWeight = FontWeights.Bold, FontSize = 16 });
			pnlIzq.Children.Add(new TextBlock() { Text = dirEmpresa, FontSize = 10 });
			pnlIzq.Children.Add(new TextBlock() { Text = $"Tel: {telEmpresa}", FontSize = 10 });
			pnlIzq.Children.Add(new TextBlock() { Text = "ventas@casaschettini.com", FontSize = 10 });
			pnlIzq.Children.Add(new TextBlock() { Text = "IVA RESPONSABLE INSCRIPTO", FontWeight = FontWeights.Bold, Margin = new Thickness(0, 5, 0, 0) });

			hRow.Cells.Add(new TableCell(new BlockUIContainer(pnlIzq)) { BorderThickness = new Thickness(0, 0, 0, 1), BorderBrush = BrushBorde, Padding = new Thickness(0, 0, 10, 10) });

			// 2. CENTRO (Letra)
			Border boxLetra = new Border() { BorderBrush = BrushBorde, BorderThickness = new Thickness(1), Background = BrushFondoGris, Width = 40, Height = 40, VerticalAlignment = VerticalAlignment.Top };
			StackPanel pnlLetra = new StackPanel() { VerticalAlignment = VerticalAlignment.Center };
			pnlLetra.Children.Add(new TextBlock() { Text = letra, FontSize = 24, FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center });
			pnlLetra.Children.Add(new TextBlock() { Text = "COD. 01", FontSize = 8, HorizontalAlignment = HorizontalAlignment.Center }); // Codigo AFIP (01=A, 06=B)
			boxLetra.Child = pnlLetra;
			hRow.Cells.Add(new TableCell(new BlockUIContainer(boxLetra)) { BorderThickness = new Thickness(0, 0, 0, 1), BorderBrush = BrushBorde });

			// 3. DERECHA (Datos Factura)
			StackPanel pnlDer = new StackPanel() { HorizontalAlignment = HorizontalAlignment.Right };
			pnlDer.Children.Add(new TextBlock() { Text = titulo, FontWeight = FontWeights.Bold, FontSize = 18, HorizontalAlignment = HorizontalAlignment.Right });
			pnlDer.Children.Add(new TextBlock() { Text = $"N° {numero}", FontWeight = FontWeights.Bold, FontSize = 14, HorizontalAlignment = HorizontalAlignment.Right });
			pnlDer.Children.Add(new TextBlock() { Text = $"FECHA: {fecha:dd/MM/yyyy}", HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 5, 0, 0) });
			pnlDer.Children.Add(new TextBlock() { Text = $"CUIT: {cuitEmpresa}", HorizontalAlignment = HorizontalAlignment.Right });
			pnlDer.Children.Add(new TextBlock() { Text = $"Ing. Brutos: {cuitEmpresa}", HorizontalAlignment = HorizontalAlignment.Right });
			pnlDer.Children.Add(new TextBlock() { Text = $"Inicio Act: 01/01/2000", HorizontalAlignment = HorizontalAlignment.Right });

			hRow.Cells.Add(new TableCell(new BlockUIContainer(pnlDer)) { BorderThickness = new Thickness(0, 0, 0, 1), BorderBrush = BrushBorde, Padding = new Thickness(10, 0, 0, 10) });

			hGrp.Rows.Add(hRow);
			headerT.RowGroups.Add(hGrp);
			doc.Blocks.Add(headerT);

			// --- CLIENTE (Caja con Borde) ---
			Border borderCli = new Border() { BorderBrush = BrushBorde, BorderThickness = new Thickness(1), Margin = new Thickness(0, 10, 0, 10), Padding = new Thickness(5) };
			Grid gridCli = new Grid();
			gridCli.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Star) });
			gridCli.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Star) });

			StackPanel cliIzq = new StackPanel();
			cliIzq.Children.Add(CrearLineaDato("SEÑOR/ES:", cliente));
			cliIzq.Children.Add(CrearLineaDato("IVA:", "CONSUMIDOR FINAL")); // Esto debería venir de la BD
			cliIzq.Children.Add(CrearLineaDato("DOMICILIO:", "-"));

			StackPanel cliDer = new StackPanel();
			cliDer.Children.Add(CrearLineaDato("CUIT:", "-")); // Debería venir de BD
			cliDer.Children.Add(CrearLineaDato("CONDICIÓN VENTA:", infoExtra.Replace("CONDICIÓN: ", "")));

			Grid.SetColumn(cliIzq, 0);
			Grid.SetColumn(cliDer, 1);
			gridCli.Children.Add(cliIzq);
			gridCli.Children.Add(cliDer);

			borderCli.Child = gridCli;
			doc.Blocks.Add(new BlockUIContainer(borderCli));

			// --- GRILLA PRODUCTOS ---
			Table tProd = new Table() { CellSpacing = 0, BorderBrush = BrushBorde, BorderThickness = new Thickness(1) };
			tProd.Columns.Add(new TableColumn() { Width = new GridLength(15, GridUnitType.Star) }); // Cod
			tProd.Columns.Add(new TableColumn() { Width = new GridLength(45, GridUnitType.Star) }); // Desc
			tProd.Columns.Add(new TableColumn() { Width = new GridLength(10, GridUnitType.Star) }); // Cant
			tProd.Columns.Add(new TableColumn() { Width = new GridLength(15, GridUnitType.Star) }); // Unit
			tProd.Columns.Add(new TableColumn() { Width = new GridLength(15, GridUnitType.Star) }); // Subtotal

			TableRowGroup rgProd = new TableRowGroup();

			// Encabezados Grises
			TableRow hProd = new TableRow() { Background = BrushFondoGris };
			hProd.Cells.Add(CeldaGrilla("CÓDIGO", true));
			hProd.Cells.Add(CeldaGrilla("DESCRIPCIÓN", true));
			hProd.Cells.Add(CeldaGrilla("CANT.", true, TextAlignment.Center));
			hProd.Cells.Add(CeldaGrilla("PRECIO UNIT.", true, TextAlignment.Right));
			hProd.Cells.Add(CeldaGrilla("SUBTOTAL", true, TextAlignment.Right));
			rgProd.Rows.Add(hProd);

			foreach (DataRow row in items.Rows)
			{
				TableRow tr = new TableRow();
				string codigo = row.Table.Columns.Contains("Codigo") ? row["Codigo"].ToString() : "-";
				decimal sub = Convert.ToDecimal(row["Subtotal"]);
				decimal cant = Convert.ToDecimal(row["Cantidad"]);
				decimal unit = cant != 0 ? sub / cant : 0;
				if (row.Table.Columns.Contains("PrecioUnitario")) unit = Convert.ToDecimal(row["PrecioUnitario"]);

				tr.Cells.Add(CeldaGrilla(codigo));
				tr.Cells.Add(CeldaGrilla(row["Descripcion"].ToString()));
				tr.Cells.Add(CeldaGrilla(cant.ToString("N2"), false, TextAlignment.Center));
				tr.Cells.Add(CeldaGrilla(unit.ToString("N2"), false, TextAlignment.Right));
				tr.Cells.Add(CeldaGrilla(sub.ToString("N2"), false, TextAlignment.Right));
				rgProd.Rows.Add(tr);
			}
			tProd.RowGroups.Add(rgProd);
			doc.Blocks.Add(tProd);

			// --- TOTALES ---
			// Usamos una tabla al final para alinear a la derecha
			Table tFin = new Table();
			tFin.Columns.Add(new TableColumn() { Width = new GridLength(70, GridUnitType.Star) }); // Espacio vacio
			tFin.Columns.Add(new TableColumn() { Width = new GridLength(30, GridUnitType.Star) }); // Totales
			TableRowGroup rgFin = new TableRowGroup();
			TableRow rFin = new TableRow();

			StackPanel pnlTotales = new StackPanel() { Margin = new Thickness(0, 10, 0, 0) };
			// Calculo de IVA (Simulado para visualización)
			decimal neto = total / 1.21m;
			decimal iva = total - neto;

			pnlTotales.Children.Add(FilaTotal("Subtotal:", neto));
			pnlTotales.Children.Add(FilaTotal("IVA 21%:", iva));

			TextBlock txtTotal = new TextBlock() { Text = $"TOTAL: {total:C2}", FontWeight = FontWeights.Black, FontSize = 16, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 5, 0, 0) };
			pnlTotales.Children.Add(txtTotal);

			rFin.Cells.Add(new TableCell()); // Celda vacía izq
			rFin.Cells.Add(new TableCell(new BlockUIContainer(pnlTotales)));
			rgFin.Rows.Add(rFin);
			tFin.RowGroups.Add(rgFin);
			doc.Blocks.Add(tFin);

			// --- PIE FISCAL Y LEGALES ---
			if (!string.IsNullOrEmpty(pieFiscal))
			{
				Border boxCae = new Border() { BorderBrush = Brushes.Gray, BorderThickness = new Thickness(1), Padding = new Thickness(5), Margin = new Thickness(0, 20, 0, 0), HorizontalAlignment = HorizontalAlignment.Right, Width = 300 };
				boxCae.Child = new TextBlock() { Text = pieFiscal, FontWeight = FontWeights.Bold, TextAlignment = TextAlignment.Center };
				doc.Blocks.Add(new BlockUIContainer(boxCae));
			}

			Paragraph pLegal = new Paragraph() { Margin = new Thickness(0, 20, 0, 0), FontSize = 8, Foreground = Brushes.Gray, TextAlignment = TextAlignment.Center };
			pLegal.Inlines.Add(new Run("Comprobante Autorizado. Esta factura se considera válida una vez obtenido el CAE."));
			doc.Blocks.Add(pLegal);
		}

		// --- HELPERS VISUALES ---
		private static TextBlock CrearLineaDato(string label, string valor)
		{
			TextBlock tb = new TextBlock();
			tb.Inlines.Add(new Run(label + " ") { FontWeight = FontWeights.Bold });
			tb.Inlines.Add(new Run(valor));
			return tb;
		}

		private static TableCell CeldaGrilla(string texto, bool bold = false, TextAlignment align = TextAlignment.Left)
		{
			return new TableCell(new Paragraph(new Run(texto)) { TextAlignment = align, FontWeight = bold ? FontWeights.Bold : FontWeights.Normal }) { Padding = new Thickness(5) };
		}

		private static Grid FilaTotal(string label, decimal val)
		{
			Grid g = new Grid();
			g.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Star) });
			g.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Star) });
			g.Children.Add(new TextBlock() { Text = label, FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Right });
			TextBlock v = new TextBlock() { Text = val.ToString("C2"), HorizontalAlignment = HorizontalAlignment.Right };
			Grid.SetColumn(v, 1);
			g.Children.Add(v);
			return g;
		}
	}
}