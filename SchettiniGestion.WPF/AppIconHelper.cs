using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace SchettiniGestion.WPF
{
    internal static class AppIconHelper
    {
        private static readonly Uri PackIconUri =
            new Uri("pack://application:,,,/SchettiniGestion.WPF;component/Resources/app.ico", UriKind.Absolute);

        private static ImageSource _cachedIcon;

        public static ImageSource LoadWindowIcon()
        {
            if (_cachedIcon != null)
                return _cachedIcon;

            try
            {
                var frame = BitmapFrame.Create(PackIconUri);
                frame.Freeze();
                _cachedIcon = frame;
                return _cachedIcon;
            }
            catch
            {
                try
                {
                    var logo = SvgLogoHelper.LoadEmbeddedLogo();
                    if (logo == null)
                        return null;

                    var bmp = RenderDrawingToBitmap(logo, 64);
                    if (bmp == null)
                        return null;

                    bmp.Freeze();
                    _cachedIcon = bmp;
                    return _cachedIcon;
                }
                catch
                {
                    return null;
                }
            }
        }

        public static void ApplyToAllWindows()
        {
            var icon = LoadWindowIcon();
            if (icon == null)
                return;

            EventManager.RegisterClassHandler(
                typeof(Window),
                FrameworkElement.LoadedEvent,
                new RoutedEventHandler(OnWindowLoadedApplyIcon),
                true);
        }

        private static void OnWindowLoadedApplyIcon(object sender, RoutedEventArgs e)
        {
            if (!(sender is Window window) || window.Icon != null)
                return;

            var icon = LoadWindowIcon();
            if (icon != null)
                window.Icon = icon;
        }

        private static BitmapSource RenderDrawingToBitmap(DrawingImage drawingImage, int size)
        {
            if (drawingImage?.Drawing == null)
                return null;

            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                var bounds = drawingImage.Drawing.Bounds;
                if (bounds.Width <= 0 || bounds.Height <= 0)
                    return null;

                double scale = Math.Min(size / bounds.Width, size / bounds.Height);
                dc.PushTransform(new ScaleTransform(scale, scale));
                dc.DrawDrawing(drawingImage.Drawing);
            }

            var rtb = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(visual);
            return rtb;
        }
    }
}
