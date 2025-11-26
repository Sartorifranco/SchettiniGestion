using Microsoft.Win32;
using SchettiniGestion;
using System;
using System.Data;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace SchettiniGestion.WPF
{
    public partial class ProductosControl : UserControl
    {
        private int _productoID = 0;
        private string _rutaImagenActual = "";
        private bool _cargandoDatos = false;

        public ProductosControl()
        {
            InitializeComponent();
        }

        private void ProductosControl_Loaded(object sender, RoutedEventArgs e)
        {
            CargarLista();
            Limpiar();
        }

        private void CargarLista()
        {
            try { dgvProductos.ItemsSource = DatabaseService.GetProductos().DefaultView; } catch { }
        }

        private void Limpiar()
        {
            _cargandoDatos = true;

            _productoID = 0;
            _rutaImagenActual = "";
            txtCodigo.Text = "";
            txtCodigoBarra.Text = "";
            txtDescripcion.Text = "";
            cmbCategoria.Text = "";
            cmbTipoIVA.SelectedIndex = 0;
            numCosto.Value = 0;
            numGanancia.Value = 30;
            numImpInterno.Value = 0;
            numPrecioFinal.Value = 0;
            imgProducto.Source = null;

            btnGuardar.Content = "💾 Guardar";
            btnEliminar.IsEnabled = false;

            _cargandoDatos = false;
            txtCodigo.Focus();
        }

        // ===== MÉTODO CORREGIDO: LECTURA ROBUSTA =====
        private void dgvProductos_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dgvProductos.SelectedItem is DataRowView row)
            {
                _cargandoDatos = true;

                try
                {
                    _productoID = Convert.ToInt32(row["ProductoID"]);
                    txtCodigo.Text = row["Codigo"].ToString();

                    // Usamos verificación de columnas para evitar crash si la columna no existe
                    if (row.Row.Table.Columns.Contains("CodigoBarra"))
                        txtCodigoBarra.Text = row["CodigoBarra"].ToString();

                    txtDescripcion.Text = row["Descripcion"].ToString();

                    if (row.Row.Table.Columns.Contains("Categoria"))
                        cmbCategoria.Text = row["Categoria"].ToString();

                    // Precios (Manejo de nulos)
                    decimal costo = 0;
                    if (row.Row.Table.Columns.Contains("PrecioCosto") && row["PrecioCosto"] != DBNull.Value)
                        decimal.TryParse(row["PrecioCosto"].ToString(), out costo);
                    numCosto.Value = costo;

                    decimal ganancia = 30;
                    if (row.Row.Table.Columns.Contains("Ganancia") && row["Ganancia"] != DBNull.Value)
                        decimal.TryParse(row["Ganancia"].ToString(), out ganancia);
                    numGanancia.Value = ganancia;

                    decimal imp = 0;
                    if (row.Row.Table.Columns.Contains("ImpuestoInterno") && row["ImpuestoInterno"] != DBNull.Value)
                        decimal.TryParse(row["ImpuestoInterno"].ToString(), out imp);
                    numImpInterno.Value = imp;

                    decimal venta = 0;
                    if (row.Row.Table.Columns.Contains("PrecioVenta") && row["PrecioVenta"] != DBNull.Value)
                        decimal.TryParse(row["PrecioVenta"].ToString(), out venta);
                    numPrecioFinal.Value = venta;

                    // Imagen
                    imgProducto.Source = null;
                    if (row.Row.Table.Columns.Contains("ImagenPath") && row["ImagenPath"] != DBNull.Value)
                    {
                        string ruta = row["ImagenPath"].ToString();
                        if (!string.IsNullOrEmpty(ruta))
                        {
                            try
                            {
                                imgProducto.Source = new BitmapImage(new Uri(ruta));
                            }
                            catch { } // Si la imagen no existe, no hacemos nada
                        }
                    }

                    btnGuardar.Content = "Modificar";
                    btnEliminar.IsEnabled = true;
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error al leer datos: " + ex.Message);
                }

                _cargandoDatos = false;
            }
        }
        // =============================================

        private void CalcularPrecio_ValueChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (_cargandoDatos) return;
            CalcularPrecioVenta();
        }

        private void CalcularPrecioVenta()
        {
            if (numCosto == null || numGanancia == null || numImpInterno == null || numPrecioFinal == null) return;

            decimal costo = numCosto.Value ?? 0;
            decimal ganancia = numGanancia.Value ?? 0;
            decimal impuestos = numImpInterno.Value ?? 0;

            decimal venta = costo * (1 + (ganancia / 100)) + impuestos;

            numPrecioFinal.Value = Math.Round(venta, 2);
        }

        private void btnImportarExcel_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dlg = new OpenFileDialog();
            dlg.Filter = "Archivos Excel|*.xlsx;*.xls";

            if (dlg.ShowDialog() == true)
            {
                try
                {
                    FileInfo fileInfo = new FileInfo(dlg.FileName);
                    using (OfficeOpenXml.ExcelPackage package = new OfficeOpenXml.ExcelPackage(fileInfo))
                    {
                        OfficeOpenXml.ExcelWorksheet worksheet = package.Workbook.Worksheets[0];
                        int rowCount = worksheet.Dimension.Rows;
                        int contador = 0;

                        for (int row = 2; row <= rowCount; row++)
                        {
                            string codigo = worksheet.Cells[row, 1].Text;
                            if (string.IsNullOrWhiteSpace(codigo)) continue;

                            string barras = worksheet.Cells[row, 2].Text;
                            string descripcion = worksheet.Cells[row, 3].Text;
                            string rubro = worksheet.Cells[row, 4].Text;

                            decimal costo = 0;
                            decimal.TryParse(worksheet.Cells[row, 5].Text, out costo);

                            decimal ganancia = 30;
                            decimal.TryParse(worksheet.Cells[row, 6].Text, out ganancia);

                            int stock = 0;
                            int.TryParse(worksheet.Cells[row, 7].Text, out stock);

                            decimal venta = costo * (1 + (ganancia / 100));

                            DatabaseService.GuardarProducto(0, codigo, barras, descripcion, rubro, "21% (General)", costo, ganancia, 0, venta, stock, "");
                            contador++;
                        }

                        MessageBox.Show($"¡Importación Exitosa!\nSe procesaron {contador} productos.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
                        CargarLista();
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error al importar: {ex.Message}\n\nAsegúrese de que el archivo no esté abierto.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void btnSeleccionarImagen_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dlg = new OpenFileDialog();
            dlg.Filter = "Imágenes|*.jpg;*.png;*.jpeg";
            if (dlg.ShowDialog() == true)
            {
                _rutaImagenActual = dlg.FileName;
                imgProducto.Source = new BitmapImage(new Uri(_rutaImagenActual));
            }
        }

        private void btnNuevo_Click(object sender, RoutedEventArgs e) { Limpiar(); }

        private void btnGuardar_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtCodigo.Text) || string.IsNullOrWhiteSpace(txtDescripcion.Text))
            {
                MessageBox.Show("Código y Descripción son obligatorios.");
                return;
            }

            decimal venta = numPrecioFinal.Value ?? 0;
            if (venta == 0 && (numCosto.Value ?? 0) > 0)
            {
                decimal costo = numCosto.Value ?? 0;
                decimal ganancia = numGanancia.Value ?? 0;
                decimal impuestos = numImpInterno.Value ?? 0;
                venta = costo * (1 + (ganancia / 100)) + impuestos;
            }

            bool ok = DatabaseService.GuardarProducto(
                _productoID,
                txtCodigo.Text,
                txtCodigoBarra.Text,
                txtDescripcion.Text,
                cmbCategoria.Text,
                cmbTipoIVA.Text,
                numCosto.Value ?? 0,
                numGanancia.Value ?? 0,
                numImpInterno.Value ?? 0,
                venta,
                0,
                _rutaImagenActual
            );

            if (ok) { MessageBox.Show("Guardado."); CargarLista(); Limpiar(); }
        }

        private void btnEliminar_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("¿Eliminar?", "Confirma", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                DatabaseService.EliminarProducto(_productoID);
                CargarLista();
                Limpiar();
            }
        }
    }
}