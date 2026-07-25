using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
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
                    CargarEncabezadoDocumento("Presupuesto", "PresupuestoID", "Total");
                }
                else if (_tipo == "Remito")
                {
                    Title = "Detalle de Remito";
                    lblTitulo.Text = "Detalle de Remito";
                    _encabezado = DatabaseService.GetRemitoPorID(_id);
                    _detalle = DatabaseService.GetRemitoDetalle(_id);
                    CargarEncabezadoDocumento("Remito", "RemitoID", null);
                }
                else if (_tipo == "Pedido")
                {
                    Title = "Detalle de Pedido";
                    lblTitulo.Text = "Detalle de Pedido";
                    _encabezado = DatabaseService.GetPedidoPorID(_id);
                    _detalle = DatabaseService.GetPedidoDetalle(_id);
                    CargarEncabezadoDocumento("Pedido", "PedidoID", "Total");
                }
                else
                {
                    Title = "Detalle de Factura";
                    lblTitulo.Text = "Detalle de Factura / Venta";
                    btnNotaCredito.Visibility = Visibility.Visible;
                    _encabezado = DatabaseService.GetFacturaPorID(_id);
                    _detalle = DatabaseService.GetFacturaDetalle(_id);

                    if (_encabezado != null)
                    {
                        lblTipo.Text = _encabezado["TipoComprobante"]?.ToString() ?? "—";
                        lblFecha.Text = Convert.ToDateTime(_encabezado["Fecha"]).ToString("dd/MM/yyyy HH:mm");
                        int? nro = _encabezado["NumeroComprobanteAFIP"] != DBNull.Value ? (int?)Convert.ToInt32(_encabezado["NumeroComprobanteAFIP"]) : null;
                        lblNumero.Text = nro.HasValue ? nro.Value.ToString() : "—";
                        lblTotal.Text = Convert.ToDecimal(_encabezado["Total"]).ToString("C2");
                        lblEstado.Text = "Emitida";
                        if (_encabezado.Table.Columns.Contains("NombrePersonal"))
                            lblPersonal.Text = string.IsNullOrWhiteSpace(_encabezado["NombrePersonal"]?.ToString())
                                ? "—" : _encabezado["NombrePersonal"].ToString();
                        else
                            lblPersonal.Text = "—";
                        if (string.IsNullOrWhiteSpace(lblCliente.Text) || lblCliente.Text == "—")
                            lblCliente.Text = _encabezado["ClienteNombre"]?.ToString() ?? "—";

                        string cae = _encabezado["CAE"]?.ToString();
                        if (!string.IsNullOrWhiteSpace(cae))
                        {
                            pnlCae.Visibility = Visibility.Visible;
                            lblCae.Text = cae;
                            lblVtoCae.Text = _encabezado["VencimientoCAE"]?.ToString() ?? "—";
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
                            Cantidad = r.Table.Columns.Contains("Cantidad") ? Convert.ToDecimal(r["Cantidad"]) : 0,
                            PrecioUnitario = r.Table.Columns.Contains("PrecioUnitario") ? Convert.ToDecimal(r["PrecioUnitario"]) : 0
                        });
                    }
                    dgvDetalle.ItemsSource = items;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al cargar detalle: " + ex.Message);
            }
        }

        private void CargarEncabezadoDocumento(string tipo, string idColumn, string totalColumn)
        {
            if (_encabezado == null) return;

            lblTipo.Text = tipo;
            lblFecha.Text = Convert.ToDateTime(_encabezado["Fecha"]).ToString("dd/MM/yyyy HH:mm");
            lblNumero.Text = _encabezado[idColumn].ToString();
            if (!string.IsNullOrEmpty(totalColumn) && _encabezado.Table.Columns.Contains(totalColumn))
                lblTotal.Text = Convert.ToDecimal(_encabezado[totalColumn]).ToString("C2");
            else if (_detalle != null && _detalle.Rows.Count > 0)
                lblTotal.Text = _detalle.AsEnumerable().Sum(r => Convert.ToDecimal(r["Subtotal"])).ToString("C2");
            else
                lblTotal.Text = "—";
            lblEstado.Text = _encabezado.Table.Columns.Contains("Estado") ? _encabezado["Estado"]?.ToString() ?? "—" : "—";
            if (_encabezado.Table.Columns.Contains("ClienteNombre") && string.IsNullOrWhiteSpace(lblCliente.Text))
                lblCliente.Text = _encabezado["ClienteNombre"]?.ToString();
        }

        private void btnImprimir_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                switch (_tipo)
                {
                    case "Presupuesto": PrintService.ImprimirPresupuesto(_id); break;
                    case "Remito": PrintService.ImprimirRemito(_id); break;
                    case "Pedido": PrintService.ImprimirPedido(_id); break;
                    default: PrintService.ImprimirFactura(_id); break;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al imprimir: " + ex.Message);
            }
        }

        private void btnNotaCredito_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var win = new NotaCreditoVentaWindow(_id)
                {
                    Owner = this
                };
                if (win.ShowDialog() == true)
                    ResultID = win.ResultID;
            }
            catch (Exception ex)
            {
                MessageBox.Show("No se pudo generar la nota de crédito: " + ex.Message);
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
            public decimal Cantidad { get; set; }
            public decimal PrecioUnitario { get; set; }
            public decimal Subtotal => Cantidad * PrecioUnitario;
            public string PrecioUnitarioFmt => PrecioUnitario.ToString("C2");
            public string SubtotalFmt => Subtotal.ToString("C2");
        }
    }
}
