using System;
using System.Collections.Generic;
using System.Data;
using System.Windows;
using SchettiniGestion;

namespace SchettiniGestion.WPF
{
    public partial class DetalleVentaWindow : Window
    {
        public int ResultID { get; private set; } = 0;

        private readonly int _id;
        private readonly string _tipo;
        private DataRow _encabezado;
        private DataTable _detalle;

        public DetalleVentaWindow()
        {
            InitializeComponent();
            Loaded += DetalleVentaWindow_Loaded;
        }

        public DetalleVentaWindow(int id, string cliente) : this()
        {
            _id = id;
            _tipo = "Factura";
            lblCliente.Text = cliente;
        }

        public DetalleVentaWindow(int id, string cliente, string tipo) : this()
        {
            _id = id;
            _tipo = tipo ?? "Factura";
            lblCliente.Text = cliente;
        }

        public DetalleVentaWindow(object param) : this() { }
        public DetalleVentaWindow(object p1, object p2) : this()
        {
            if (p1 is int i) _id = i;
            if (p2 is string s) { lblCliente.Text = s; _tipo = "Factura"; }
        }
        public DetalleVentaWindow(object p1, object p2, object p3) : this()
        {
            if (p1 is int i) _id = i;
            if (p2 is string s) lblCliente.Text = s;
            if (p3 is string t) _tipo = t;
        }

        private void DetalleVentaWindow_Loaded(object sender, RoutedEventArgs e)
        {
            CargarDatos();
        }

        private void CargarDatos()
        {
            try
            {
                if (_tipo == "Presupuesto")
                {
                    Title = "Detalle de Presupuesto";
                    lblTitulo.Text = "Detalle de Presupuesto";
                    _encabezado = DatabaseService.GetPresupuestoPorID(_id);
                    _detalle = DatabaseService.GetPresupuestoDetalle(_id);

                    if (_encabezado != null)
                    {
                        lblTipo.Text = "Presupuesto";
                        lblFecha.Text = Convert.ToDateTime(_encabezado["Fecha"]).ToString("dd/MM/yyyy HH:mm");
                        lblNumero.Text = _encabezado["PresupuestoID"].ToString();
                        lblTotal.Text = Convert.ToDecimal(_encabezado["Total"]).ToString("C2");
                        lblEstado.Text = _encabezado["Estado"]?.ToString() ?? "—";
                    }
                }
                else
                {
                    Title = "Detalle de Factura";
                    lblTitulo.Text = "Detalle de Factura / Venta";
                    var dtF = DatabaseService.GetFacturasPorFecha(DateTime.MinValue, DateTime.MaxValue);
                    DataRow row = null;
                    foreach (DataRow r in dtF.Rows)
                        if (Convert.ToInt32(r["FacturaID"]) == _id) { row = r; break; }

                    _detalle = DatabaseService.GetFacturaDetalle(_id);

                    if (row != null)
                    {
                        lblTipo.Text = row["TipoComprobante"]?.ToString() ?? "—";
                        lblFecha.Text = Convert.ToDateTime(row["Fecha"]).ToString("dd/MM/yyyy HH:mm");
                        int? nro = row["NumeroComprobanteAFIP"] != DBNull.Value ? (int?)Convert.ToInt32(row["NumeroComprobanteAFIP"]) : null;
                        lblNumero.Text = nro.HasValue ? nro.Value.ToString() : "—";
                        lblTotal.Text = Convert.ToDecimal(row["Total"]).ToString("C2");
                        lblEstado.Text = "Emitida";

                        string cae = row["CAE"]?.ToString();
                        if (!string.IsNullOrWhiteSpace(cae))
                        {
                            pnlCae.Visibility = Visibility.Visible;
                            lblCae.Text = cae;
                            lblVtoCae.Text = row["VencimientoCAE"]?.ToString() ?? "—";
                        }
                    }
                }

                if (string.IsNullOrWhiteSpace(lblCliente.Text))
                    lblCliente.Text = "—";

                // Poblar grilla
                if (_detalle != null)
                {
                    var items = new List<DetalleVentaItem>();
                    foreach (DataRow r in _detalle.Rows)
                    {
                        items.Add(new DetalleVentaItem
                        {
                            Codigo = r.Table.Columns.Contains("Codigo") ? r["Codigo"]?.ToString() ?? "" : "",
                            Descripcion = r.Table.Columns.Contains("Descripcion") ? r["Descripcion"]?.ToString() ?? "" : "",
                            Cantidad = r.Table.Columns.Contains("Cantidad") ? Convert.ToInt32(r["Cantidad"]) : 0,
                            PrecioUnitario = r.Table.Columns.Contains("PrecioUnitario") ? Convert.ToDecimal(r["PrecioUnitario"]) : 0
                        });
                    }
                    dgvDetalle.ItemsSource = items;
                }
            }
            catch (Exception ex)
            {
                ModernMessageBox.Show("Error al cargar detalle: " + ex.Message);
            }
        }

        private void btnImprimir_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_tipo == "Presupuesto")
                {
                    PrintService.ImprimirPresupuesto(_id);
                }
                else
                {
                    ModernMessageBox.Show("Impresión de facturas desde este panel no disponible aún.\nUse la opción de impresión al guardar la venta.",
                        "Información", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                ModernMessageBox.Show("Error al imprimir: " + ex.Message);
            }
        }

        private void btnCerrar_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private class DetalleVentaItem
        {
            public string Codigo { get; set; }
            public string Descripcion { get; set; }
            public int Cantidad { get; set; }
            public decimal PrecioUnitario { get; set; }
            public decimal Subtotal => Cantidad * PrecioUnitario;
            public string PrecioUnitarioFmt => PrecioUnitario.ToString("C2");
            public string SubtotalFmt => Subtotal.ToString("C2");
        }
    }
}
