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
        private int _diasMuertos = 60;

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

            chartTopMargen.AxisX.Clear();
            chartTopMargen.AxisY.Clear();
            chartTopMargen.AxisX.Add(new Axis
            {
                Foreground = BrushTema("TextSecondary", Brushes.Gray),
                FontSize = 11,
                LabelFormatter = formatoMoneda
            });
            chartTopMargen.AxisY.Add(new Axis
            {
                Foreground = BrushTema("TextSecondary", Brushes.Gray),
                FontSize = 11,
                Labels = new List<string>()
            });

            chartVentasHora.AxisX.Clear();
            chartVentasHora.AxisY.Clear();
            chartVentasHora.AxisX.Add(new Axis
            {
                Foreground = BrushTema("TextSecondary", Brushes.Gray),
                FontSize = 10,
                Labels = new List<string>()
            });
            chartVentasHora.AxisY.Add(new Axis
            {
                Foreground = BrushTema("TextSecondary", Brushes.Gray),
                FontSize = 11,
                LabelFormatter = formatoMoneda
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
                lblMargen.Text = resumen.MargenEstimado.ToString("C2", CulturaAr);
                lblMargenPct.Text = $"Margen {resumen.MargenPct:N1}%";
                lblPeriodoAnterior.Text = resumen.TotalPeriodoAnterior.ToString("C2", CulturaAr);
                lblStockBajo.Text = DatabaseService.GetCantidadProductosStockBajo().ToString("N0", CulturaAr);

                string signo = resumen.VariacionPct >= 0 ? "+" : "";
                lblVsPeriodo.Text = $"vs período anterior: {signo}{resumen.VariacionPct:N1}%";
                lblVsPeriodo.Foreground = resumen.VariacionPct >= 0
                    ? BrushTema("SuccessColor", Brushes.LimeGreen)
                    : BrushTema("DangerColor", Brushes.OrangeRed);

                CargarVentasPorDia(desde, hasta);
                CargarVentasPorHora(desde, hasta);
                CargarTopProductos(desde, hasta);
                CargarTopMargen(desde, hasta);
                CargarMediosPago(desde, hasta);
                CargarAbc(desde, hasta);
                CargarMuertos();
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

        private void CargarVentasPorHora(DateTime desde, DateTime hasta)
        {
            DataTable dt = DatabaseService.GetVentasPorHora(desde, hasta);
            var porHora = new double[24];
            foreach (DataRow row in dt.Rows)
            {
                int h = Convert.ToInt32(row["Hora"]);
                if (h >= 0 && h < 24)
                    porHora[h] = Convert.ToDouble(row["Total"]);
            }

            var labels = new List<string>();
            var valores = new ChartValues<double>();
            // Mostrar franja comercial típica 8–22 si hay datos; si no, todas
            bool hayDatos = porHora.Any(v => v > 0);
            int desdeH = 8, hastaH = 22;
            if (hayDatos)
            {
                desdeH = Enumerable.Range(0, 24).FirstOrDefault(i => porHora[i] > 0);
                hastaH = Enumerable.Range(0, 24).LastOrDefault(i => porHora[i] > 0);
                if (hastaH < desdeH) { desdeH = 8; hastaH = 22; }
            }

            for (int h = desdeH; h <= hastaH; h++)
            {
                labels.Add($"{h:00}h");
                valores.Add(porHora[h]);
            }

            bool vacio = !hayDatos;
            lblSinHoras.Visibility = vacio ? Visibility.Visible : Visibility.Collapsed;
            chartVentasHora.Visibility = vacio ? Visibility.Collapsed : Visibility.Visible;

            if (chartVentasHora.AxisX.Count > 0)
                chartVentasHora.AxisX[0].Labels = labels;

            chartVentasHora.Series = new SeriesCollection
            {
                new ColumnSeries
                {
                    Title = "Ventas",
                    Values = valores,
                    Fill = BrushTema("SuccessColor", new SolidColorBrush(Color.FromRgb(0, 158, 227))),
                    DataLabels = false
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

        private void CargarTopMargen(DateTime desde, DateTime hasta)
        {
            DataTable dt = DatabaseService.GetTopProductosPorMargen(desde, hasta, 10);
            var filas = dt.AsEnumerable().Reverse().ToList();
            var labels = new List<string>();
            var valores = new ChartValues<double>();

            foreach (DataRow row in filas)
            {
                string desc = row["Descripcion"]?.ToString() ?? "";
                if (desc.Length > 28) desc = desc.Substring(0, 26) + "…";
                labels.Add(desc);
                valores.Add(Convert.ToDouble(row["Margen"]));
            }

            bool vacio = valores.Count == 0;
            lblSinMargen.Visibility = vacio ? Visibility.Visible : Visibility.Collapsed;
            chartTopMargen.Visibility = vacio ? Visibility.Collapsed : Visibility.Visible;

            if (chartTopMargen.AxisY.Count > 0)
                chartTopMargen.AxisY[0].Labels = labels;

            chartTopMargen.Series = new SeriesCollection
            {
                new RowSeries
                {
                    Title = "Margen",
                    Values = valores,
                    DataLabels = true,
                    LabelPoint = p => p.X.ToString("C0", CulturaAr),
                    Fill = new SolidColorBrush(Color.FromRgb(59, 130, 246))
                }
            };
        }

        private void CargarAbc(DateTime desde, DateTime hasta)
        {
            DataTable abc = DatabaseService.GetAnalisisAbcProductos(desde, hasta);
            dgvAbc.ItemsSource = abc.DefaultView;

            int a = 0, b = 0, c = 0;
            decimal ventaA = 0m, total = 0m;
            foreach (DataRow row in abc.Rows)
            {
                decimal v = Convert.ToDecimal(row["TotalVendido"]);
                total += v;
                string clase = row["ClaseAbc"]?.ToString() ?? "C";
                if (clase == "A") { a++; ventaA += v; }
                else if (clase == "B") b++;
                else c++;
            }
            decimal pctA = total > 0 ? (ventaA / total) * 100m : 0m;
            lblAbcResumen.Text = abc.Rows.Count == 0
                ? "Todavía no hay ventas en este período para armar el ranking."
                : $"Estrellas: {a} productos (se llevan el {pctA:N0}% de lo vendido). Importantes: {b}. Resto: {c}. " +
                  "Idea: cuidá y empujá las estrellas; revisá el resto (¿liquidar, no reponer o bajar precio?).";
        }

        private void btnMuertos_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag != null && int.TryParse(btn.Tag.ToString(), out int dias))
                _diasMuertos = dias;
            CargarMuertos();
        }

        private void CargarMuertos()
        {
            DataTable dt = DatabaseService.GetProductosSinMovimiento(_diasMuertos, 40);
            dgvMuertos.ItemsSource = dt.DefaultView;

            decimal capital = 0m;
            foreach (DataRow row in dt.Rows)
                capital += row["CapitalInmovilizado"] != DBNull.Value ? Convert.ToDecimal(row["CapitalInmovilizado"]) : 0m;

            bool vacio = dt.Rows.Count == 0;
            lblSinMuertos.Visibility = vacio ? Visibility.Visible : Visibility.Collapsed;
            dgvMuertos.Visibility = vacio ? Visibility.Collapsed : Visibility.Visible;
            lblMuertosResumen.Text = vacio
                ? $"Ningún producto con stock sin ventas en {_diasMuertos} días."
                : $"{dt.Rows.Count} productos sin venta en {_diasMuertos} días · capital inmovilizado ≈ {capital.ToString("C0", CulturaAr)}. Candidatos a liquidar o no reponer.";
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
