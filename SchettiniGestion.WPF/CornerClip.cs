using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SchettiniGestion.WPF
{
    /// <summary>
    /// Recorta el contenido de un Border a su CornerRadius.
    /// WPF no recorta solo con ClipToBounds: el header/scrollbar del DataGrid queda cuadrado.
    /// </summary>
    public static class CornerClip
    {
        public static readonly DependencyProperty EnableProperty =
            DependencyProperty.RegisterAttached(
                "Enable",
                typeof(bool),
                typeof(CornerClip),
                new PropertyMetadata(false, OnEnableChanged));

        public static bool GetEnable(DependencyObject obj) => (bool)obj.GetValue(EnableProperty);
        public static void SetEnable(DependencyObject obj, bool value) => obj.SetValue(EnableProperty, value);

        private static void OnEnableChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (!(d is Border border)) return;

            if ((bool)e.NewValue)
            {
                border.SizeChanged += OnSizeChanged;
                border.Loaded += OnLoaded;
                Aplicar(border);
            }
            else
            {
                border.SizeChanged -= OnSizeChanged;
                border.Loaded -= OnLoaded;
                border.Clip = null;
            }
        }

        private static void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (sender is Border border) Aplicar(border);
        }

        private static void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (sender is Border border) Aplicar(border);
        }

        private static void Aplicar(Border border)
        {
            double w = border.ActualWidth;
            double h = border.ActualHeight;
            if (w < 1 || h < 1)
            {
                border.Clip = null;
                return;
            }

            var r = border.CornerRadius;
            double rx = r.TopLeft;
            if (rx < 0.5) rx = 0;
            border.Clip = new RectangleGeometry(new Rect(0, 0, w, h), rx, rx);
        }
    }
}
