using Microsoft.Win32;
using OfficeOpenXml;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SchettiniGestion;
using System.Globalization;

namespace SchettiniGestion.WPF
{
    public partial class ProductosControl : UserControl
    {
        private DataTable _dtProductos;

        // Columnas en el orden de la plantilla
        private static readonly string[] COLS_HEADER = {
            "CODIGO", "CODIGO_BARRAS", "DESCRIPCION", "CATEGORIA",
            "SUB_RUBRO", "MARCA", "PROVEEDOR", "COSTO", "PRECIO_VENTA", "STOCK"
        };

        public ProductosControl()
        {
            InitializeComponent();
        }

        private void ProductosControl_Loaded(object sender, RoutedEventArgs e)
        {
            CargarProductos();
        }

        private void CargarProductos()
        {
            try
            {
                _dtProductos = DatabaseService.GetProductos("");
                EnriquecerColumnasProductos(_dtProductos);
                dgvProductos.ItemsSource = _dtProductos.DefaultView;
                AplicarFiltro();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al cargar lista: " + ex.Message);
            }
        }

        private static void EnriquecerColumnasProductos(DataTable dt)
        {
            if (dt == null) return;

            if (!dt.Columns.Contains("CostoCompraFinal"))
                dt.Columns.Add("CostoCompraFinal", typeof(decimal));

            foreach (DataRow row in dt.Rows)
            {
                decimal costo = row["PrecioCosto"] != DBNull.Value ? Convert.ToDecimal(row["PrecioCosto"]) : 0m;
                decimal imp = row.Table.Columns.Contains("ImpuestoInterno") && row["ImpuestoInterno"] != DBNull.Value
                    ? Convert.ToDecimal(row["ImpuestoInterno"]) : 0m;
                bool conIva = row.Table.Columns.Contains("CostoIncluyeIva") && row["CostoIncluyeIva"] != DBNull.Value
                    && Convert.ToBoolean(row["CostoIncluyeIva"]);
                string iva = row["TipoIVA"]?.ToString() ?? "21";
                row["CostoCompraFinal"] = DatabaseService.CalcularCostoCompraFinal(
                    costo, conIva, DatabaseService.ParseIvaPct(iva), imp);
            }
        }

        private void AplicarFiltro()
        {
            if (_dtProductos == null) return;
            string t = (txtFiltro?.Text ?? "").Trim();
            if (string.IsNullOrEmpty(t))
            {
                _dtProductos.DefaultView.RowFilter = "";
                return;
            }
            string esc = t.Replace("'", "''");
            var sb = new StringBuilder();
            string[] cols = { "Codigo", "CodigoBarra", "Descripcion", "Categoria", "SubRubro", "Marca", "Proveedor" };
            foreach (string col in cols)
            {
                if (_dtProductos.Columns.Contains(col))
                {
                    if (sb.Length > 0) sb.Append(" OR ");
                    sb.Append($"({col} IS NOT NULL AND {col} LIKE '%{esc}%')");
                }
            }
            _dtProductos.DefaultView.RowFilter = sb.Length > 0 ? sb.ToString() : "";
        }

        private void txtFiltro_TextChanged(object sender, TextChangedEventArgs e) => AplicarFiltro();

        private void btnNuevo_Click(object sender, RoutedEventArgs e)
        {
            var modal = new ProductoModalWindow(0, false, CargarProductos);
            modal.Owner = Window.GetWindow(this);
            modal.ShowDialog();
        }

        private void dgvProductos_SelectionChanged(object sender, SelectionChangedEventArgs e) { }

        private void dgvProductos_MouseDoubleClick(object sender, MouseButtonEventArgs e) => AbrirEditar();

        private void MenuItemEditar_Click(object sender, RoutedEventArgs e) => AbrirEditar();

        private void MenuItemDuplicar_Click(object sender, RoutedEventArgs e) => AbrirDuplicar();

        private void AbrirEditar()
        {
            if (dgvProductos.SelectedItem is DataRowView row)
            {
                int id = Convert.ToInt32(row["ProductoID"]);
                var modal = new ProductoModalWindow(id, false, CargarProductos);
                modal.Owner = Window.GetWindow(this);
                modal.ShowDialog();
            }
        }

        private void AbrirDuplicar()
        {
            if (dgvProductos.SelectedItem is DataRowView row)
            {
                int id = Convert.ToInt32(row["ProductoID"]);
                var modal = new ProductoModalWindow(id, true, CargarProductos);
                modal.Owner = Window.GetWindow(this);
                modal.ShowDialog();
            }
        }

        // ── PLANTILLA CSV ─────────────────────────────────────────────────────

        private void btnDescargarPlantilla_Click(object sender, RoutedEventArgs e)
        {
            var sfd = new SaveFileDialog
            {
                Filter = "CSV (separado por punto y coma)|*.csv",
                FileName = "Plantilla_Carga_Productos.csv"
            };
            if (sfd.ShowDialog() != true) return;
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine(string.Join(";", COLS_HEADER));
                sb.AppendLine("COCA15;779123456789;Coca Cola 1.5 Litros;Bebidas;Gaseosas;Coca-Cola;Coca-Cola;1000;1500;50");
                sb.AppendLine("PAN001;;Pan Frances Kg;Almacen;Panaderia;Varios;;800;1200;10");
                File.WriteAllText(sfd.FileName, sb.ToString(), new UTF8Encoding(true));
                MessageBox.Show("Plantilla CSV guardada.\n\nAbrila con Excel, completá tus productos y guardá como CSV UTF-8.",
                    "Plantilla guardada", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex) { MessageBox.Show("Error: " + ex.Message); }
        }

        // ── PLANTILLA EXCEL ───────────────────────────────────────────────────

        private void btnDescargarPlantillaXlsx_Click(object sender, RoutedEventArgs e)
        {
            var sfd = new SaveFileDialog
            {
                Filter = "Excel (*.xlsx)|*.xlsx",
                FileName = "Plantilla_Carga_Productos.xlsx"
            };
            if (sfd.ShowDialog() != true) return;
            try
            {
                ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
                using (var pkg = new ExcelPackage())
                {
                    var ws = pkg.Workbook.Worksheets.Add("Productos");

                    // Encabezados con formato
                    for (int c = 0; c < COLS_HEADER.Length; c++)
                    {
                        var cell = ws.Cells[1, c + 1];
                        cell.Value = COLS_HEADER[c];
                        cell.Style.Font.Bold = true;
                        cell.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                        cell.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(0x1E, 0x88, 0xE5));
                        cell.Style.Font.Color.SetColor(System.Drawing.Color.White);
                    }

                    // Filas de ejemplo
                    ws.Cells[2, 1].Value = "COCA15";
                    ws.Cells[2, 2].Value = "779123456789";
                    ws.Cells[2, 3].Value = "Coca Cola 1.5 Litros";
                    ws.Cells[2, 4].Value = "Bebidas";
                    ws.Cells[2, 5].Value = "Gaseosas";
                    ws.Cells[2, 6].Value = "Coca-Cola";
                    ws.Cells[2, 7].Value = "Coca-Cola";
                    ws.Cells[2, 8].Value = 1000;
                    ws.Cells[2, 9].Value = 1500;
                    ws.Cells[2, 10].Value = 50;

                    ws.Cells[3, 1].Value = "PAN001";
                    ws.Cells[3, 3].Value = "Pan Frances Kg";
                    ws.Cells[3, 4].Value = "Almacen";
                    ws.Cells[3, 5].Value = "Panaderia";
                    ws.Cells[3, 8].Value = 800;
                    ws.Cells[3, 9].Value = 1200;
                    ws.Cells[3, 10].Value = 10;

                    // Anchos fijos (AutoFitColumns requiere EPPlus.System.Drawing compatible)
                    int[] colWidths = { 14, 18, 40, 16, 16, 14, 20, 12, 14, 10 };
                    for (int c = 0; c < colWidths.Length; c++)
                        ws.Column(c + 1).Width = colWidths[c];

                    pkg.SaveAs(new FileInfo(sfd.FileName));
                }
                MessageBox.Show("Plantilla Excel guardada.\n\nCompletá tus productos en las filas siguientes (fila 1 = encabezados, no la borres).",
                    "Plantilla guardada", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex) { MessageBox.Show("Error al generar plantilla Excel: " + ex.Message); }
        }

        // ── IMPORTAR (CSV o XLSX) ─────────────────────────────────────────────

        private void btnImportarExcel_Click(object sender, RoutedEventArgs e)
        {
            var ofd = new OpenFileDialog
            {
                Filter = "Archivos de productos|*.csv;*.xlsx|CSV (*.csv)|*.csv|Excel (*.xlsx)|*.xlsx",
                Title = "Seleccionar archivo de productos"
            };
            if (ofd.ShowDialog() != true) return;

            string ext = Path.GetExtension(ofd.FileName).ToLowerInvariant();
            try
            {
                if (ext == ".xlsx")
                    ProcesarImportacionExcel(ofd.FileName);
                else
                    ProcesarImportacionCSV(ofd.FileName);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al importar: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ── CSV ───────────────────────────────────────────────────────────────

        private void ProcesarImportacionCSV(string ruta)
        {
            var lineas = File.ReadAllLines(ruta, DetectarEncoding(ruta));
            if (lineas.Length < 2)
            {
                MessageBox.Show("El archivo CSV está vacío o solo tiene encabezado.", "Aviso",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Detectar separador automáticamente desde la primera fila
            char sep = DetectarSeparador(lineas[0]);
            int[] mapa = MapearColumnas(SplitCsv(lineas[0], sep));

            int importados = 0, actualizados = 0, errores = 0;
            var mensajesError = new List<string>();

            for (int i = 1; i < lineas.Length; i++)
            {
                string linea = lineas[i].Trim();
                if (string.IsNullOrWhiteSpace(linea)) continue;

                try
                {
                    var datos = SplitCsv(linea, sep);
                    bool nuevo = ProcesarFila(datos, mapa);
                    if (nuevo) importados++; else actualizados++;
                }
                catch (Exception ex)
                {
                    errores++;
                    if (mensajesError.Count < 5)
                        mensajesError.Add($"Fila {i + 1}: {ex.Message}");
                }
            }

            MostrarResultado(importados, actualizados, errores, mensajesError);
            CargarProductos();
        }

        // ── EXCEL ─────────────────────────────────────────────────────────────

        private void ProcesarImportacionExcel(string ruta)
        {
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            using (var pkg = new ExcelPackage(new FileInfo(ruta)))
            {
                var ws = pkg.Workbook.Worksheets.FirstOrDefault();
                if (ws == null)
                {
                    MessageBox.Show("El archivo Excel no tiene hojas.", "Aviso",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                int totalRows = ws.Dimension?.End.Row ?? 0;
                int totalCols = ws.Dimension?.End.Column ?? 0;
                if (totalRows < 2)
                {
                    MessageBox.Show("El archivo Excel está vacío o solo tiene encabezado.", "Aviso",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Leer encabezados desde fila 1
                var headers = new List<string>();
                for (int c = 1; c <= totalCols; c++)
                    headers.Add(ws.Cells[1, c].Text?.Trim().ToUpper() ?? "");

                int[] mapa = MapearColumnas(headers);

                int importados = 0, actualizados = 0, errores = 0;
                var mensajesError = new List<string>();

                for (int r = 2; r <= totalRows; r++)
                {
                    // Verificar que la fila no esté vacía
                    bool filaVacia = true;
                    var datos = new List<string>();
                    for (int c = 1; c <= totalCols; c++)
                    {
                        string val = ws.Cells[r, c].Text?.Trim() ?? "";
                        datos.Add(val);
                        if (!string.IsNullOrEmpty(val)) filaVacia = false;
                    }
                    if (filaVacia) continue;

                    try
                    {
                        bool nuevo = ProcesarFila(datos, mapa);
                        if (nuevo) importados++; else actualizados++;
                    }
                    catch (Exception ex)
                    {
                        errores++;
                        if (mensajesError.Count < 5)
                            mensajesError.Add($"Fila {r}: {ex.Message}");
                    }
                }

                MostrarResultado(importados, actualizados, errores, mensajesError);
                CargarProductos();
            }
        }

        // ── LÓGICA COMÚN ─────────────────────────────────────────────────────

        /// <summary>Devuelve índices de columnas según el encabezado. -1 si no existe.</summary>
        private static int[] MapearColumnas(IList<string> headers)
        {
            // [0]=CODIGO [1]=CB [2]=DESC [3]=CAT [4]=SUB [5]=MARCA [6]=PROV [7]=COSTO [8]=VENTA [9]=STOCK
            string[] buscados = { "CODIGO", "CODIGO_BARRAS", "DESCRIPCION", "CATEGORIA",
                                   "SUB_RUBRO", "MARCA", "PROVEEDOR", "COSTO", "PRECIO_VENTA", "STOCK" };
            // Alias alternativos para mayor tolerancia
            string[][] alias = {
                new[]{ "CODIGO", "COD", "CODE" },
                new[]{ "CODIGO_BARRAS", "CODIGOBARRA", "BARCODE", "EAN", "CB" },
                new[]{ "DESCRIPCION", "DESCRIPCIÓN", "NOMBRE", "DESCRIPTION", "PRODUCT" },
                new[]{ "CATEGORIA", "CATEGORÍA", "RUBRO", "CATEGORY" },
                new[]{ "SUB_RUBRO", "SUBRUBRO", "SUBCATEGORIA", "SUBCATEGORÍA" },
                new[]{ "MARCA", "BRAND" },
                new[]{ "PROVEEDOR", "PROVIDER", "SUPPLIER" },
                new[]{ "COSTO", "PRECIO_COSTO", "COST", "PRECIO COSTO" },
                new[]{ "PRECIO_VENTA", "PRECIOVENTA", "VENTA", "PRICE", "PRECIO" },
                new[]{ "STOCK", "CANTIDAD", "QTY", "QUANTITY" }
            };

            var norm = headers.Select(h => h.ToUpper().Replace(" ", "_")).ToList();
            var resultado = new int[buscados.Length];
            for (int i = 0; i < buscados.Length; i++)
            {
                resultado[i] = -1;
                foreach (string a in alias[i])
                {
                    int idx = norm.IndexOf(a);
                    if (idx >= 0) { resultado[i] = idx; break; }
                }
            }
            return resultado;
        }

        private static string Get(IList<string> datos, int[] mapa, int campo)
        {
            int idx = mapa[campo];
            if (idx < 0 || idx >= datos.Count) return "";
            return datos[idx].Trim();
        }

        /// <summary>Procesa una fila. Devuelve true si es nuevo, false si se actualizó.</summary>
        private static bool ProcesarFila(IList<string> datos, int[] mapa)
        {
            string codigo = Get(datos, mapa, 0);
            string desc   = Get(datos, mapa, 2);
            if (string.IsNullOrWhiteSpace(codigo))
                throw new Exception("El campo CODIGO está vacío.");

            string codigoBarra = Get(datos, mapa, 1);
            if (string.IsNullOrEmpty(codigoBarra)) codigoBarra = codigo;

            string categoria = Get(datos, mapa, 3);
            string subRubro  = Get(datos, mapa, 4);
            string marca     = Get(datos, mapa, 5);
            string proveedor = Get(datos, mapa, 6);

            decimal costo = ParseDecimal(Get(datos, mapa, 7));
            decimal venta = ParseDecimal(Get(datos, mapa, 8));
            int stock     = (int)ParseDecimal(Get(datos, mapa, 9));

            decimal ganancia = costo > 0 ? Math.Round((venta - costo) / costo * 100, 2) : 0;

            int idProd = 0;
            var existente = DatabaseService.BuscarProductoPorCodigoExacto(codigo);
            if (existente != null) idProd = Convert.ToInt32(existente["ProductoID"]);

            if (string.IsNullOrWhiteSpace(desc))
                throw new Exception("El campo DESCRIPCION está vacío.");

            DatabaseService.GuardarProducto(idProd, codigo, codigoBarra, desc,
                categoria, subRubro, marca, proveedor,
                "21.0", costo, ganancia, 0, venta, stock, null);

            return idProd == 0;
        }

        private static void MostrarResultado(int importados, int actualizados, int errores, List<string> mensajesError)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"✅ Nuevos productos creados: {importados}");
            sb.AppendLine($"🔄 Productos actualizados:   {actualizados}");
            if (errores > 0)
            {
                sb.AppendLine($"❌ Filas con error:          {errores}");
                sb.AppendLine();
                sb.AppendLine("Primeros errores:");
                foreach (var m in mensajesError) sb.AppendLine("  • " + m);
            }
            var icon = errores > 0 ? MessageBoxImage.Warning : MessageBoxImage.Information;
            MessageBox.Show(sb.ToString(), "Resultado de importación", MessageBoxButton.OK, icon);
        }

        // ── HELPERS ───────────────────────────────────────────────────────────

        private static decimal ParseDecimal(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return 0;
            // Normalizar: eliminar separadores de miles y convertir coma decimal a punto
            s = s.Trim().Replace("$", "").Replace(" ", "");
            // Si tiene coma Y punto: el último es el decimal
            int lastComma = s.LastIndexOf(',');
            int lastDot   = s.LastIndexOf('.');
            if (lastComma > lastDot)
                s = s.Replace(".", "").Replace(",", ".");  // formato europeo 1.234,56
            else
                s = s.Replace(",", "");                    // formato anglosajón 1,234.56
            decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal result);
            return result;
        }

        private static char DetectarSeparador(string linea)
        {
            int puntoYComa = linea.Count(c => c == ';');
            int coma       = linea.Count(c => c == ',');
            int tab        = linea.Count(c => c == '\t');
            if (tab > puntoYComa && tab > coma) return '\t';
            if (puntoYComa >= coma) return ';';
            return ',';
        }

        private static List<string> SplitCsv(string linea, char sep)
        {
            var result = new List<string>();
            bool inQuotes = false;
            var current = new StringBuilder();
            for (int i = 0; i < linea.Length; i++)
            {
                char c = linea[i];
                if (c == '"')
                {
                    if (inQuotes && i + 1 < linea.Length && linea[i + 1] == '"')
                    { current.Append('"'); i++; }
                    else inQuotes = !inQuotes;
                }
                else if (c == sep && !inQuotes)
                { result.Add(current.ToString()); current.Clear(); }
                else current.Append(c);
            }
            result.Add(current.ToString());
            return result;
        }

        private static Encoding DetectarEncoding(string ruta)
        {
            byte[] bom = new byte[4];
            using (var fs = new FileStream(ruta, FileMode.Open, FileAccess.Read))
                fs.Read(bom, 0, 4);
            if (bom[0] == 0xEF && bom[1] == 0xBB && bom[2] == 0xBF) return new UTF8Encoding(true);
            if (bom[0] == 0xFF && bom[1] == 0xFE) return Encoding.Unicode;
            if (bom[0] == 0xFE && bom[1] == 0xFF) return Encoding.BigEndianUnicode;
            // Sin BOM: asumir UTF-8, si falla caer a Latin-1 (iso-8859-1)
            try
            {
                File.ReadAllText(ruta, Encoding.UTF8);
                return Encoding.UTF8;
            }
            catch { return Encoding.GetEncoding(1252); }
        }
    }
}
