using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace SchettiniGestion.WPF
{
    /// <summary>
    /// Ventana Topmost que hospeda el teclado.
    /// Va ENCIMA de los ShowDialog (Nuevo Producto, etc.) sin quedar atrapada
    /// dentro del HWND de PrincipalWindow. No usa Owner=Principal para no
    /// deshabilitarse durante un modal.
    /// </summary>
    public partial class VirtualKeyboardShellWindow : Window
    {
        private Window _trackedAnchor;
        private const int WM_MOUSEACTIVATE = 0x0021;
        private const int MA_NOACTIVATE = 3;
        private const uint SWP_NOZORDER = 0x0004;
        private const uint SWP_NOACTIVATE = 0x0010;
        private const uint MONITOR_DEFAULTTONEAREST = 2;

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT { public int Left, Top, Right, Bottom; }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int X, Y; }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct MONITORINFO
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public int dwFlags;
        }

        [DllImport("user32.dll")]
        private static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        private static extern bool ClientToScreen(IntPtr hWnd, ref POINT lpPoint);

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(
            IntPtr hWnd, IntPtr hWndInsertAfter,
            int X, int Y, int cx, int cy, uint uFlags);

        public VirtualKeyboardShellWindow()
        {
            InitializeComponent();
            Closing += (s, e) => { e.Cancel = true; Hide(); };
            IsVisibleChanged += (s, e) =>
            {
                if (IsVisible)
                {
                    AttachAnchorTracking();
                    Dispatcher.BeginInvoke(new Action(() => PositionToAnchor(false)),
                        System.Windows.Threading.DispatcherPriority.Loaded);
                }
            };
        }

        public VirtualKeyboardControl Keyboard => keyboard;

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            (PresentationSource.FromVisual(this) as HwndSource)?.AddHook(WndProc);
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            // No robar el foco del TextBox del modal.
            if (msg == WM_MOUSEACTIVATE) { handled = true; return new IntPtr(MA_NOACTIVATE); }
            return IntPtr.Zero;
        }

        public void SetMode(VirtualKeyboardMode mode)
        {
            bool same = keyboard.CurrentMode == mode && IsVisible;
            keyboard.SetMode(mode);
            if (!same || !IsVisible)
                PositionToAnchor(animate: false);
        }

        public new void Show()
        {
            AttachAnchorTracking();
            base.Show();
            PositionToAnchor(animate: true);
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (IsVisible) PositionToAnchor(false);
            }), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        }

        private void AttachAnchorTracking()
        {
            var anchor = GetAnchorWindow();
            if (anchor == null || ReferenceEquals(anchor, this)) return;

            if (_trackedAnchor != null && !ReferenceEquals(_trackedAnchor, anchor))
            {
                _trackedAnchor.LocationChanged -= OnAnchorChanged;
                _trackedAnchor.SizeChanged -= OnAnchorChanged;
                _trackedAnchor.StateChanged -= OnAnchorChanged;
            }

            if (!ReferenceEquals(_trackedAnchor, anchor))
            {
                _trackedAnchor = anchor;
                _trackedAnchor.LocationChanged += OnAnchorChanged;
                _trackedAnchor.SizeChanged += OnAnchorChanged;
                _trackedAnchor.StateChanged += OnAnchorChanged;
            }
        }

        private void OnAnchorChanged(object sender, EventArgs e)
        {
            if (IsVisible) PositionToAnchor(false);
        }

        private static Window GetAnchorWindow()
        {
            var app = Application.Current;
            if (app == null) return null;

            foreach (Window w in app.Windows)
            {
                if (w is PrincipalWindow pw && pw.IsLoaded && pw.IsVisible)
                    return pw;
            }

            foreach (Window w in app.Windows)
            {
                if (w is LoginWindow lw && lw.IsLoaded && lw.IsVisible)
                    return lw;
            }

            return app.MainWindow;
        }

        /// <summary>
        /// Principal → área cliente de SCHPOS.
        /// Login → work area del monitor del login (teclado grande, puede salir del login).
        /// </summary>
        private static Rect GetAnchorBoundsDip(Window anchor)
        {
            if (anchor == null)
                return SystemParameters.WorkArea;

            try
            {
                var hwnd = new WindowInteropHelper(anchor).Handle;
                if (hwnd == IntPtr.Zero)
                    hwnd = new WindowInteropHelper(anchor).EnsureHandle();

                var source = PresentationSource.FromVisual(anchor);
                var fromDevice = source?.CompositionTarget?.TransformFromDevice
                                 ?? Matrix.Identity;

                if (anchor is PrincipalWindow && hwnd != IntPtr.Zero
                    && GetClientRect(hwnd, out RECT client))
                {
                    var tlPx = new POINT { X = 0, Y = 0 };
                    var brPx = new POINT { X = client.Right, Y = client.Bottom };
                    ClientToScreen(hwnd, ref tlPx);
                    ClientToScreen(hwnd, ref brPx);
                    Point tl = fromDevice.Transform(new Point(tlPx.X, tlPx.Y));
                    Point br = fromDevice.Transform(new Point(brPx.X, brPx.Y));
                    if (br.X > tl.X + 80 && br.Y > tl.Y + 80)
                        return new Rect(tl, br);
                }

                // Login (u otros): work area del monitor donde está la ventana.
                if (hwnd != IntPtr.Zero)
                {
                    IntPtr mon = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
                    var mi = new MONITORINFO { cbSize = Marshal.SizeOf(typeof(MONITORINFO)) };
                    if (mon != IntPtr.Zero && GetMonitorInfo(mon, ref mi))
                    {
                        Point tl = fromDevice.Transform(new Point(mi.rcWork.Left, mi.rcWork.Top));
                        Point br = fromDevice.Transform(new Point(mi.rcWork.Right, mi.rcWork.Bottom));
                        return new Rect(tl, br);
                    }
                }
            }
            catch { /* fallback */ }

            return SystemParameters.WorkArea;
        }

        public void PositionToAnchor(bool animate)
        {
            var anchor = GetAnchorWindow();
            AttachAnchorTracking();
            Rect bounds = GetAnchorBoundsDip(anchor);

            bool numeric = keyboard.CurrentMode == VirtualKeyboardMode.Numeric;

            // Medir alto del control con el ancho objetivo.
            double targetWidth = numeric
                ? Math.Min(360, Math.Max(300, bounds.Width - 24))
                : Math.Max(320, bounds.Width);

            keyboard.Measure(new Size(targetWidth, double.PositiveInfinity));
            double height = keyboard.DesiredSize.Height;
            if (height < 80)
                height = numeric ? 340 : 390;

            double left = numeric
                ? Math.Max(bounds.Left + 8, bounds.Right - targetWidth - 10)
                : bounds.Left;

            double top = bounds.Bottom - height;
            if (top < bounds.Top + 40)
                top = bounds.Top + 40;

            SizeToContent = SizeToContent.Manual;
            Width = targetWidth;
            Height = height;
            MinWidth = targetWidth;
            MaxWidth = targetWidth;

            // Asegurar modo visual (stretch / right) coherente con el ancho del shell.
            keyboard.Width = targetWidth;
            keyboard.HorizontalAlignment = HorizontalAlignment.Stretch;

            Left = left;
            Top = top;

            ApplySetWindowPos(left, top, targetWidth, height, anchor);
        }

        private void ApplySetWindowPos(double leftDip, double topDip, double widthDip, double heightDip, Window anchor)
        {
            try
            {
                var hwnd = new WindowInteropHelper(this).Handle;
                if (hwnd == IntPtr.Zero) return;

                var visual = (anchor != null && anchor.IsLoaded) ? (Visual)anchor : this;
                var source = PresentationSource.FromVisual(visual) ?? PresentationSource.FromVisual(this);
                if (source?.CompositionTarget == null) return;

                var toDevice = source.CompositionTarget.TransformToDevice;
                Point tl = toDevice.Transform(new Point(leftDip, topDip));
                Point br = toDevice.Transform(new Point(leftDip + widthDip, topDip + heightDip));

                int x = (int)Math.Round(tl.X);
                int y = (int)Math.Round(tl.Y);
                int cx = Math.Max(1, (int)Math.Round(br.X - tl.X));
                int cy = Math.Max(1, (int)Math.Round(br.Y - tl.Y));

                SetWindowPos(hwnd, IntPtr.Zero, x, y, cx, cy, SWP_NOZORDER | SWP_NOACTIVATE);
            }
            catch { /* Left/Top ya puestos */ }
        }
    }
}
