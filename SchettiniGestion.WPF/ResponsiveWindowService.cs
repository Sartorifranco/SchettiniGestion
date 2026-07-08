using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace SchettiniGestion.WPF
{
    /// <summary>
    /// Envuelve automáticamente el contenido de ventanas modales y de arranque en un Viewbox
    /// que escala hacia abajo en monitores pequeños (sin agrandar en pantallas grandes).
    /// </summary>
    public static class ResponsiveWindowService
    {
        private static readonly DependencyProperty WrappedProperty =
            DependencyProperty.RegisterAttached(
                "ResponsiveWrapped",
                typeof(bool),
                typeof(ResponsiveWindowService),
                new PropertyMetadata(false));

        private static readonly HashSet<Type> ExcludedTypes = new HashSet<Type>
        {
            typeof(PrincipalWindow),
            typeof(VisorClienteWindow)
        };

        public static void Initialize()
        {
            EventManager.RegisterClassHandler(
                typeof(Window),
                FrameworkElement.LoadedEvent,
                new RoutedEventHandler(OnWindowLoaded),
                true);
        }

        private static void OnWindowLoaded(object sender, RoutedEventArgs e)
        {
            var window = sender as Window;
            if (window == null || ExcludedTypes.Contains(window.GetType()))
                return;

            if ((bool)window.GetValue(WrappedProperty))
                return;

            window.Dispatcher.BeginInvoke(
                new Action(() => TryWrapWindow(window)),
                DispatcherPriority.Loaded);
        }

        private static void TryWrapWindow(Window window)
        {
            if (window == null || (bool)window.GetValue(WrappedProperty))
                return;

            var content = window.Content as UIElement;
            if (content == null)
                return;

            double designW = ResolveDesignWidth(window, content);
            double designH = ResolveDesignHeight(window, content);

            if (designW <= 0 || designH <= 0)
                return;

            window.Content = null;

            var host = new Grid
            {
                Width = designW,
                Height = designH
            };
            host.Children.Add(content);

            var viewbox = new Viewbox
            {
                Stretch = Stretch.Uniform,
                StretchDirection = StretchDirection.DownOnly,
                Child = host
            };

            window.Content = viewbox;
            window.SetValue(WrappedProperty, true);

            if (window.SizeToContent != SizeToContent.Manual)
                window.SizeToContent = SizeToContent.Manual;

            FitWindowToScreen(window, designW, designH);
            window.SizeChanged += (_, __) => FitWindowToScreen(window, designW, designH);
        }

        private static double ResolveDesignWidth(Window window, UIElement content)
        {
            if (!double.IsNaN(window.Width) && window.Width > 0)
                return window.Width;

            content.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            if (content.DesiredSize.Width > 0)
                return content.DesiredSize.Width;

            return 520;
        }

        private static double ResolveDesignHeight(Window window, UIElement content)
        {
            if (!double.IsNaN(window.Height) && window.Height > 0)
                return window.Height;

            double width = ResolveDesignWidth(window, content);
            content.Measure(new Size(width, double.PositiveInfinity));
            if (content.DesiredSize.Height > 0)
                return Math.Max(content.DesiredSize.Height, 180);

            return 420;
        }

        private static void FitWindowToScreen(Window window, double designW, double designH)
        {
            var area = SystemParameters.WorkArea;
            double maxW = area.Width * 0.96;
            double maxH = area.Height * 0.92;
            double scale = Math.Min(1.0, Math.Min(maxW / designW, maxH / designH));

            window.MaxWidth = maxW;
            window.MaxHeight = maxH;
            window.Width = designW * scale;
            window.Height = designH * scale;
        }
    }
}
