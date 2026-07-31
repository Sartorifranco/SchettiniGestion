using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;

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

        private static readonly DependencyProperty PendingTimerProperty =
            DependencyProperty.RegisterAttached(
                "ResponsivePendingTimer",
                typeof(DispatcherTimer),
                typeof(ResponsiveModuleService),
                new PropertyMetadata(null));

        private static readonly DependencyProperty LastBreakpointProperty =
            DependencyProperty.RegisterAttached(
                "ResponsiveLastBreakpoint",
                typeof(string),
                typeof(ResponsiveModuleService),
                new PropertyMetadata(null));

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
            control.SizeChanged += (_, __) => ScheduleApply(control);
            control.Loaded += (_, __) => ScheduleApply(control);
        }

        private static void ScheduleApply(UserControl control)
        {
            if (control == null) return;

            var timer = control.GetValue(PendingTimerProperty) as DispatcherTimer;
            if (timer == null)
            {
                timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(140) };
                timer.Tick += (s, e) =>
                {
                    timer.Stop();
                    Apply(control);
                };
                control.SetValue(PendingTimerProperty, timer);
            }

            timer.Stop();
            timer.Start();
        }

        private static void Apply(UserControl control)
        {
            if (!control.IsLoaded || !control.IsVisible || control.ActualWidth <= 0)
                return;

            double anchoViewport = UiScaleHelper.GetViewportWidthForModule(control);
            double altoViewport = UiScaleHelper.GetViewportHeightForModule(control);

            bool compacto = UiScaleHelper.IsCompactWidth(anchoViewport);
            bool muyCompacto = UiScaleHelper.IsVeryCompactWidth(anchoViewport)
                               || UiScaleHelper.IsCompactHeight(altoViewport);
            string breakpoint = (compacto ? "C" : "N") + (muyCompacto ? "M" : "F")
                + ((int)(anchoViewport / 40));

            string previo = control.GetValue(LastBreakpointProperty) as string;
            if (string.Equals(previo, breakpoint, StringComparison.Ordinal))
                return;
            control.SetValue(LastBreakpointProperty, breakpoint);

            if (control.Content is Panel rootPanel)
                rootPanel.Margin = UiScaleHelper.ContentMargin(compacto);

            AdjustVisualTree(control, compacto, muyCompacto, anchoViewport);
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
                if (fe is DataGrid dg)
                    dg.HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;

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
