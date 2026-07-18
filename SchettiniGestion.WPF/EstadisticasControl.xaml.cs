using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using LiveCharts;
using LiveCharts.Wpf;
using SchettiniGestion;

namespace SchettiniGestion.WPF
{
    public partial class EstadisticasControl : UserControl
    {
        private static readonly CultureInfo CulturaAr = CultureInfo.GetCultureInfo("es-AR");

        public EstadisticasControl()
        {
            InitializeComponent();
            ConfigurarEjesVacios();
        }

        private void ConfigurarEjesVacios()
        {
            Func<double, string> formatoMoneda = v => v.ToString("C0", CulturaAr);

            chartVentasDia.AxisX.Clear();
            chartVentasDia.AxisY.Clear();
            chartVentasDia.AxisX.Add(new Axis
            {
                Foreground = BrushTema("TextSecondary", Brushes.Gray),
                FontSize = 11,
                Labels = new List<string>()
            });
            chartVentasDia.AxisY.Add(new Axis
            {
                Foreground = BrushTema("TextSecondary", Brushes.Gray),
                FontSize = 11,
                LabelFormatter = formatoMoneda
            });

            chartTopProductos.AxisX.Clear();
            chartTopProductos.AxisY.Clear();
            chartTopProductos.AxisX.Add(new Axis
            {
                Foreground = BrushTema("TextSecondary", Brushes.Gray),
                FontSize = 11,
                LabelFormatter = formatoMoneda
            });
            chartTopProductos.AxisY.Add(new Axis
            {
                Foreground = BrushTema("TextSecondary", Brushes.Gray),
                FontSize = 11,
                Labels = new List<string>()
            });
        }

        private void EstadisticasControl_Loaded(object sender, RoutedEventArgs e)
        {
            EstablecerFechas("Mes");
        }

        private void btnFiltroRapido_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string tag)
                EstablecerFechas(tag);
        }

        private void btnGenerar_Click(object sender, RoutedEventArgs e) => CargarEstadisticas();

        private void EstablecerFechas(string opcion)
        {
            DateTime hoy = DateTime.Today;
            switch (opcion)
            {
                case "Hoy":
                    dtpDesde.SelectedDate = hoy;
                    dtpHasta.SelectedDate = hoy;
                    break;
                case "Semana":
                    dtpDesde.SelectedDate = hoy.AddDays(-6);
                    dtpHasta.SelectedDate = hoy;
                    break;
                case "Mes":
                    dtpDesde.SelectedDate = new DateTime(hoy.Year, hoy.Month, 1);
                    dtpHasta.SelectedDate = hoy;
                    break;
                case "Anio":
                    dtpDesde.SelectedDate = new DateTime(hoy.Year, 1, 1);
                    dtpHasta.SelectedDate = hoy;
                    break;
            }
            CargarEstadisticas();
        }

        private void CargarEstadisticas()
        {
            if (dtpDesde.SelectedDate == null || dtpHasta.SelectedDate == null) return;

            DateTime desde = dtpDesde.SelectedDate.Value.Date;
            DateTime hasta = dtpHasta.SelectedDate.Value.Date.AddDays(1).AddSeconds(-1);

            try
            {
                var resumen = DatabaseService.GetResumenVentasPeriodo(desde, hasta);
                lblTotalVendido.Text = resumen.TotalVendido.ToString("C2", CulturaAr);
                lblCantidad.Text = resumen.CantidadComprobantes.ToString("N0", CulturaAr);
                lblTicketPromedio.Text = resumen.TicketPromedio.ToString("C2", CulturaAr);

                CargarVentasPorDia(desde, hasta);
                CargarTopProductos(desde, hasta);
                CargarMediosPago(desde, hasta);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al cargar estadísticas: " + ex.Message, "Estadísticas",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void CargarVentasPorDia(DateTime desde, DateTime hasta)
        {
            DataTable dt = DatabaseService.GetVentasPorDia(desde, hasta);
            var labels = new List<string>();
            var valores = new ChartValues<double>();

            foreach (DataRow row in dt.Rows)
            {
                DateTime dia = Convert.ToDateTime(row["Dia"]);
                labels.Add(dia.ToString("dd/MM", CulturaAr));
                valores.Add(Convert.ToDouble(row["Total"]));
            }

            bool vacio = valores.Count == 0;
            lblSinVentasDia.Visibility = vacio ? Visibility.Visible : Visibility.Collapsed;
            chartVentasDia.Visibility = vacio ? Visibility.Collapsed : Visibility.Visible;

            if (chartVentasDia.AxisX.Count > 0)
                chartVentasDia.AxisX[0].Labels = labels;

            chartVentasDia.Series = new SeriesCollection
            {
                new LineSeries
                {
                    Title = "Ventas",
                    Values = valores,
                    PointGeometry = DefaultGeometries.Circle,
                    PointGeometrySize = 8,
                    StrokeThickness = 2.5,
                    Fill = Brushes.Transparent,
                    Stroke = BrushTema("SuccessColor", Brushes.LimeGreen),
                    LineSmoothness = 0.3
                }
            };
        }

        private void CargarTopProductos(DateTime desde, DateTime hasta)
        {
            DataTable dt = DatabaseService.GetTopProductosVentas(desde, hasta, 10);
            var filas = dt.AsEnumerable().Reverse().ToList();
            var labels = new List<string>();
            var valores = new ChartValues<double>();

            foreach (DataRow row in filas)
            {
                string desc = row["Descripcion"]?.ToString() ?? "";
                if (desc.Length > 28) desc = desc.Substring(0, 26) + "…";
                labels.Add(desc);
                valores.Add(Convert.ToDouble(row["TotalVendido"]));
            }

            bool vacio = valores.Count == 0;
            lblSinTop.Visibility = vacio ? Visibility.Visible : Visibility.Collapsed;
            chartTopProductos.Visibility = vacio ? Visibility.Collapsed : Visibility.Visible;

            if (chartTopProductos.AxisY.Count > 0)
                chartTopProductos.AxisY[0].Labels = labels;

            chartTopProductos.Series = new SeriesCollection
            {
                new RowSeries
                {
                    Title = "Total",
                    Values = valores,
                    DataLabels = true,
                    LabelPoint = p => p.X.ToString("C0", CulturaAr),
                    Fill = BrushTema("SuccessColor", new SolidColorBrush(Color.FromRgb(0, 158, 227)))
                }
            };
        }

        private void CargarMediosPago(DateTime desde, DateTime hasta)
        {
            DataTable dt = DatabaseService.GetVentasPorMedioPago(desde, hasta);
            var series = new SeriesCollection();
            var colores = new[]
            {
                Color.FromRgb(46, 204, 113),
                Color.FromRgb(0, 158, 227),
                Color.FromRgb(241, 196, 15),
                Color.FromRgb(155, 89, 182),
                Color.FromRgb(230, 126, 34),
                Color.FromRgb(52, 152, 219),
                Color.FromRgb(231, 76, 60)
            };

            int i = 0;
            foreach (DataRow row in dt.Rows)
            {
                decimal total = Convert.ToDecimal(row["Total"]);
                if (total <= 0) continue;
                string medio = row["Medio"]?.ToString() ?? "Otro";
                series.Add(new PieSeries
                {
                    Title = medio,
                    Values = new ChartValues<double> { Convert.ToDouble(total) },
                    DataLabels = true,
                    LabelPoint = p => p.Y.ToString("C0", CulturaAr),
                    Fill = new SolidColorBrush(colores[i % colores.Length])
                });
                i++;
            }

            bool vacio = series.Count == 0;
            lblSinMedios.Visibility = vacio ? Visibility.Visible : Visibility.Collapsed;
            chartMediosPago.Visibility = vacio ? Visibility.Collapsed : Visibility.Visible;
            chartMediosPago.Series = series;
        }

        private static Brush BrushTema(string key, Brush fallback)
        {
            return Application.Current?.TryFindResource(key) as Brush ?? fallback;
        }
    }
}
