using SchettiniGestion;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SchettiniGestion.WPF
{
    public partial class ConfigurarEtiquetaWindow : Window
    {
        private static readonly string[] Presets =
        {
            "30 x 20 mm", "50 x 25 mm", "50 x 30 mm", "55 x 44 mm", "64 x 32 mm",
            "80 x 40 mm", "100 x 80 mm", "100 x 100 mm", "100 x 150 mm", "Personalizado"
        };

        private bool _cargando;
        public bool Guardado { get; private set; }

        public ConfigurarEtiquetaWindow()
        {
            InitializeComponent();
            Loaded += Window_Loaded;
            MouseLeftButtonDown += (_, e) =>
            {
                if (e.ButtonState == System.Windows.Input.MouseButtonState.Pressed)
                    DragMove();
            };
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            UiScaleHelper.FitWindowToWorkArea(this, 920, 640, 720, 500);
            _cargando = true;
            try
            {
                cmbPreset.ItemsSource = Presets;
                var op = DatabaseService.GetOpcionesEtiqueta();
                txtAncho.Text = op.AnchoMm.ToString();
                txtAlto.Text = op.AltoMm.ToString();
                cmbPreset.SelectedItem = Presets.Contains($"{op.AnchoMm} x {op.AltoMm} mm")
                    ? $"{op.AnchoMm} x {op.AltoMm} mm" : "Personalizado";
                cmbOrientacion.SelectedIndex =
                    string.Equals(op.Orientacion, "Horizontal", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
                SeleccionarComboPorTexto(cmbModo, op.ModoImpresion, 0);

                chkNombre.IsChecked = op.MostrarDescripcion;
                chkDescripcion.IsChecked = op.MostrarDescripcionExtra;
                chkPrecio.IsChecked = op.MostrarPrecio;
                chkCodigo.IsChecked = op.MostrarCodigo;
                chkBarras.IsChecked = op.MostrarCodigoBarras;
                chkMarca.IsChecked = op.MostrarMarca;

                txtColumnas.Text = op.Columnas.ToString();
                txtGapH.Text = op.GapHorizontalMm.ToString();
                txtGapV.Text = op.GapVerticalMm.ToString();
                txtMargenIzq.Text = op.MargenIzquierdoMm.ToString();
                txtMargenSup.Text = op.MargenSuperiorMm.ToString();
                txtMargenDer.Text = op.MargenDerechoMm.ToString();
                txtMargenInf.Text = op.MargenInferiorMm.ToString();

                string impresora = DatabaseService.GetImpresoraEtiquetas();
                txtImpresora.Text = string.IsNullOrWhiteSpace(impresora)
                    ? "Impresora: sin configurar. Se solicitará al imprimir."
                    : $"Impresora configurada: {impresora}";
            }
            finally { _cargando = false; }
            ActualizarPreview();
        }

        private static void SeleccionarComboPorTexto(ComboBox combo, string texto, int defecto)
        {
            for (int i = 0; i < combo.Items.Count; i++)
            {
                if (combo.Items[i] is ComboBoxItem item &&
                    string.Equals(item.Content?.ToString(), texto, StringComparison.OrdinalIgnoreCase))
                {
                    combo.SelectedIndex = i;
                    return;
                }
            }
            combo.SelectedIndex = defecto;
        }

        private void cmbPreset_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_cargando) return;
            string preset = cmbPreset.SelectedItem?.ToString() ?? "";
            if (preset.StartsWith("Personalizado", StringComparison.OrdinalIgnoreCase)) return;
            var partes = preset.Replace("mm", "").Split('x');
            if (partes.Length == 2)
            {
                _cargando = true;
                txtAncho.Text = partes[0].Trim();
                txtAlto.Text = partes[1].Trim();
                _cargando = false;
                ActualizarPreview();
            }
        }

        private void Medida_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_cargando) return;
            int ancho = LeerEntero(txtAncho?.Text, 50, 10, 300);
            int alto = LeerEntero(txtAlto?.Text, 25, 10, 300);
            string preset = $"{ancho} x {alto} mm";
            _cargando = true;
            cmbPreset.SelectedItem = Presets.Contains(preset) ? preset : "Personalizado";
            _cargando = false;
            ActualizarPreview();
        }

        private void btnIntercambiar_Click(object sender, RoutedEventArgs e)
        {
            string ancho = txtAncho.Text;
            _cargando = true;
            txtAncho.Text = txtAlto.Text;
            txtAlto.Text = ancho;
            _cargando = false;
            ActualizarPreview();
        }

        private void cmbOrientacion_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_cargando) ActualizarPreview();
        }

        private void ActualizarPreview()
        {
            if (previewEtiqueta == null) return;
            int ancho = LeerEntero(txtAncho?.Text, 50, 10, 300);
            int alto = LeerEntero(txtAlto?.Text, 25, 10, 300);
            const double maxW = 230;
            const double maxH = 190;
            double escala = Math.Min(maxW / ancho, maxH / alto);
            previewEtiqueta.Width = Math.Max(60, ancho * escala);
            previewEtiqueta.Height = Math.Max(45, alto * escala);
            previewContenido.LayoutTransform = cmbOrientacion?.SelectedIndex == 1
                ? new RotateTransform(90)
                : Transform.Identity;
            if (txtMedidaPreview != null)
                txtMedidaPreview.Text = $"{ancho} × {alto} mm · " +
                    (cmbOrientacion?.SelectedIndex == 1 ? "contenido girado 90°" : "contenido sin girar");
        }

        private OpcionesEtiqueta LeerOpciones()
        {
            return new OpcionesEtiqueta
            {
                AnchoMm = LeerEntero(txtAncho.Text, 50, 10, 300),
                AltoMm = LeerEntero(txtAlto.Text, 25, 10, 300),
                Orientacion = cmbOrientacion.SelectedIndex == 1 ? "Horizontal" : "Vertical",
                ModoImpresion = (cmbModo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Rollo",
                MostrarDescripcion = chkNombre.IsChecked == true,
                MostrarDescripcionExtra = chkDescripcion.IsChecked == true,
                MostrarPrecio = chkPrecio.IsChecked == true,
                MostrarCodigo = chkCodigo.IsChecked == true,
                MostrarCodigoBarras = chkBarras.IsChecked == true,
                MostrarMarca = chkMarca.IsChecked == true,
                Columnas = LeerEntero(txtColumnas.Text, 3, 1, 12),
                GapHorizontalMm = LeerEntero(txtGapH.Text, 2, 0, 50),
                GapVerticalMm = LeerEntero(txtGapV.Text, 2, 0, 50),
                MargenIzquierdoMm = LeerEntero(txtMargenIzq.Text, 5, 0, 50),
                MargenSuperiorMm = LeerEntero(txtMargenSup.Text, 5, 0, 50),
                MargenDerechoMm = LeerEntero(txtMargenDer.Text, 5, 0, 50),
                MargenInferiorMm = LeerEntero(txtMargenInf.Text, 5, 0, 50)
            };
        }

        private static int LeerEntero(string texto, int defecto, int min, int max)
        {
            if (!int.TryParse((texto ?? "").Trim(), out int valor)) valor = defecto;
            return Math.Max(min, Math.Min(max, valor));
        }

        private bool Guardar()
        {
            var op = LeerOpciones();
            if (!op.MostrarDescripcion && !op.MostrarDescripcionExtra && !op.MostrarPrecio &&
                !op.MostrarCodigo && !op.MostrarCodigoBarras && !op.MostrarMarca)
            {
                ModernMessageBox.Show("Seleccioná al menos un dato para mostrar en la etiqueta.",
                    "Configuración de etiquetas", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            string impresora = DatabaseService.GetImpresoraEtiquetas();
            if (!DatabaseService.GuardarConfigEtiquetas(impresora, op))
            {
                ModernMessageBox.Show("No se pudo guardar la configuración.", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
            Guardado = true;
            return true;
        }

        private void btnGuardar_Click(object sender, RoutedEventArgs e)
        {
            if (!Guardar()) return;
            DialogResult = true;
            Close();
        }

        private void btnGuardarProbar_Click(object sender, RoutedEventArgs e)
        {
            var op = LeerOpciones();
            if (!op.MostrarDescripcion && !op.MostrarDescripcionExtra && !op.MostrarPrecio &&
                !op.MostrarCodigo && !op.MostrarCodigoBarras && !op.MostrarMarca)
            {
                ModernMessageBox.Show("Seleccioná al menos un dato para mostrar en la etiqueta.",
                    "Configuración de etiquetas", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string impresora = DatabaseService.GetImpresoraEtiquetas();
            if (string.IsNullOrWhiteSpace(impresora))
            {
                ModernMessageBox.Show("Configurá primero una impresora de etiquetas en Configuración → Impresoras.",
                    "Impresora no configurada", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Prueba con lo que se ve en pantalla, sin persistir hasta "Guardar configuración".
            PrintService.ImprimirPaginaDePrueba(impresora, "Etiqueta", op);
        }

        private void btnCancelar_Click(object sender, RoutedEventArgs e) => Close();
    }
}
