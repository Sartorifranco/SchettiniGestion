using System.Windows;
using System.Windows.Controls;
using Xceed.Wpf.Toolkit;

namespace SchettiniGestion.WPF
{
    /// <summary>Ventana modal de ingreso/egreso de caja (un solo .cs para evitar problemas de XAML en el .csproj).</summary>
    public class CajaMovimientoModalWindow : Window
    {
        private readonly DecimalUpDown _numMonto;
        private readonly TextBox _txtConcepto;

        public decimal Monto { get; private set; }
        public string Concepto { get; private set; }

        public CajaMovimientoModalWindow(string titulo, string textoBotonGuardar = "Guardar")
        {
            Title = titulo;
            Width = 480;
            Height = 420;
            WindowStartupLocation = System.Windows.WindowStartupLocation.CenterOwner;
            WindowStyle = System.Windows.WindowStyle.None;
            ResizeMode = System.Windows.ResizeMode.NoResize;
            AllowsTransparency = true;
            Background = System.Windows.Media.Brushes.Transparent;

            var root = new Border
            {
                CornerRadius = new CornerRadius(12),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(24)
            };
            root.SetResourceReference(Border.BackgroundProperty, "PanelBackgroundBrush");
            root.SetResourceReference(Border.BorderBrushProperty, "BorderColor");

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var header = new Grid { Margin = new Thickness(0, 0, 0, 20) };
            var lblTitulo = new TextBlock
            {
                Text = titulo,
                FontSize = 22,
                FontWeight = FontWeights.Bold,
                VerticalAlignment = VerticalAlignment.Center
            };
            lblTitulo.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimary");
            header.Children.Add(lblTitulo);

            var headerButtons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            var btnCerrar = CrearBotonSecundario("✕", 48, 48, "Cerrar");
            btnCerrar.Click += (_, __) => { DialogResult = false; Close(); };
            headerButtons.Children.Add(btnCerrar);
            header.Children.Add(headerButtons);

            Grid.SetRow(header, 0);
            grid.Children.Add(header);

            var fields = new StackPanel();
            fields.Children.Add(CrearEtiqueta("Monto"));
            _numMonto = new DecimalUpDown
            {
                FormatString = "C2",
                Minimum = 0,
                MinHeight = 48,
                FontSize = 18,
                Margin = new Thickness(0, 0, 0, 18),
                CultureInfo = AppCulture.Argentine
            };
            _numMonto.SetResourceReference(Control.StyleProperty, "ModuleNumericUpDownStyle");
            fields.Children.Add(_numMonto);

            fields.Children.Add(CrearEtiqueta("Concepto / Motivo"));
            _txtConcepto = new TextBox
            {
                MinHeight = 100,
                TextWrapping = TextWrapping.Wrap,
                AcceptsReturn = true,
                VerticalContentAlignment = VerticalAlignment.Top
            };
            _txtConcepto.SetResourceReference(Control.StyleProperty, "ModuleTextBoxStyle");
            fields.Children.Add(_txtConcepto);

            Grid.SetRow(fields, 1);
            grid.Children.Add(fields);

            var footer = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 20, 0, 0)
            };
            var btnCancelar = CrearBotonSecundario("Cancelar", 120, 48);
            btnCancelar.Margin = new Thickness(0, 0, 10, 0);
            btnCancelar.Click += (_, __) => { DialogResult = false; Close(); };
            footer.Children.Add(btnCancelar);

            var btnGuardar = new Button
            {
                Content = textoBotonGuardar,
                MinWidth = 120,
                MinHeight = 48
            };
            btnGuardar.SetResourceReference(Control.StyleProperty, "ButtonStyle");
            btnGuardar.Click += BtnGuardar_Click;
            footer.Children.Add(btnGuardar);

            Grid.SetRow(footer, 2);
            grid.Children.Add(footer);

            root.Child = grid;
            Content = root;
        }

        private static TextBlock CrearEtiqueta(string texto)
        {
            var tb = new TextBlock { Text = texto, Margin = new Thickness(0, 0, 0, 6) };
            tb.SetResourceReference(TextBlock.StyleProperty, "ModalCaptionTextStyle");
            return tb;
        }

        private static Button CrearBotonSecundario(string content, double minWidth, double minHeight, string toolTip = null)
        {
            var btn = new Button
            {
                Content = content,
                MinWidth = minWidth,
                MinHeight = minHeight
            };
            btn.SetResourceReference(Control.StyleProperty, "SecondaryButtonStyle");
            if (!string.IsNullOrEmpty(toolTip))
                btn.ToolTip = toolTip;
            return btn;
        }

        private void BtnGuardar_Click(object sender, RoutedEventArgs e)
        {
            Monto = _numMonto.Value ?? 0;
            Concepto = _txtConcepto.Text?.Trim() ?? "";
            DialogResult = true;
            Close();
        }
    }
}
