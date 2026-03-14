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
                _dtProductos = DatabaseService.GetProductos("");
                dgvProductos.ItemsSource = _dtProductos.DefaultView;
                AplicarFiltro();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al cargar lista: " + ex.Message);
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
            var sfd = new SaveFileDialog { Filter = "Archivo CSV (Excel)|*.csv", FileName = "Plantilla_Carga_Productos.csv" };
            if (sfd.ShowDialog() == true)
            {
                try
                {
                    string contenido = "CODIGO;CODIGO_BARRAS;DESCRIPCION;CATEGORIA;SUB_RUBRO;MARCA;PROVEEDOR;COSTO;PRECIO_VENTA;STOCK\n" +
                                       "COCA15;779123456;Coca Cola 1.5 Litros;Bebidas;Gaseosas;Coca-Cola;Coca-Cola;1000;1500;50\n" +
                                       "PAN001;;Pan Frances Kg;Almacen;Panaderia;Varios;;800;1200;10";
                    File.WriteAllText(sfd.FileName, contenido, Encoding.UTF8);
                    MessageBox.Show("Plantilla guardada.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex) { MessageBox.Show("Error: " + ex.Message); }
            }
        }

        private void btnImportarExcel_Click(object sender, RoutedEventArgs e)
        {
            var ofd = new OpenFileDialog { Filter = "Archivo CSV (Excel)|*.csv" };
            if (ofd.ShowDialog() == true)
            {
                try { ProcesarImportacionCSV(ofd.FileName); }
                catch (Exception ex) { MessageBox.Show("Error: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error); }
            }
        }

        private void ProcesarImportacionCSV(string ruta)
        {
            var lineas = File.ReadAllLines(ruta, Encoding.UTF8);
            int importados = 0, errores = 0;

            for (int i = 1; i < lineas.Length; i++)
            {
                try
                {
                    string linea = lineas[i];
                    if (string.IsNullOrWhiteSpace(linea)) continue;

                    string[] datos = linea.Split(';');
                    if (datos.Length < 6) { errores++; continue; }

                    string codigo = datos[0].Trim();
                    string codigoBarra = datos.Length > 1 ? datos[1].Trim() : "";
                    string descripcion = datos.Length > 2 ? datos[2].Trim() : "";
                    string categoria = datos.Length > 3 ? datos[3].Trim() : "";
                    string subRubro = datos.Length > 4 ? datos[4].Trim() : "";
                    string marca = datos.Length > 5 ? datos[5].Trim() : "";
                    string proveedor = datos.Length > 6 ? datos[6].Trim() : "";

                    string costoStr = (datos.Length > 7 ? datos[7] : "0").Trim().Replace(",", ".");
                    string ventaStr = (datos.Length > 8 ? datos[8] : "0").Trim().Replace(",", ".");
                    decimal.TryParse(costoStr, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal costo);
                    decimal.TryParse(ventaStr, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal venta);

                    int stock = 0;
                    if (datos.Length > 9) int.TryParse(datos[9].Trim(), out stock);

                    if (string.IsNullOrEmpty(codigoBarra)) codigoBarra = codigo;

                    decimal ganancia = costo > 0 ? ((venta - costo) / costo) * 100 : 0;

                    int idProd = 0;
                    var existente = DatabaseService.BuscarProducto(codigo);
                    if (existente != null) idProd = Convert.ToInt32(existente["ProductoID"]);

                    DatabaseService.GuardarProducto(idProd, codigo, codigoBarra, descripcion, categoria, subRubro, marca, proveedor, "21.0", costo, ganancia, 0, venta, stock, null);
                    importados++;
                }
                catch { errores++; }
            }

            MessageBox.Show($"Procesados: {importados}\nErrores: {errores}", "Importación", MessageBoxButton.OK, MessageBoxImage.Information);
            CargarProductos();
        }
    }
}
