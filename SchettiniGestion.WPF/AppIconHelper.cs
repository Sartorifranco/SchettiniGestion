using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace SchettiniGestion.WPF
{
    internal static class AppIconHelper
    {
        private const int WmSetIcon = 0x80;
        private const int IconSmall = 0;
        private const int IconBig = 1;

        private static readonly Uri PackPngUri =
            new Uri("pack://application:,,,/Resources/app-icon.png", UriKind.Absolute);

        private static readonly Uri PackIcoUri =
            new Uri("pack://application:,,,/Resources/app.ico", UriKind.Absolute);

        private static ImageSource _cachedIcon;
        private static Icon _nativeSmall;
        private static Icon _nativeBig;
        private static bool _aumidSet;

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern int SetCurrentProcessExplicitAppUserModelID(string appID);

        public static ImageSource LoadWindowIcon()
        {
            if (_cachedIcon != null)
                return _cachedIcon;

            try
            {
                var png = new BitmapImage();
                png.BeginInit();
                png.UriSource = PackPngUri;
                png.CacheOption = BitmapCacheOption.OnLoad;
                png.EndInit();
                png.Freeze();
                _cachedIcon = png;
                return _cachedIcon;
            }
            catch { /* recurso opcional */ }

            try
            {
                var decoder = BitmapDecoder.Create(
                    PackIcoUri,
                    BitmapCreateOptions.PreservePixelFormat,
                    BitmapCacheOption.OnLoad);
                var frame = decoder.Frames
                    .OrderByDescending(f => f.PixelWidth * f.PixelHeight)
                    .FirstOrDefault();
                if (frame != null)
                {
                    frame.Freeze();
                    _cachedIcon = frame;
                    return _cachedIcon;
                }
            }
            catch { }

            return null;
        }

        public static void ApplyToAllWindows()
        {
            EnsureAppUserModelId();
            EnsureNativeIcons();

            EventManager.RegisterClassHandler(
                typeof(Window),
                FrameworkElement.LoadedEvent,
                new RoutedEventHandler(OnWindowLoadedApplyIcon),
                true);
        }

        private static void EnsureAppUserModelId()
        {
            if (_aumidSet)
                return;
            try
            {
                SetCurrentProcessExplicitAppUserModelID("SCHPOS.Gestion");
                _aumidSet = true;
            }
            catch { }
        }

        private static string ResolveIcoPath()
        {
            var besideExe = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app.ico");
            if (File.Exists(besideExe))
                return besideExe;

            try
            {
                var streamInfo = Application.GetResourceStream(PackIcoUri);
                if (streamInfo == null)
                    return null;
                var temp = Path.Combine(Path.GetTempPath(), "schpos-app.ico");
                using (var input = streamInfo.Stream)
                using (var output = File.Create(temp))
                    input.CopyTo(output);
                return temp;
            }
            catch
            {
                return null;
            }
        }

        private static void EnsureNativeIcons()
        {
            if (_nativeSmall != null)
                return;

            var path = ResolveIcoPath();
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return;

            try
            {
                _nativeSmall = new Icon(path, 32, 32);
                _nativeBig = new Icon(path, 256, 256);
            }
            catch
            {
                try
                {
                    _nativeSmall = new Icon(path);
                }
                catch { }
            }
        }

        private static void OnWindowLoadedApplyIcon(object sender, RoutedEventArgs e)
        {
            if (!(sender is Window window))
                return;

            var icon = LoadWindowIcon();
            if (icon != null)
                window.Icon = icon;

            EnsureNativeIcons();
            try
            {
                var hwnd = new WindowInteropHelper(window).EnsureHandle();
                if (_nativeBig != null)
                    SendMessage(hwnd, WmSetIcon, (IntPtr)IconBig, _nativeBig.Handle);
                if (_nativeSmall != null)
                    SendMessage(hwnd, WmSetIcon, (IntPtr)IconSmall, _nativeSmall.Handle);
            }
            catch { }
        }
    }
}
