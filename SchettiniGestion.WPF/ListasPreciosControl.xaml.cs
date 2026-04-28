using SchettiniGestion;
using System;
using System.Data;
using System.Windows;
using System.Windows.Controls;

namespace SchettiniGestion.WPF
{
    public partial class ListasPreciosControl : UserControl
    {
        private int _listaID = 0;

        public ListasPreciosControl()
        {
            InitializeComponent();
        }

        public ListasPreciosControl(object param) : this() { }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            CargarListas();
        }

        private void CargarListas()
        {
            try { dgvListas.ItemsSource = DatabaseService.GetListasPrecios().DefaultView; } catch { }
        }

        private void Limpiar()
        {
            _listaID = 0;
            txtNombreLista.Text = "";
            numPorcentaje.Value = 0;
            btnGuardar.Content = "Guardar";
        }

        private void btnNuevo_Click(object sender, RoutedEventArgs e) { Limpiar(); }

        private void btnGuardar_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtNombreLista.Text)) { CustomMessageBox.Show("Ingrese un nombre."); return; }

            if (DatabaseService.GuardarListaPrecio(_listaID, txtNombreLista.Text, numPorcentaje.Value ?? 0))
            {
                CustomMessageBox.Show("Guardado.");
                CargarListas();
                Limpiar();
            }
        }

        private void dgvListas_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dgvListas.SelectedItem is DataRowView row)
            {
                _listaID = Convert.ToInt32(row["ListaID"]);
                txtNombreLista.Text = row["Nombre"].ToString();
                numPorcentaje.Value = Convert.ToDecimal(row["Porcentaje"]);
                btnGuardar.Content = "Modificar";
            }
        }

        private void btnEliminar_Click(object sender, RoutedEventArgs e)
        {
            if (dgvListas.SelectedItem is DataRowView row)
            {
                int id = Convert.ToInt32(row["ListaID"]);
                if (CustomMessageBox.Show("¿Eliminar esta lista?", "Confirmar", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    if (DatabaseService.EliminarListaPrecio(id))
                    {
                        CargarListas();
                        Limpiar();
                    }
                }
            }
        }
    }
}