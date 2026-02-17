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
        private TextBox _ultimoTextBoxFoco; // Variable para recordar dónde escribir

        public PreciosControl()
        {
            InitializeComponent();
            _ultimoTextBoxFoco = txtBuscar; // Por defecto al buscador
        }

        private void PreciosControl_Loaded(object sender, RoutedEventArgs e)
        {
            CargarProductos();
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

        // --- SELECCIÓN EN TABLA ---
        private void gridProductos_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (gridProductos.SelectedItem is DataRowView row)
            {
                txtDescripcion.Text = row["Descripcion"].ToString();

                // CORRECCIÓN AQUÍ: Usamos el nombre real de la columna en BD (PrecioCosto)
                if (row.Row.Table.Columns.Contains("PrecioCosto"))
                    txtCosto.Text = row["PrecioCosto"].ToString();
                else
                    txtCosto.Text = "0";

                txtPrecioVenta.Text = row["PrecioVenta"].ToString();

                // Al seleccionar, ponemos foco en Venta para editar rápido con teclado
                txtPrecioVenta.Focus();
            }
        }

        // --- GUARDAR CAMBIOS ---
        private void btnGuardar_Click(object sender, RoutedEventArgs e)
        {
            if (gridProductos.SelectedItem is DataRowView row)
            {
                try
                {
                    int id = Convert.ToInt32(row["ProductoID"]);
                    decimal costo = 0;
                    decimal venta = 0;

                    decimal.TryParse(txtCosto.Text, out costo);
                    decimal.TryParse(txtPrecioVenta.Text, out venta);

                    if (DatabaseService.ActualizarPrecioProducto(id, costo, venta))
                    {
                        // Mensaje sutil o recarga
                        CargarProductos(txtBuscar.Text); // Recargar manteniendo filtro
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

        // --- BUSCADOR ---
        private void txtBuscar_KeyUp(object sender, KeyEventArgs e)
        {
            CargarProductos(txtBuscar.Text);
        }

        // --- MAGIA DEL TECLADO ---

        // 1. Detectar quién tiene el foco
        private void TextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            _ultimoTextBoxFoco = sender as TextBox;
        }

        // 2. Escribir número
        private void BtnNum_Click(object sender, RoutedEventArgs e)
        {
            if (_ultimoTextBoxFoco == null) return;

            Button btn = sender as Button;
            string numero = btn.Content.ToString();

            // Insertar en la posición del cursor
            int index = _ultimoTextBoxFoco.CaretIndex;
            _ultimoTextBoxFoco.Text = _ultimoTextBoxFoco.Text.Insert(index, numero);
            _ultimoTextBoxFoco.CaretIndex = index + 1; // Mover cursor adelante

            // NO HACEMOS FOCUS() AQUÍ para que el teclado visual no robe el foco lógico real
            // Gracias a Focusable=False en XAML, el foco sigue en el TextBox
        }

        // 3. Borrar (Backspace)
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

        // 4. Limpiar (C)
        private void BtnClear_Click(object sender, RoutedEventArgs e)
        {
            if (_ultimoTextBoxFoco != null)
            {
                _ultimoTextBoxFoco.Text = "";
                _ultimoTextBoxFoco.Focus();
            }
        }

        // --- VALIDACIONES ---
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