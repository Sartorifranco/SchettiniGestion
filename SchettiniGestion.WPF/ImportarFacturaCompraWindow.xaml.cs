using System;
using System.Collections.ObjectModel;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using SchettiniGestion;

namespace SchettiniGestion.WPF
{
    public partial class ImportarFacturaCompraWindow : Window
    {
        public int ProveedorID { get; private set; }
        public string ProveedorNombre { get; private set; } = "";
        public string TipoComprobante { get; private set; } = "Factura A";
        public ObservableCollection<LineaRevisionVm> LineasConfirmadas { get; private set; }

        private readonly ObservableCollection<LineaRevisionVm> _lineas = new ObservableCollection<LineaRevisionVm>();
        private FacturaCompraPdfImportResult _import;

        public ImportarFacturaCompraWindow()
        {
            InitializeComponent();
            dgvLineas.ItemsSource = _lineas;
            Loaded += (_, __) => UiScaleHelper.FitWindowToWorkArea(this, 980, 640, 700, 480);
        }

        public ImportarFacturaCompraWindow(Window owner) : this()
        {
            Owner = owner;
        }

        /// <summary>Abre diálogo de archivo, parsea y muestra revisión. Devuelve false si el usuario cancela el OpenFile.</summary>
        public bool CargarDesdeDialogo()
        {
            var dlg = new OpenFileDialog
            {
                Title = "Seleccionar factura PDF",
                Filter = "PDF (*.pdf)|*.pdf",
                CheckFileExists = true
            };
            if (dlg.ShowDialog(this) != true) return false;
            return CargarDesdeArchivo(dlg.FileName);
        }

        public bool CargarDesdeArchivo(string rutaPdf)
        {
            try
            {
                _import = FacturaCompraPdfService.ImportarConMatching(rutaPdf);
            }
            catch (Exception ex)
            {
                ModernMessageBox.Show("No se pudo leer el PDF:\n" + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
            AplicarResultadoImportacion();
            return true;
        }

        public async Task<bool> CargarDesdeFotoAsync(string rutaImagen)
        {
            try
            {
                Title = "Importar foto de factura";
                _import = await Task.Run(() => FacturaCompraOcrService.ImportarDesdeFoto(rutaImagen));
            }
            catch (Exception ex)
            {
                ModernMessageBox.Show("No se pudo analizar la foto:\n" + ex.Message, "Error de OCR",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
            AplicarResultadoImportacion();
            return true;
        }

        private void AplicarResultadoImportacion()
        {
            ProveedorID = _import.ProveedorID;
            ProveedorNombre = _import.ProveedorNombre ?? "";
            if (ProveedorID > 0)
                lblProveedor.Text = $"Proveedor: {ProveedorNombre}  (CUIT {_import.Parse.CuitEmisor})";
            else if (!string.IsNullOrWhiteSpace(_import.Parse.CuitEmisor))
                lblProveedor.Text = $"Proveedor no encontrado (CUIT {_import.Parse.CuitEmisor}). Selecciónelo luego en la factura.";
            else
                lblProveedor.Text = "Proveedor: (sin detectar en el PDF)";

            string nro = string.IsNullOrWhiteSpace(_import.Parse.NumeroComprobante) ? "" : $"  |  N° {_import.Parse.NumeroComprobante}";
            string rs = string.IsNullOrWhiteSpace(_import.Parse.RazonSocialEmisor) ? "" : $"  |  {_import.Parse.RazonSocialEmisor}";
            lblCabecera.Text = $"Detectado: {_import.Parse.TipoComprobante}{nro}{rs}";
            lblAdvertencia.Text = _import.Parse.MensajeAdvertencia ?? "";

            SeleccionarTipo(_import.Parse.TipoComprobante);

            _lineas.Clear();
            foreach (var m in _import.Lineas)
            {
                _lineas.Add(new LineaRevisionVm
                {
                    CodigoPdf = m.LineaPdf?.CodigoProveedor ?? "",
                    DescripcionPdf = m.LineaPdf?.DescripcionPdf ?? "",
                    Cantidad = m.LineaPdf?.Cantidad ?? 1,
                    CostoUnitario = m.LineaPdf?.CostoUnitario ?? 0,
                    ProductoID = m.ProductoID,
                    CodigoProducto = m.CodigoProducto ?? "",
                    DescripcionProducto = m.ProductoID > 0 ? m.DescripcionProducto : "(elegir producto)",
                    OrigenMatch = m.OrigenMatch ?? "Sin match",
                    Confianza = m.Confianza
                });
            }
            ActualizarResumen();
        }

        private void SeleccionarTipo(string tipo)
        {
            tipo = tipo ?? "Factura A";
            for (int i = 0; i < cmbTipoComprobante.Items.Count; i++)
            {
                if ((cmbTipoComprobante.Items[i] as ComboBoxItem)?.Content?.ToString() == tipo)
                {
                    cmbTipoComprobante.SelectedIndex = i;
                    return;
                }
            }
            cmbTipoComprobante.SelectedIndex = 0;
        }

        private void ActualizarResumen()
        {
            int ok = _lineas.Count(l => l.ProductoID > 0 && l.Estado == "OK");
            int rev = _lineas.Count(l => l.ProductoID > 0 && l.Estado == "Revisar");
            int sin = _lineas.Count(l => l.ProductoID <= 0);
            decimal total = _lineas.Sum(l => l.Cantidad * l.CostoUnitario);
            lblResumen.Text = $"{_lineas.Count} ítems — OK: {ok} — Revisar: {rev} — Sin match: {sin} — Total estimado: {total:C2}";
        }

        private void btnElegirProducto_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is FrameworkElement fe) || !(fe.DataContext is LineaRevisionVm linea)) return;
            AbrirAsignacionProducto(linea);
        }

        private void AbrirAsignacionProducto(LineaRevisionVm linea)
        {
            var selector = new AsignarProductoCompraWindow(
                this, linea.CodigoPdf, linea.DescripcionPdf, linea.CostoUnitario);
            if (selector.ShowDialog() != true) return;

            linea.ProductoID = selector.ProductoID;
            linea.CodigoProducto = selector.CodigoProducto;
            linea.DescripcionProducto = selector.DescripcionProducto;
            linea.OrigenMatch = "Manual";
            linea.Confianza = 1m;
            dgvLineas.Items.Refresh();
            ActualizarResumen();
        }

        private void btnAgregarLineaFaltante_Click(object sender, RoutedEventArgs e)
        {
            var modal = new LineaFacturaCompraManualWindow(this);
            if (modal.ShowDialog() != true) return;

            var linea = new LineaRevisionVm
            {
                CodigoPdf = modal.Codigo,
                DescripcionPdf = modal.Descripcion,
                Cantidad = modal.Cantidad,
                CostoUnitario = modal.CostoUnitario,
                DescripcionProducto = "(elegir producto)",
                OrigenMatch = "Carga manual",
                Confianza = 0m
            };
            _lineas.Add(linea);
            ActualizarResumen();
            AbrirAsignacionProducto(linea);
        }

        private void btnConfirmar_Click(object sender, RoutedEventArgs e)
        {
            if (_lineas.Count == 0)
            {
                ModernMessageBox.Show("No hay ítems para importar.", "Atención", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            var sinMatch = _lineas.Where(l => l.ProductoID <= 0).ToList();
            if (sinMatch.Count > 0)
            {
                if (ModernMessageBox.Show(
                        $"{sinMatch.Count} ítem(s) sin producto asignado se omitirán. ¿Continuar con el resto?",
                        "Confirmar", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                    return;
            }

            var ok = _lineas.Where(l => l.ProductoID > 0).ToList();
            if (ok.Count == 0)
            {
                ModernMessageBox.Show("Asigne al menos un producto.", "Atención", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (ProveedorID > 0)
            {
                foreach (var l in ok)
                    DatabaseService.GuardarAliasProductoProveedor(ProveedorID, l.DescripcionPdf, l.ProductoID, l.CodigoPdf);
            }

            ProveedorNombre = ProveedorNombre ?? "";
            TipoComprobante = (cmbTipoComprobante.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Factura A";
            LineasConfirmadas = new ObservableCollection<LineaRevisionVm>(ok);
            DialogResult = true;
            Close();
        }

        private void btnCancelar_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        public class LineaRevisionVm
        {
            public string CodigoPdf { get; set; }
            public string DescripcionPdf { get; set; }
            public int Cantidad { get; set; }
            public decimal CostoUnitario { get; set; }
            public string CostoFmt => CostoUnitario.ToString("C2");
            public int ProductoID { get; set; }
            public string CodigoProducto { get; set; }
            public string DescripcionProducto { get; set; }
            public string OrigenMatch { get; set; }
            public decimal Confianza { get; set; }
            public string Estado
            {
                get
                {
                    if (ProductoID <= 0) return "Sin match";
                    if (Confianza >= 0.85m || OrigenMatch == "Código" || OrigenMatch == "Alias" || OrigenMatch == "Manual")
                        return "OK";
                    return "Revisar";
                }
            }
        }
    }
}
