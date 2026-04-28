using System.Windows;

namespace SchettiniGestion.WPF
{
    public partial class InputWindow : Window
    {
        public string ResultText { get; private set; }
        public string ResponseText => ResultText;

        public InputWindow(string titulo) : this(titulo, titulo) { }

        public InputWindow(string titulo, string etiqueta, string valorInicial = "")
        {
            Title = titulo;
            Width = 400; Height = 180;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ResizeMode = ResizeMode.NoResize;
            Background = System.Windows.Media.Brushes.White;

            var grid = new System.Windows.Controls.Grid { Margin = new Thickness(16) };
            grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = System.Windows.GridLength.Auto });
            grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = System.Windows.GridLength.Auto });
            grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = System.Windows.GridLength.Auto });

            var lbl = new System.Windows.Controls.TextBlock { Text = etiqueta, Margin = new Thickness(0, 0, 0, 6) };
            System.Windows.Controls.Grid.SetRow(lbl, 0);

            var txt = new System.Windows.Controls.TextBox { Text = valorInicial, Margin = new Thickness(0, 0, 0, 12), Padding = new Thickness(6) };
            System.Windows.Controls.Grid.SetRow(txt, 1);

            var panel = new System.Windows.Controls.StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var btnOk = new System.Windows.Controls.Button { Content = "Aceptar", Width = 90, Height = 30, Margin = new Thickness(0, 0, 8, 0) };
            var btnCancelar = new System.Windows.Controls.Button { Content = "Cancelar", Width = 90, Height = 30 };
            btnOk.Click += (s, e) => { ResultText = txt.Text; DialogResult = true; };
            btnCancelar.Click += (s, e) => { DialogResult = false; };
            panel.Children.Add(btnOk);
            panel.Children.Add(btnCancelar);
            System.Windows.Controls.Grid.SetRow(panel, 2);

            grid.Children.Add(lbl);
            grid.Children.Add(txt);
            grid.Children.Add(panel);
            Content = grid;
        }
    }
}
