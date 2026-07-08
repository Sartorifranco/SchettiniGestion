using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace SchettiniGestion.WPF
{
    /// <summary>
    /// Ajusta automáticamente márgenes, columnas rígidas, popups y textos de ayuda
    /// en todos los UserControl cargados en el área principal.
    /// </summary>
    public static class ResponsiveModuleService
    {
        private static readonly DependencyProperty HookedProperty =
            DependencyProperty.RegisterAttached(
                "ResponsiveModuleHooked",
                typeof(bool),
                typeof(ResponsiveModuleService),
                new PropertyMetadata(false));

        public static void Initialize()
        {
            EventManager.RegisterClassHandler(
                typeof(UserControl),
                FrameworkElement.LoadedEvent,
                new RoutedEventHandler(OnUserControlLoaded),
                true);
        }

        private static void OnUserControlLoaded(object sender, RoutedEventArgs e)
        {
            var control = sender as UserControl;
            if (control == null || (bool)control.GetValue(HookedProperty))
                return;

            control.SetValue(HookedProperty, true);
            control.SizeChanged += (_, __) => Apply(control);
            control.Loaded += (_, __) => Apply(control);
        }

        private static void Apply(UserControl control)
        {
            if (!control.IsLoaded || control.ActualWidth <= 0)
                return;

            bool compacto = UiScaleHelper.IsCompactWidth(control.ActualWidth);
            bool muyCompacto = UiScaleHelper.IsVeryCompactWidth(control.ActualWidth)
                               || UiScaleHelper.IsCompactHeight(control.ActualHeight);

            if (control.Content is Panel rootPanel)
                rootPanel.Margin = UiScaleHelper.ContentMargin(compacto);

            AdjustVisualTree(control, compacto, muyCompacto, control.ActualWidth);
        }

        private static void AdjustVisualTree(DependencyObject node, bool compacto, bool muyCompacto, double anchoModulo)
        {
            if (node == null)
                return;

            if (node is Grid grid)
            {
                foreach (var col in grid.ColumnDefinitions)
                {
                    if (compacto && col.MinWidth >= 280)
                        col.MinWidth = 220;
                    else if (!compacto && col.MinWidth == 220)
                        col.MinWidth = 280;
                }
            }

            if (node is FrameworkElement fe)
            {
                if (muyCompacto && fe is TextBlock tb && tb.TextWrapping == TextWrapping.Wrap && tb.FontSize <= 11)
                {
                    string name = tb.Name ?? string.Empty;
                    if (name.IndexOf("Ayuda", StringComparison.OrdinalIgnoreCase) >= 0
                        || name.IndexOf("Hint", StringComparison.OrdinalIgnoreCase) >= 0)
                        tb.Visibility = Visibility.Collapsed;
                }

                if (fe is ListBox lb && lb.Width >= 400 && double.IsNaN(lb.MaxWidth))
                {
                    lb.MaxWidth = Math.Max(260, anchoModulo * 0.92);
                    lb.Width = double.NaN;
                }

                if (fe is Border b && b.MinWidth >= 380)
                    b.MinWidth = compacto ? 280 : 380;
            }

            int count = VisualTreeHelper.GetChildrenCount(node);
            for (int i = 0; i < count; i++)
                AdjustVisualTree(VisualTreeHelper.GetChild(node, i), compacto, muyCompacto, anchoModulo);
        }
    }
}
