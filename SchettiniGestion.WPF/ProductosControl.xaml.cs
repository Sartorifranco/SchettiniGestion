using Microsoft.Win32;
using OfficeOpenXml;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using SchettiniGestion;
using System.Globalization;

namespace SchettiniGestion.WPF
{
    public partial class ProductosControl : UserControl, ISincronizableEnRed
    {
        private DataTable _dtProductos;
        private readonly DispatcherTimer _filtroTimer;
        private int _versionCarga;

        // Columnas en el orden de la plantilla (alta masiva)
        private static readonly string[] COLS_HEADER = {
            "Codigo", "CodigoBarras", "CodigoExterno", "Descripcion", "Categoria",
            "SubRubro", "Marca", "Proveedor", "TipoMoneda", "CostoCompra", "% IVA",
            "ImpuestoInterno", "CostoIncluyeIVA", "Stock", "StockMinimo", "StockIdeal",
            "PermitirModificarPrecioVenta", "Stockeable", "PermitirStockNegativo",
            "UsaVariantes", "EsCombo", "Activo", "PrecioVenta", "VarianteColor",
            "VarianteTalle", "VarianteUnidadMedida", "CobraIvaAlCliente", "ImagenPath"
        };

        // Columnas exportación / importación de actualización masiva
        private static readonly string[] COLS_ACTUALIZACION = {
            "ProductoID", "Codigo", "CodigoBarras", "CodigoExterno", "Descripcion", "Categoria",
            "SubRubro", "Marca", "Proveedor", "TipoMoneda", "CostoCompra", "% IVA",
            "ImpuestoInterno", "CostoIncluyeIva", "Stock", "StockMinimo", "StockIdeal",
            "PermitirModificarPrecioVenta", "EsStockeable", "PermitirStockNegativo",
            "UsaVariantes", "EsCombo", "Activo", "PrecioVenta", "VarianteColor",
            "VarianteTalle", "VarianteUnidadMedida", "CobraIvaAlCliente", "ImagenPath"
        };

        public ProductosControl()
        {
            InitializeComponent();
            _filtroTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
            _filtroTimer.Tick += (_, __) =>
            {
                _filtroTimer.Stop();
                AplicarFiltro();
            };
        }

        private bool _inicializado;

        private void ProductosControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (_inicializado) return;
            _inicializado = true;
            CargarProductos();
        }

        public void AplicarCambioRed(string entidad)
        {
            if (!_inicializado) return;
            if (!string.IsNullOrEmpty(entidad) && entidad != "Productos" && entidad != "ListasPrecios")
                return;
            if (RedSyncWatcher.HayVentanaVisible<ProductoModalWindow>())
                return;
            CargarProductos(silencioso: true);
        }

        private async void CargarProductos(bool silencioso = false)
        {
            int version = ++_versionCarga;
            bool incluirInactivos = chkMostrarInactivos?.IsChecked == true;
            if (!silencioso && pnlCargandoProductos != null) pnlCargandoProductos.Visibility = Visibility.Visible;
            try
            {
                var datos = await Task.Run(() =>
                {
                    var dt = DatabaseService.GetProductos("", incluirInactivos);
                    EnriquecerColumnasProductos(dt);
                    return dt;
                });
                if (version != _versionCarga || !IsLoaded) return;

                _dtProductos = datos;
                dgvProductos.ItemsSource = _dtProductos.DefaultView;
                AplicarFiltro();
            }
            catch (Exception ex)
            {
                if (version == _versionCarga)
                    ModernMessageBox.Show("Error al cargar la lista de productos:\n" + ex.Message,
                        "Productos", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                if (version == _versionCarga && !silencioso && pnlCargandoProductos != null)
                    pnlCargandoProductos.Visibility = Visibility.Collapsed;
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

        private void txtFiltro_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_filtroTimer == null) return;
            _filtroTimer.Stop();
            _filtroTimer.Start();
        }

        private void chkMostrarInactivos_Checked(object sender, RoutedEventArgs e) => CargarProductos();

        private void btnNuevo_Click(object sender, RoutedEventArgs e)
        {
            var modal = new ProductoModalWindow(0, false, () => CargarProductos());
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
                var modal = new ProductoModalWindow(id, false, () => CargarProductos());
                modal.Owner = Window.GetWindow(this);
                modal.ShowDialog();
            }
        }

        private void AbrirDuplicar()
        {
            if (dgvProductos.SelectedItem is DataRowView row)
            {
                int id = Convert.ToInt32(row["ProductoID"]);
                var modal = new ProductoModalWindow(id, true, () => CargarProductos());
                modal.Owner = Window.GetWindow(this);
                modal.ShowDialog();
            }
        }

        // ── EXPORTAR PRODUCTOS (actualización masiva) ─────────────────────────

        private void btnExportarProductos_Click(object sender, RoutedEventArgs e)
        {
            var sfd = new SaveFileDialog
            {
                Filter = "Excel (*.xlsx)|*.xlsx|CSV (*.csv)|*.csv",
                FileName = $"Productos_Actualizacion_{DateTime.Now:yyyyMMdd}.xlsx"
            };
            if (sfd.ShowDialog() != true) return;

            try
            {
                var dt = DatabaseService.ObtenerProductosParaExportacionMasiva();
                if (dt == null || dt.Rows.Count == 0)
                {
                    MessageBox.Show("No hay productos para exportar.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                string ext = Path.GetExtension(sfd.FileName).ToLowerInvariant();
                if (ext == ".csv")
                    ExportarActualizacionCsv(sfd.FileName, dt);
                else
                    ExportarActualizacionExcel(sfd.FileName, dt);

                MessageBox.Show($"Se exportaron {dt.Rows.Count} producto(s).\n\nModifique costos y estados en el archivo e importe con \"Importar Actualización\".",
                    "Exportación completada", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al exportar: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static void ExportarActualizacionCsv(string ruta, DataTable dt)
        {
            var sb = new StringBuilder();
            sb.AppendLine(string.Join(";", COLS_ACTUALIZACION));
            foreach (DataRow row in dt.Rows)
            {
                var vals = new List<string>();
                foreach (string col in COLS_ACTUALIZACION)
                {
                    string v = row.Table.Columns.Contains(col) && row[col] != DBNull.Value ? row[col].ToString() : "";
                    if (v.Contains(";") || v.Contains("\""))
                        v = "\"" + v.Replace("\"", "\"\"") + "\"";
                    vals.Add(v);
                }
                sb.AppendLine(string.Join(";", vals));
            }
            File.WriteAllText(ruta, sb.ToString(), new UTF8Encoding(true));
        }

        private static void ExportarActualizacionExcel(string ruta, DataTable dt)
        {
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            using (var pkg = new ExcelPackage())
            {
                var ws = pkg.Workbook.Worksheets.Add("Productos");
                for (int c = 0; c < COLS_ACTUALIZACION.Length; c++)
                {
                    var cell = ws.Cells[1, c + 1];
                    cell.Value = COLS_ACTUALIZACION[c];
                    cell.Style.Font.Bold = true;
                    cell.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                    cell.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(0x1E, 0x88, 0xE5));
                    cell.Style.Font.Color.SetColor(System.Drawing.Color.White);
                }

                int rowIdx = 2;
                foreach (DataRow row in dt.Rows)
                {
                    for (int c = 0; c < COLS_ACTUALIZACION.Length; c++)
                    {
                        string col = COLS_ACTUALIZACION[c];
                        object val = row.Table.Columns.Contains(col) && row[col] != DBNull.Value ? row[col] : null;
                        ws.Cells[rowIdx, c + 1].Value = val;
                    }
                    rowIdx++;
                }

                for (int c = 0; c < COLS_ACTUALIZACION.Length; c++)
                    ws.Column(c + 1).Width = c == 4 ? 40 : 18;

                ws.View.FreezePanes(2, 1);
                pkg.SaveAs(new FileInfo(ruta));
            }
        }

        // ── IMPORTAR ACTUALIZACIÓN MASIVA ─────────────────────────────────────

        private void btnImportarActualizacion_Click(object sender, RoutedEventArgs e)
        {
            var ofd = new OpenFileDialog
            {
                Filter = "Archivos de actualización|*.xlsx;*.csv|Excel (*.xlsx)|*.xlsx|CSV (*.csv)|*.csv",
                Title = "Importar actualización masiva de productos"
            };
            if (ofd.ShowDialog() != true) return;

            try
            {
                List<DatabaseService.ProductoActualizacionMasivaItem> filas;
                string ext = Path.GetExtension(ofd.FileName).ToLowerInvariant();
                if (ext == ".xlsx")
                    filas = LeerFilasActualizacionExcel(ofd.FileName);
                else
                    filas = LeerFilasActualizacionCsv(ofd.FileName);

                if (filas.Count == 0)
                {
                    MessageBox.Show("El archivo no contiene filas de datos.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (MessageBox.Show(
                    $"Se procesarán {filas.Count} fila(s).\n\nLos cambios se aplican en una única transacción: si alguna fila falla, no se guardará ningún cambio.\n\n¿Continuar?",
                    "Confirmar importación", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                    return;

                var resultado = DatabaseService.ImportarActualizacionMasivaProductos(filas);
                MostrarResultadoActualizacion(resultado);
                if (resultado.Exitoso)
                    CargarProductos();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al importar: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private List<DatabaseService.ProductoActualizacionMasivaItem> LeerFilasActualizacionCsv(string ruta)
        {
            var filas = new List<DatabaseService.ProductoActualizacionMasivaItem>();
            var lineas = File.ReadAllLines(ruta, DetectarEncoding(ruta));
            if (lineas.Length < 2) return filas;

            char sep = DetectarSeparador(lineas[0]);
            int[] mapa = MapearColumnasActualizacion(SplitCsv(lineas[0], sep));

            for (int i = 1; i < lineas.Length; i++)
            {
                string linea = lineas[i].Trim();
                if (string.IsNullOrWhiteSpace(linea)) continue;
                var datos = SplitCsv(linea, sep);
                var item = ConstruirItemActualizacion(datos, mapa, i + 1);
                if (item != null) filas.Add(item);
            }
            return filas;
        }

        private List<DatabaseService.ProductoActualizacionMasivaItem> LeerFilasActualizacionExcel(string ruta)
        {
            var filas = new List<DatabaseService.ProductoActualizacionMasivaItem>();
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            using (var pkg = new ExcelPackage(new FileInfo(ruta)))
            {
                var ws = pkg.Workbook.Worksheets.FirstOrDefault();
                if (ws == null) return filas;

                int totalRows = ws.Dimension?.End.Row ?? 0;
                int totalCols = ws.Dimension?.End.Column ?? 0;
                if (totalRows < 2) return filas;

                var headers = new List<string>();
                for (int c = 1; c <= totalCols; c++)
                    headers.Add(ws.Cells[1, c].Text?.Trim() ?? "");

                int[] mapa = MapearColumnasActualizacion(headers);

                for (int r = 2; r <= totalRows; r++)
                {
                    bool filaVacia = true;
                    var datos = new List<string>();
                    for (int c = 1; c <= totalCols; c++)
                    {
                        string val = ws.Cells[r, c].Text?.Trim() ?? "";
                        datos.Add(val);
                        if (!string.IsNullOrEmpty(val)) filaVacia = false;
                    }
                    if (filaVacia) continue;

                    var item = ConstruirItemActualizacion(datos, mapa, r);
                    if (item != null) filas.Add(item);
                }
            }
            return filas;
        }

        /// <summary>Mapeo completo de campos de ficha. Indices: ProductoID + COLS_HEADER.</summary>
        private static int[] MapearColumnasActualizacion(IList<string> headers)
        {
            string[][] alias = {
                new[]{ "PRODUCTOID", "ID", "ID_PRODUCTO" },
                new[]{ "CODIGO", "COD", "CODE" },
                new[]{ "CODIGOBARRAS", "CODIGO_BARRAS", "CODIGOBARRA", "CODIGO_BARRA", "BARCODE", "EAN", "CB" },
                new[]{ "CODIGOEXTERNO", "CODIGO_EXTERNO", "COD_EXT" },
                new[]{ "DESCRIPCION", "DESCRIPCIÓN", "NOMBRE", "PRODUCTO", "PRODUCT" },
                new[]{ "CATEGORIA", "CATEGORÍA", "RUBRO", "CATEGORY" },
                new[]{ "SUBRUBRO", "SUB_RUBRO", "SUBCATEGORIA", "SUBCATEGORÍA", "SUB_RUBRO" },
                new[]{ "MARCA", "BRAND" },
                new[]{ "PROVEEDOR", "PROVIDER", "SUPPLIER" },
                new[]{ "TIPOMONEDA", "TIPO_MONEDA", "MONEDA", "CURRENCY" },
                new[]{ "COSTOCOMPRA", "COSTO_COMPRA", "COSTO_DE_COMPRA", "COSTO", "PRECIO_COSTO", "PRECIO COSTO", "COSTO COMPRA" },
                new[]{ "%IVA", "%_IVA", "IVA", "PORCENTAJE_IVA", "PORC_IVA", "TIPOIVA", "TIPO_IVA" },
                new[]{ "IMPUESTOINTERNO", "IMPUESTO_INTERNO", "IMPUESTO", "IMP_INTERNO", "IMPUESTO INTERNO" },
                new[]{ "COSTOINCLUYEIVA", "COSTO_INCLUYE_IVA", "INCLUYE_IVA", "COSTO INCLUYE IVA" },
                new[]{ "STOCK", "STOCKACTUAL", "STOCK_ACTUAL", "CANTIDAD", "QTY", "QUANTITY" },
                new[]{ "STOCKMINIMO", "STOCK_MINIMO", "MINIMO", "MÍNIMO" },
                new[]{ "STOCKIDEAL", "STOCK_IDEAL", "IDEAL" },
                new[]{ "PERMITIRMODIFICARPRECIOVENTA", "PERMITIR_MODIFICAR_PRECIO_VENTA", "PERMITE_MODIFICAR_PRECIO_VENTA", "MODIFICA_PRECIO_VENTA" },
                new[]{ "ESSTOCKEABLE", "STOCKEABLE", "ES_STOCKEABLE", "CONTROLA_STOCK" },
                new[]{ "PERMITIRSTOCKNEGATIVO", "PERMITIR_STOCK_NEGATIVO", "VENDEENNEGATIVO", "VENDE_EN_NEGATIVO", "STOCK_NEGATIVO", "ACEPTA_STOCK_NEGATIVO", "VENDE EN NEGATIVO" },
                new[]{ "USAVARIANTES", "USA_VARIANTES", "VARIANTES" },
                new[]{ "ESCOMBO", "ES_COMBO", "COMBO" },
                new[]{ "ACTIVO", "HABILITADO", "EN_CATALOGO" },
                new[]{ "PRECIOVENTA", "PRECIO_VENTA", "VENTA", "PRICE", "PRECIO" },
                new[]{ "VARIANTECOLOR", "VARIANTE_COLOR", "COLOR" },
                new[]{ "VARIANTETALLE", "VARIANTE_TALLE", "TALLE" },
                new[]{ "VARIANTEUNIDADMEDIDA", "VARIANTE_UNIDAD_MEDIDA", "UNIDAD_MEDIDA", "UNIDAD" },
                new[]{ "COBRAIVAALCLIENTE", "COBRA_IVA_AL_CLIENTE", "IVA_AL_CLIENTE" },
                new[]{ "IMAGENPATH", "IMAGEN_PATH", "IMAGEN", "RUTA_IMAGEN" }
            };

            var norm = headers.Select(NormalizarHeader).ToList();
            var resultado = new int[alias.Length];
            for (int i = 0; i < alias.Length; i++)
            {
                resultado[i] = -1;
                foreach (string a in alias[i])
                {
                    string na = NormalizarHeader(a);
                    int idx = norm.FindIndex(h => h == na || h.Replace("_", "") == na.Replace("_", ""));
                    if (idx >= 0) { resultado[i] = idx; break; }
                }
            }
            return resultado;
        }

        private static string NormalizarHeader(string header)
        {
            return (header ?? "")
                .Trim()
                .ToUpperInvariant()
                .Replace("Á", "A")
                .Replace("É", "E")
                .Replace("Í", "I")
                .Replace("Ó", "O")
                .Replace("Ú", "U")
                .Replace(" ", "_")
                .Replace("-", "_");
        }

        private static DatabaseService.ProductoActualizacionMasivaItem ConstruirItemActualizacion(IList<string> datos, int[] mapa, int numeroFila)
        {
            string idTxt = GetCol(datos, mapa, 0);
            string codigo = GetCol(datos, mapa, 1);

            if (string.IsNullOrWhiteSpace(idTxt) && string.IsNullOrWhiteSpace(codigo))
                return null;

            int? productoId = null;
            if (!string.IsNullOrWhiteSpace(idTxt))
            {
                if (!int.TryParse(idTxt.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int pid) || pid <= 0)
                    throw new Exception($"Fila {numeroFila}: ProductoID inválido '{idTxt}'.");
                productoId = pid;
            }

            var item = new DatabaseService.ProductoActualizacionMasivaItem
            {
                NumeroFila = numeroFila,
                ProductoId = productoId,
                Codigo = HasCol(mapa, 1) ? codigo : null,
                CodigoBarra = GetColOrNull(datos, mapa, 2),
                CodigoExterno = GetColOrNull(datos, mapa, 3),
                Descripcion = GetColOrNull(datos, mapa, 4),
                Categoria = GetColOrNull(datos, mapa, 5),
                SubRubro = GetColOrNull(datos, mapa, 6),
                Marca = GetColOrNull(datos, mapa, 7),
                Proveedor = GetColOrNull(datos, mapa, 8),
                TipoMoneda = GetColOrNull(datos, mapa, 9),
                VarianteColor = GetColOrNull(datos, mapa, 24),
                VarianteTalle = GetColOrNull(datos, mapa, 25),
                VarianteUnidadMedida = GetColOrNull(datos, mapa, 26),
                ImagenPath = GetColOrNull(datos, mapa, 28)
            };

            string costoTxt = GetCol(datos, mapa, 10);
            if (!string.IsNullOrWhiteSpace(costoTxt))
                item.CostoCompra = ParseDecimal(costoTxt);

            string ivaTxt = GetCol(datos, mapa, 11);
            if (!string.IsNullOrWhiteSpace(ivaTxt))
                item.IvaPct = ParseDecimal(ivaTxt.Replace("%", ""));

            string impTxt = GetCol(datos, mapa, 12);
            if (!string.IsNullOrWhiteSpace(impTxt))
                item.ImpuestoInterno = ParseDecimal(impTxt);

            string costoIncluyeIvaTxt = GetCol(datos, mapa, 13);
            if (!string.IsNullOrWhiteSpace(costoIncluyeIvaTxt))
                item.CostoIncluyeIva = ParseSiNoCampo(costoIncluyeIvaTxt, numeroFila, "CostoIncluyeIva");

            string stockTxt = GetCol(datos, mapa, 14);
            if (!string.IsNullOrWhiteSpace(stockTxt))
                item.Stock = ParseDecimal(stockTxt);

            string stockMinTxt = GetCol(datos, mapa, 15);
            if (!string.IsNullOrWhiteSpace(stockMinTxt))
                item.StockMinimo = ParseDecimal(stockMinTxt);

            string stockIdealTxt = GetCol(datos, mapa, 16);
            if (!string.IsNullOrWhiteSpace(stockIdealTxt))
                item.StockIdeal = ParseDecimal(stockIdealTxt);

            string permiteModTxt = GetCol(datos, mapa, 17);
            if (!string.IsNullOrWhiteSpace(permiteModTxt))
                item.PermitirModificarPrecioVenta = ParseSiNoCampo(permiteModTxt, numeroFila, "PermitirModificarPrecioVenta");

            string stockeableTxt = GetCol(datos, mapa, 18);
            if (!string.IsNullOrWhiteSpace(stockeableTxt))
                item.EsStockeable = ParseSiNoCampo(stockeableTxt, numeroFila, "EsStockeable");

            string negativoTxt = GetCol(datos, mapa, 19);
            if (!string.IsNullOrWhiteSpace(negativoTxt))
                item.VendeEnNegativo = ParseSiNoCampo(negativoTxt, numeroFila, "PermitirStockNegativo");

            string usaVarTxt = GetCol(datos, mapa, 20);
            if (!string.IsNullOrWhiteSpace(usaVarTxt))
                item.UsaVariantes = ParseSiNoCampo(usaVarTxt, numeroFila, "UsaVariantes");

            string comboTxt = GetCol(datos, mapa, 21);
            if (!string.IsNullOrWhiteSpace(comboTxt))
                item.EsCombo = ParseSiNoCampo(comboTxt, numeroFila, "EsCombo");

            string activoTxt = GetCol(datos, mapa, 22);
            if (!string.IsNullOrWhiteSpace(activoTxt))
                item.Activo = ParseSiNoCampo(activoTxt, numeroFila, "Activo");

            string precioVentaTxt = GetCol(datos, mapa, 23);
            if (!string.IsNullOrWhiteSpace(precioVentaTxt))
                item.PrecioVenta = ParseDecimal(precioVentaTxt);

            string cobraIvaTxt = GetCol(datos, mapa, 27);
            if (!string.IsNullOrWhiteSpace(cobraIvaTxt))
                item.CobraIvaAlCliente = ParseSiNoCampo(cobraIvaTxt, numeroFila, "CobraIvaAlCliente");

            return item;
        }

        private static bool HasCol(int[] mapa, int campo) => mapa != null && campo >= 0 && campo < mapa.Length && mapa[campo] >= 0;

        private static string GetColOrNull(IList<string> datos, int[] mapa, int campo)
        {
            return HasCol(mapa, campo) ? GetCol(datos, mapa, campo) : null;
        }

        private static bool ParseSiNoCampo(string valor, int numeroFila, string campo)
        {
            if (!DatabaseService.TryParseSiNo(valor, out bool result))
                throw new Exception($"Fila {numeroFila}: {campo} inválido '{valor}' (use SI o NO).");
            return result;
        }

        private static string GetCol(IList<string> datos, int[] mapa, int campo)
        {
            int idx = mapa[campo];
            if (idx < 0 || idx >= datos.Count) return "";
            return datos[idx]?.Trim() ?? "";
        }

        private void MostrarResultadoActualizacion(DatabaseService.ProductoImportacionMasivaResultado resultado)
        {
            ResultadoImportacionWindow modal;
            if (resultado.Exitoso)
            {
                modal = new ResultadoImportacionWindow(0, resultado.Actualizados, 0, resultado.SinCambios, null, false);
            }
            else
            {
                var detalle = new List<string>();
                if (!string.IsNullOrWhiteSpace(resultado.ErrorGeneral))
                    detalle.Add(resultado.ErrorGeneral);
                detalle.AddRange(resultado.Errores);
                modal = new ResultadoImportacionWindow(0, 0, detalle.Count, 0, detalle, true);
            }

            modal.Owner = Window.GetWindow(this);
            modal.ShowDialog();
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
                sb.AppendLine("COCA15;779123456789;;Coca Cola 1.5 Litros;Bebidas;Gaseosas;Coca-Cola;Coca-Cola;ARS;1000;21;0;NO;50;5;20;NO;SI;NO;NO;NO;SI;1500;;;;SI;");
                sb.AppendLine("PAN001;;;Pan Frances Kg;Almacen;Panaderia;Varios;;ARS;800;21;0;NO;10;2;8;NO;SI;NO;NO;NO;SI;1200;;;;SI;");
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
                    object[] ejemplo1 = { "COCA15", "779123456789", "", "Coca Cola 1.5 Litros", "Bebidas", "Gaseosas", "Coca-Cola", "Coca-Cola", "ARS", 1000, 21, 0, "NO", 50, 5, 20, "NO", "SI", "NO", "NO", "NO", "SI", 1500, "", "", "", "SI", "" };
                    object[] ejemplo2 = { "PAN001", "", "", "Pan Frances Kg", "Almacen", "Panaderia", "Varios", "", "ARS", 800, 21, 0, "NO", 10, 2, 8, "NO", "SI", "NO", "NO", "NO", "SI", 1200, "", "", "", "SI", "" };
                    for (int c = 0; c < COLS_HEADER.Length; c++)
                    {
                        ws.Cells[2, c + 1].Value = ejemplo1[c];
                        ws.Cells[3, c + 1].Value = ejemplo2[c];
                    }

                    // Anchos fijos (AutoFitColumns requiere EPPlus.System.Drawing compatible)
                    for (int c = 0; c < COLS_HEADER.Length; c++)
                        ws.Column(c + 1).Width = c == 3 ? 40 : 18;

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

            int errores = 0;
            var mensajesError = new List<string>();
            var filas = new List<DatabaseService.ProductoActualizacionMasivaItem>();

            for (int i = 1; i < lineas.Length; i++)
            {
                string linea = lineas[i].Trim();
                if (string.IsNullOrWhiteSpace(linea)) continue;

                try
                {
                    var datos = SplitCsv(linea, sep);
                    var item = ConstruirItemActualizacion(datos, mapa, i + 1);
                    if (item != null) filas.Add(item);
                }
                catch (Exception ex)
                {
                    errores++;
                    if (mensajesError.Count < 5)
                        mensajesError.Add($"Fila {i + 1}: {ex.Message}");
                }
            }

            if (errores > 0)
            {
                MostrarResultado(0, 0, errores, mensajesError);
                return;
            }

            ImportarFilasAltaMasiva(filas);
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

                int errores = 0;
                var mensajesError = new List<string>();
                var filas = new List<DatabaseService.ProductoActualizacionMasivaItem>();

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
                        var item = ConstruirItemActualizacion(datos, mapa, r);
                        if (item != null) filas.Add(item);
                    }
                    catch (Exception ex)
                    {
                        errores++;
                        if (mensajesError.Count < 5)
                            mensajesError.Add($"Fila {r}: {ex.Message}");
                    }
                }

                if (errores > 0)
                {
                    MostrarResultado(0, 0, errores, mensajesError);
                    return;
                }

                ImportarFilasAltaMasiva(filas);
            }
        }

        private void ImportarFilasAltaMasiva(List<DatabaseService.ProductoActualizacionMasivaItem> filas)
        {
            if (filas == null || filas.Count == 0)
            {
                MessageBox.Show("El archivo no contiene filas de datos.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var resultado = DatabaseService.ImportarActualizacionMasivaProductos(filas, true);
            MostrarResultadoActualizacion(resultado);
            if (resultado.Exitoso)
                CargarProductos();
        }

        // ── LÓGICA COMÚN ─────────────────────────────────────────────────────

        /// <summary>Devuelve índices de columnas según el encabezado. -1 si no existe.</summary>
        private static int[] MapearColumnas(IList<string> headers)
        {
            var mapaActualizacion = MapearColumnasActualizacion(headers);
            var mapaAlta = new int[mapaActualizacion.Length];
            mapaAlta[0] = -1; // ProductoID no existe en plantilla de alta.
            for (int i = 1; i < mapaAlta.Length; i++)
                mapaAlta[i] = mapaActualizacion[i];
            return mapaAlta;
        }

        private void MostrarResultado(int importados, int actualizados, int errores, List<string> mensajesError)
        {
            var modal = new ResultadoImportacionWindow(importados, actualizados, errores, 0, mensajesError, false);
            modal.Owner = Window.GetWindow(this);
            modal.ShowDialog();
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
