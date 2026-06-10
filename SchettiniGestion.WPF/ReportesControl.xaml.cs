using Microsoft.Win32; // Necesario para el guardado de archivos
using System;
using System.Data;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using SchettiniGestion;

namespace SchettiniGestion.WPF
{
    public partial class ReportesControl : UserControl
    {
        public ReportesControl()
        {
            InitializeComponent();
        }

        private void ReportesControl_Loaded(object sender, RoutedEventArgs e)
        {
            // Por defecto mostramos el mes actual
            EstablecerFechas("Mes");
        }

        // --- FILTROS RÁPIDOS (TOUCH) ---
        private void btnFiltroRapido_Click(object sender, RoutedEventArgs e)
        {
            string opcion = (sender as Button).Tag.ToString();
            EstablecerFechas(opcion);
        }

        private void EstablecerFechas(string opcion)
        {
            DateTime hoy = DateTime.Today;

            switch (opcion)
            {
                case "Hoy":
                    dtpDesde.SelectedDate = hoy;
                    dtpHasta.SelectedDate = hoy;
                    break;
                case "Ayer":
                    dtpDesde.SelectedDate = hoy.AddDays(-1);
                    dtpHasta.SelectedDate = hoy.AddDays(-1);
                    break;
                case "Semana":
                    dtpDesde.SelectedDate = hoy.AddDays(-7);
                    dtpHasta.SelectedDate = hoy;
                    break;
                case "Mes":
                    dtpDesde.SelectedDate = new DateTime(hoy.Year, hoy.Month, 1);
                    dtpHasta.SelectedDate = dtpDesde.SelectedDate.Value.AddMonths(1).AddDays(-1);
                    break;
                case "Anio":
                    dtpDesde.SelectedDate = new DateTime(hoy.Year, 1, 1);
                    dtpHasta.SelectedDate = new DateTime(hoy.Year, 12, 31);
                    break;
            }
            CargarReporte();
        }

        // --- GENERAR REPORTE ---
        private void btnBuscar_Click(object sender, RoutedEventArgs e)
        {
            CargarReporte();
        }

        private void CargarReporte()
        {
            if (dtpDesde.SelectedDate == null || dtpHasta.SelectedDate == null) return;

            try
            {
                DateTime desde = dtpDesde.SelectedDate.Value;
                // Ajustamos la hora final para incluir todo el día hasta las 23:59:59
                DateTime hasta = dtpHasta.SelectedDate.Value.Date.AddDays(1).AddSeconds(-1);

                DataTable dt = DatabaseService.GetRankingVentas(desde, hasta);
                dgvReporte.ItemsSource = dt.DefaultView;

                // Calcular Total General del reporte
                decimal total = 0;
                foreach (DataRow row in dt.Rows)
                {
                    total += Convert.ToDecimal(row["TotalVendido"]);
                }
                lblTotalGeneral.Text = total.ToString("C2");
            }
            catch (Exception ex)
            {
                ModernMessageBox.Show("Error al generar reporte: " + ex.Message);
            }
        }

        // --- EXPORTAR A EXCEL (LÓGICA NUEVA) ---
        private void btnExportarExcel_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 1. Verificar si hay datos
                if (dgvReporte.ItemsSource == null)
                {
                    ModernMessageBox.Show("No hay datos para exportar. Genere el reporte primero.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                DataView view = (DataView)dgvReporte.ItemsSource;
                DataTable dt = view.Table;

                if (dt.Rows.Count == 0)
                {
                    ModernMessageBox.Show("La lista está vacía.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // 2. Configurar el diálogo de guardado
                SaveFileDialog sfd = new SaveFileDialog();
                sfd.Filter = "Archivo CSV (Excel)|*.csv";
                sfd.FileName = $"Reporte_Ventas_{DateTime.Now:yyyyMMdd_HHmm}.csv";

                if (sfd.ShowDialog() == true)
                {
                    // 3. Generar el contenido del archivo (Formato CSV compatible con Excel en Español)
                    StringBuilder sb = new StringBuilder();

                    // Encabezados
                    string[] columnNames = { "CÓDIGO", "PRODUCTO", "RUBRO", "UNIDADES", "TOTAL VENDIDO" };
                    sb.AppendLine(string.Join(";", columnNames));

                    // Filas
                    foreach (DataRow row in dt.Rows)
                    {
                        string[] fields = {
                            row["Codigo"].ToString(),
                            "\"" + row["Descripcion"].ToString() + "\"", // Comillas para evitar problemas si el nombre tiene ;
                            row["Rubro"].ToString(),
                            row["UnidadesVendidas"].ToString(),
                            Convert.ToDecimal(row["TotalVendido"]).ToString("F2") // Formato número
                        };
                        sb.AppendLine(string.Join(";", fields));
                    }

                    // 4. Guardar archivo
                    File.WriteAllText(sfd.FileName, sb.ToString(), Encoding.UTF8);

                    // 5. Confirmar y abrir
                    if (ModernMessageBox.Show("¡Reporte exportado correctamente!\n\n¿Desea abrirlo ahora?", "Éxito", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = sfd.FileName,
                            UseShellExecute = true
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                ModernMessageBox.Show("Error al exportar: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}