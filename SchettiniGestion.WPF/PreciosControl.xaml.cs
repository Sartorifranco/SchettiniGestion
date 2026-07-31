using System;
using System.Data;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Text.RegularExpressions;
using SchettiniGestion;

namespace SchettiniGestion.WPF
{
    public partial class PreciosControl : UserControl
    {
        private TextBox _ultimoTextBoxFoco;
        private bool _inicializado;

        public PreciosControl()
        {
            InitializeComponent();
            _ultimoTextBoxFoco = txtBuscar;
        }

        private void PreciosControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (!_inicializado)
            {
                _inicializado = true;
                CargarProductos();
            }
            txtBuscar.Focus();
        }

        private void CargarProductos(string filtro = "")
        {
            try
            {
                DataTable dt = DatabaseService.GetProductos(filtro);
                gridProductos.ItemsSource = dt.DefaultView;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al cargar productos: " + ex.Message);
            }
        }

        private void gridProductos_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (gridProductos.SelectedItem is DataRowView row)
            {
                txtDescripcion.Text = row["Descripcion"].ToString();

                if (row.Row.Table.Columns.Contains("PrecioCosto"))
                    txtCosto.Text = row["PrecioCosto"].ToString();
                else
                    txtCosto.Text = "0";

                txtPrecioVenta.Text = row["PrecioVenta"].ToString();
                txtPrecioVenta.Focus();
            }
        }

        private void btnGuardar_Click(object sender, RoutedEventArgs e)
        {
            if (gridProductos.SelectedItem is DataRowView row)
            {
                try
                {
                    int id = Convert.ToInt32(row["ProductoID"]);
                    decimal.TryParse(txtCosto.Text, out decimal costo);
                    decimal.TryParse(txtPrecioVenta.Text, out decimal venta);

                    decimal costoAnterior = row.Row.Table.Columns.Contains("PrecioCosto") && row["PrecioCosto"] != DBNull.Value
                        ? Convert.ToDecimal(row["PrecioCosto"]) : 0m;
                    bool costoModificado = Math.Abs(costoAnterior - costo) >= 0.005m;

                    if (DatabaseService.ActualizarPreciosProducto(id, costo, venta, out decimal precioVentaActualizado))
                    {
                        if (costoModificado)
                            txtPrecioVenta.Text = precioVentaActualizado.ToString("F2");

                        CargarProductos(txtBuscar.Text);
                        LimpiarInputsMenosBuscador();
                        MessageBox.Show("Precio actualizado correctamente.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        MessageBox.Show("No se pudo actualizar el precio.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error: " + ex.Message);
                }
            }
            else
            {
                MessageBox.Show("Seleccione un producto de la lista primero.");
            }
        }

        private void txtBuscar_KeyUp(object sender, KeyEventArgs e)
        {
            CargarProductos(txtBuscar.Text);
        }

        private void TextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            _ultimoTextBoxFoco = sender as TextBox;
        }

        private void BtnNum_Click(object sender, RoutedEventArgs e)
        {
            if (_ultimoTextBoxFoco == null) return;
            Button btn = sender as Button;
            string numero = btn.Content.ToString();
            int index = _ultimoTextBoxFoco.CaretIndex;
            _ultimoTextBoxFoco.Text = _ultimoTextBoxFoco.Text.Insert(index, numero);
            _ultimoTextBoxFoco.CaretIndex = index + 1;
        }

        private void BtnBorrar_Click(object sender, RoutedEventArgs e)
        {
            if (_ultimoTextBoxFoco == null || _ultimoTextBoxFoco.Text.Length == 0) return;
            int index = _ultimoTextBoxFoco.CaretIndex;
            if (index > 0)
            {
                _ultimoTextBoxFoco.Text = _ultimoTextBoxFoco.Text.Remove(index - 1, 1);
                _ultimoTextBoxFoco.CaretIndex = index - 1;
            }
        }

        private void BtnClear_Click(object sender, RoutedEventArgs e)
        {
            if (_ultimoTextBoxFoco != null)
            {
                _ultimoTextBoxFoco.Text = "";
                _ultimoTextBoxFoco.Focus();
            }
        }

        private void SoloNumeros(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !Regex.IsMatch(e.Text, @"^[0-9,\.]+$");
        }

        private void LimpiarInputsMenosBuscador()
        {
            txtDescripcion.Text = "";
            txtCosto.Text = "";
            txtPrecioVenta.Text = "";
        }
    }
}
