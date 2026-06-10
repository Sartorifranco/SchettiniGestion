using Microsoft.Win32;
using System;
using System.Data;
using System.IO;
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
                _dtProductos = DatabaseService.GetProductosListado("");
                dgvProductos.ItemsSource = _dtProductos.DefaultView;
                AplicarFiltro();
            }
            catch (Exception ex)
            {
                ModernMessageBox.Show("Error al cargar lista: " + ex.Message);
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
            var sb = new System.Text.StringBuilder();
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
            AplicarFiltro();
        }

        private void btnNuevo_Click(object sender, RoutedEventArgs e)
        {
            var modal = new ProductoModalWindow(0, false, CargarProductos);
            modal.Owner = Window.GetWindow(this);
            modal.ShowDialog();
        }

        private void dgvProductos_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // La edición se abre con doble clic
        }

        private void dgvProductos_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            AbrirEditar();
        }

        private void MenuItemEditar_Click(object sender, RoutedEventArgs e)
        {
            AbrirEditar();
        }

        private void MenuItemDuplicar_Click(object sender, RoutedEventArgs e)
        {
            AbrirDuplicar();
        }

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

        private void btnDescargarPlantilla_Click(object sender, RoutedEventArgs e)
        {
            var sfd = new SaveFileDialog { Filter = "Archivo CSV|*.csv", FileName = "Plantilla_Productos.csv" };
            if (sfd.ShowDialog() != true) return;
            try
            {
                // Las columnas deben coincidir exactamente con lo que lee ProcesarImportacionCSV (header-driven).
                string cabeceras = "CodigoBarra;CodigoExterno;Descripcion;Categoria;SubRubro;Proveedor;Costo;GananciaPorcentaje;PrecioVenta;StockActual;StockMinimo;AceptaStockNegativo";
                string ejemploA  = "7791234567890;COCA15;Coca Cola 1.5 Litros;Bebidas;Gaseosas;Coca-Cola;1000;50;1500;50;5;No";
                string ejemploB  = ";PAN001;Pan Frances Kg;Almacen;Panaderia;Varios;800;50;1200;10;;No";
                File.WriteAllText(sfd.FileName, cabeceras + "\n" + ejemploA + "\n" + ejemploB, Encoding.UTF8);
                ModernMessageBox.Show(
                    "Plantilla descargada con éxito.\n\nLlénela respetando los nombres de las columnas y luego use '📗 Importar Masivo'.\n\n" +
                    "• CodigoBarra: código de barras EAN/UPC (puede dejarse vacío).\n" +
                    "• CodigoExterno: código interno del sistema (obligatorio o se usa CodigoBarra).\n" +
                    "• GananciaPorcentaje: ej. 50 significa 50% de ganancia sobre el costo.\n" +
                    "• AceptaStockNegativo: escribir Sí o No.",
                    "Plantilla Generada", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex) { ModernMessageBox.Show("Error al guardar la plantilla: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        private void btnImportarExcel_Click(object sender, RoutedEventArgs e)
        {
            var ofd = new OpenFileDialog { Filter = "Archivo CSV|*.csv", Title = "Seleccionar archivo de importación" };
            if (ofd.ShowDialog() == true)
            {
                try { ProcesarImportacionCSV(ofd.FileName); }
                catch (Exception ex) { ModernMessageBox.Show("Error inesperado: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error); }
            }
        }

        private void ProcesarImportacionCSV(string ruta)
        {
            var lineas = File.ReadAllLines(ruta, Encoding.UTF8);
            if (lineas.Length < 2)
            {
                ModernMessageBox.Show("El archivo no contiene filas de datos.", "Importación", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Mapa columna→índice (insensible a mayúsculas/espacios)
            var headers = lineas[0].Split(';');
            var idx = new System.Collections.Generic.Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int h = 0; h < headers.Length; h++)
                idx[headers[h].Trim()] = h;

            string Col(string[] d, string name)
                => idx.TryGetValue(name, out int i) && i < d.Length ? d[i].Trim() : "";

            decimal ParseDec(string s) { decimal.TryParse(s.Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out decimal v); return v; }

            int importados = 0, errores = 0;

            for (int i = 1; i < lineas.Length; i++)
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(lineas[i])) continue;
                    string[] d = lineas[i].Split(';');

                    string codigoBarra   = Col(d, "CodigoBarra");
                    string codigoExterno = Col(d, "CodigoExterno");
                    string descripcion   = Col(d, "Descripcion");
                    string categoria     = Col(d, "Categoria");
                    string subRubro      = Col(d, "SubRubro");
                    string proveedor     = Col(d, "Proveedor");

                    decimal costo        = ParseDec(Col(d, "Costo"));
                    decimal ganancia     = ParseDec(Col(d, "GananciaPorcentaje"));
                    decimal precioVenta  = ParseDec(Col(d, "PrecioVenta"));
                    int.TryParse(Col(d, "StockActual"), out int stockActual);

                    string stockMinStr   = Col(d, "StockMinimo");
                    decimal? stockMinimo = !string.IsNullOrEmpty(stockMinStr) ? (decimal?)ParseDec(stockMinStr) : null;

                    string aceptaRaw     = Col(d, "AceptaStockNegativo").ToLowerInvariant();
                    bool aceptaStockNeg  = aceptaRaw == "si" || aceptaRaw == "sí" || aceptaRaw == "yes" || aceptaRaw == "true" || aceptaRaw == "1";

                    // Código principal: preferir CodigoExterno; si no, usar CodigoBarra
                    string codigo = !string.IsNullOrEmpty(codigoExterno) ? codigoExterno : codigoBarra;
                    if (string.IsNullOrEmpty(codigoBarra)) codigoBarra = codigo;

                    if (string.IsNullOrEmpty(descripcion)) { errores++; continue; }

                    // Si no viene PrecioVenta pero sí Costo y Ganancia, calcularlo
                    if (precioVenta == 0 && costo > 0 && ganancia > 0)
                        precioVenta = costo * (1 + ganancia / 100m);

                    int idProd = 0;
                    var existente = DatabaseService.BuscarProducto(codigo);
                    if (existente != null) idProd = Convert.ToInt32(existente["ProductoID"]);

                    DatabaseService.GuardarProducto(
                        idProd, codigo, codigoBarra, descripcion, categoria, subRubro,
                        /*marca*/ "", proveedor, /*iva*/ "21.0",
                        costo, ganancia, /*imp*/ 0, precioVenta, stockActual, /*img*/ null,
                        /*moneda*/ "ARS", /*permiteModPrecio*/ true, /*esStockeable*/ true,
                        aceptaStockNeg, /*usaVariantes*/ false, /*esCombo*/ false,
                        stockMinimo, /*stockIdeal*/ null,
                        codigoExterno, /*color*/ "", /*talle*/ "", /*udm*/ "");

                    importados++;
                }
                catch { errores++; }
            }

            ModernMessageBox.Show(
                $"Importación finalizada.\n\n✔ Importados: {importados}\n✖ Errores: {errores}",
                "Importación Masiva", MessageBoxButton.OK, MessageBoxImage.Information);
            CargarProductos();
        }
    }
}
