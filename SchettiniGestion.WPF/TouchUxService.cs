using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace SchettiniGestion.WPF
{
    /// <summary>
    /// Scroll táctil por zona. Solo se activa si Windows reporta digitalizador táctil.
    /// En PC con mouse/teclado no se engancha nada: el sistema sigue igual que antes.
    /// En caja táctil, el mouse físico sigue funcionando; el gesto nuevo es solo con el dedo.
    /// </summary>
    internal static class TouchUxService
    {
        private const double UmbralPanPx = 14;
        private const int SM_DIGITIZER = 94;
        private const int SM_MAXIMUMTOUCHES = 95;
        private const int NID_INTEGRATED_TOUCH = 0x01;
        private const int NID_EXTERNAL_TOUCH = 0x02;

        private static bool _inicializado;
        private static readonly ConditionalWeakTable<Window, PanState> _estados =
            new ConditionalWeakTable<Window, PanState>();

        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int nIndex);

        public static bool HayPantallaTactil()
        {
            try
            {
                if (GetSystemMetrics(SM_MAXIMUMTOUCHES) > 0)
                    return true;
                int digitizer = GetSystemMetrics(SM_DIGITIZER);
                return (digitizer & (NID_INTEGRATED_TOUCH | NID_EXTERNAL_TOUCH)) != 0;
            }
            catch
            {
                return false;
            }
        }

        public static void Initialize()
        {
            if (_inicializado) return;
            _inicializado = true;

            // PC de escritorio sin táctil: no registrar handlers. Mouse y rueda quedan intactos.
            if (!HayPantallaTactil())
                return;

            EventManager.RegisterClassHandler(
                typeof(Window),
                FrameworkElement.LoadedEvent,
                new RoutedEventHandler(OnWindowLoaded),
                true);
        }

        private static void OnWindowLoaded(object sender, RoutedEventArgs e)
        {
            var window = sender as Window;
            if (window == null) return;

            PanState existing;
            if (_estados.TryGetValue(window, out existing) && existing.Enganchada)
                return;

            var state = new PanState();
            _estados.Remove(window);
            _estados.Add(window, state);
            state.Enganchada = true;

            Stylus.SetIsPressAndHoldEnabled(window, false);
            Stylus.SetIsFlicksEnabled(window, false);
            Stylus.SetIsTapFeedbackEnabled(window, false);

            window.PreviewTouchDown += OnPreviewTouchDown;
            window.PreviewTouchMove += OnPreviewTouchMove;
            window.PreviewTouchUp += OnPreviewTouchUp;
            window.LostTouchCapture += OnLostTouchCapture;
            window.PreviewMouseMove += OnPreviewMouseMove;
            window.PreviewMouseLeftButtonUp += OnPreviewMouseLeftButtonUp;
            window.ManipulationBoundaryFeedback += OnManipulationBoundaryFeedback;
            window.Closed += OnWindowClosed;
        }

        private static void OnWindowClosed(object sender, EventArgs e)
        {
            var window = sender as Window;
            if (window == null) return;
            window.PreviewTouchDown -= OnPreviewTouchDown;
            window.PreviewTouchMove -= OnPreviewTouchMove;
            window.PreviewTouchUp -= OnPreviewTouchUp;
            window.LostTouchCapture -= OnLostTouchCapture;
            window.PreviewMouseMove -= OnPreviewMouseMove;
            window.PreviewMouseLeftButtonUp -= OnPreviewMouseLeftButtonUp;
            window.ManipulationBoundaryFeedback -= OnManipulationBoundaryFeedback;
            window.Closed -= OnWindowClosed;
        }

        private static void OnManipulationBoundaryFeedback(object sender, ManipulationBoundaryFeedbackEventArgs e)
        {
            // Evita que toda la ventana "rebote" cuando un panel llega al tope.
            e.Handled = true;
        }

        private static void OnPreviewTouchDown(object sender, TouchEventArgs e)
        {
            var window = sender as Window;
            var state = GetState(window);
            if (window == null || state == null) return;

            OcultarCursor();
            state.Reset();
            state.SuprimirClic = false;
            state.Activo = true;
            state.TouchId = e.TouchDevice.Id;
            state.Origen = e.OriginalSource as DependencyObject;
            state.Ultimo = e.GetTouchPoint(window).Position;
            state.Inicio = state.Ultimo;
        }

        private static void OnPreviewTouchMove(object sender, TouchEventArgs e)
        {
            var window = sender as Window;
            var state = GetState(window);
            if (window == null || state == null || !state.Activo) return;
            if (e.TouchDevice.Id != state.TouchId) return;

            Point ahora = e.GetTouchPoint(window).Position;
            Vector deltaTotal = ahora - state.Inicio;

            if (!state.Paneando)
            {
                if (Math.Abs(deltaTotal.X) < UmbralPanPx && Math.Abs(deltaTotal.Y) < UmbralPanPx)
                    return;
                if (EsCapturaNativa(state.Origen))
                {
                    state.Reset();
                    return;
                }

                state.Target = BuscarScrollViewer(state.Origen, deltaTotal);
                if (state.Target == null)
                {
                    state.Reset();
                    return;
                }

                state.Paneando = true;
                state.SuprimirClic = true;
                Mouse.Capture(null);
                try { e.TouchDevice.Capture(window); } catch { }
            }

            if (!state.Paneando || state.Target == null) return;

            Vector paso = ahora - state.Ultimo;
            AplicarPan(state.Target, paso);
            state.Ultimo = ahora;
            e.Handled = true;
        }

        private static void OnPreviewTouchUp(object sender, TouchEventArgs e)
        {
            var window = sender as Window;
            var state = GetState(window);
            if (window == null || state == null) return;
            if (state.Activo && e.TouchDevice.Id != state.TouchId) return;

            bool fuePan = state.Paneando;
            if (fuePan)
                e.Handled = true;

            try { window.ReleaseAllTouchCaptures(); } catch { }
            if (window.IsMouseCaptured)
                window.ReleaseMouseCapture();

            state.Reset();
            if (fuePan)
                state.SuprimirClic = true;
        }

        private static void OnLostTouchCapture(object sender, TouchEventArgs e)
        {
            var state = GetState(sender as Window);
            if (state == null || !state.Paneando) return;
            // Si perdimos captura a mitad de gesto, no dispares el clic al soltar.
            state.SuprimirClic = true;
        }

        private static void OnPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            var state = GetState(sender as Window);
            if (state == null || !state.SuprimirClic) return;
            e.Handled = true;
            state.SuprimirClic = false;
        }

        private static void OnPreviewMouseMove(object sender, MouseEventArgs e)
        {
            var state = GetState(sender as Window);
            if (state != null && state.Paneando)
            {
                e.Handled = true;
                return;
            }

            // Mouse físico (no promoción de táctil): volver a mostrar el cursor.
            if (e.StylusDevice == null)
                MostrarCursor();
        }

        private static void AplicarPan(ScrollViewer sv, Vector paso)
        {
            if (sv == null) return;

            // El contenido sigue el dedo: dedo abajo → offset vertical baja.
            if (sv.ScrollableHeight > 0.5 && Math.Abs(paso.Y) >= 0.1)
            {
                double y = sv.VerticalOffset - paso.Y;
                if (y < 0) y = 0;
                if (y > sv.ScrollableHeight) y = sv.ScrollableHeight;
                sv.ScrollToVerticalOffset(y);
            }

            if (sv.ScrollableWidth > 0.5 && Math.Abs(paso.X) >= 0.1)
            {
                double x = sv.HorizontalOffset - paso.X;
                if (x < 0) x = 0;
                if (x > sv.ScrollableWidth) x = sv.ScrollableWidth;
                sv.ScrollToHorizontalOffset(x);
            }
        }

        private static ScrollViewer BuscarScrollViewer(DependencyObject origen, Vector delta)
        {
            bool vertical = Math.Abs(delta.Y) >= Math.Abs(delta.X);
            ScrollViewer masInterno = null;
            var d = origen;

            while (d != null)
            {
                var sv = d as ScrollViewer;
                if (sv != null && sv.IsVisible && sv.IsEnabled)
                {
                    if (masInterno == null)
                        masInterno = sv;

                    if (vertical && sv.ScrollableHeight > 1)
                    {
                        if (delta.Y > 0 && sv.VerticalOffset > 0.5)
                            return sv;
                        if (delta.Y < 0 && sv.VerticalOffset < sv.ScrollableHeight - 0.5)
                            return sv;
                    }
                    else if (!vertical && sv.ScrollableWidth > 1)
                    {
                        if (delta.X > 0 && sv.HorizontalOffset > 0.5)
                            return sv;
                        if (delta.X < 0 && sv.HorizontalOffset < sv.ScrollableWidth - 0.5)
                            return sv;
                    }
                }

                d = GetParent(d);
            }

            if (masInterno != null)
            {
                bool puede = vertical ? masInterno.ScrollableHeight > 1 : masInterno.ScrollableWidth > 1;
                if (puede) return masInterno;
            }

            return null;
        }

        private static bool EsCapturaNativa(DependencyObject origen)
        {
            int hops = 0;
            var d = origen;
            while (d != null && hops < 10)
            {
                if (d is TextBoxBase || d is PasswordBox)
                    return true;
                if (d is Thumb || d is Slider || d is ScrollBar)
                    return true;
                d = GetParent(d);
                hops++;
            }
            return false;
        }

        private static DependencyObject GetParent(DependencyObject current)
        {
            if (current == null) return null;
            if (current is Visual || current is System.Windows.Media.Media3D.Visual3D)
            {
                var visualParent = VisualTreeHelper.GetParent(current);
                if (visualParent != null) return visualParent;
            }
            return LogicalTreeHelper.GetParent(current);
        }

        private static PanState GetState(Window window)
        {
            if (window == null) return null;
            PanState state;
            return _estados.TryGetValue(window, out state) ? state : null;
        }

        private static void OcultarCursor()
        {
            if (Mouse.OverrideCursor != Cursors.None)
                Mouse.OverrideCursor = Cursors.None;
        }

        private static void MostrarCursor()
        {
            if (Mouse.OverrideCursor != null)
                Mouse.OverrideCursor = null;
        }

        private sealed class PanState
        {
            public bool Enganchada;
            public bool Activo;
            public bool Paneando;
            public bool SuprimirClic;
            public int TouchId;
            public Point Inicio;
            public Point Ultimo;
            public DependencyObject Origen;
            public ScrollViewer Target;

            public void Reset()
            {
                Activo = false;
                Paneando = false;
                TouchId = -1;
                Origen = null;
                Target = null;
            }
        }
    }
}
