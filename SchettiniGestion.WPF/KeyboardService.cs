using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using SchettiniGestion.WPF.Properties;

namespace SchettiniGestion.WPF
{
    public enum VirtualKeyboardMode { Alpha, Numeric }

    /// <summary>
    /// Servicio global de teclado virtual.
    /// Usa una ventana Topmost (sin Owner=Principal) para quedar ENCIMA de los
    /// ShowDialog, anclada al área de PrincipalWindow o al work area del login.
    /// </summary>
    public static class KeyboardService
    {
        private static VirtualKeyboardShellWindow _shell;
        private static WeakReference<TextBox> _activeTextBox;
        private static WeakReference<PasswordBox> _activePasswordBox;
        private static Window _liftedModal;
        private static double _liftedModalOriginalTop = double.NaN;

        public static bool IsEnabled { get; private set; }
        public static double KeyboardTop { get; private set; } = double.MaxValue;
        public static event Action<bool> VisibilityChanged;
        public static event Action EnabledChanged;

        public static bool IsVisible
            => _shell != null && _shell.IsVisible;

        public static void LoadSavedPreference()
        {
            try { IsEnabled = Settings.Default.KeyboardEnabled; }
            catch { IsEnabled = false; }
            if (!IsEnabled) Hide();
            EnabledChanged?.Invoke();
        }

        public static void SetEnabled(bool enabled)
        {
            if (IsEnabled == enabled) return;
            IsEnabled = enabled;
            try
            {
                Settings.Default.KeyboardEnabled = enabled;
                Settings.Default.Save();
            }
            catch { /* sin userSettings en config antiguo */ }
            if (!IsEnabled) Hide();
            EnabledChanged?.Invoke();
        }

        public static void Toggle() => SetEnabled(!IsEnabled);

        private static readonly string[] AlphaForceKeywords =
        {
            "descripcion", "nombre", "razon", "direccion", "email", "mail", "usuario",
            "password", "clave", "obs", "nota", "comentario", "busc", "filtro",
            "codigo", "barra", "cuit", "cuil", "marca", "categoria", "proveedor",
            "cliente", "licencia", "ip", "servidor", "host", "path", "ruta", "archivo"
        };

        private static readonly string[] NumericNameKeywords =
        {
            "precio", "importe", "monto", "costo", "cantidad", "descuento",
            "stock", "saldo", "interes", "tasa", "telefono", "fax", "postal",
            "numero", "puntoventa", "puntos", "cuotas", "dias", "iva", "porcentaje",
            "ancho", "alto", "margen", "gap", "columna", "cant", "total", "pago",
            "efectivo", "vuelto", "recargo", "bonificacion", "mm"
        };

        public static void Initialize()
        {
            EventManager.RegisterClassHandler(
                typeof(TextBox),
                UIElement.GotKeyboardFocusEvent,
                new KeyboardFocusChangedEventHandler(OnTextBoxFocused));

            EventManager.RegisterClassHandler(
                typeof(PasswordBox),
                UIElement.GotKeyboardFocusEvent,
                new KeyboardFocusChangedEventHandler(OnPasswordBoxFocused));

            EventManager.RegisterClassHandler(
                typeof(UIElement),
                UIElement.LostKeyboardFocusEvent,
                new KeyboardFocusChangedEventHandler(OnAnyLostFocus));
        }

        private static void OnTextBoxFocused(object sender, KeyboardFocusChangedEventArgs e)
        {
            if (!IsEnabled) return;
            if (!(sender is TextBox tb)) return;
            if (tb.IsReadOnly) return;

            _activeTextBox = new WeakReference<TextBox>(tb);
            _activePasswordBox = null;
            Show(DetectMode(tb));
        }

        private static void OnPasswordBoxFocused(object sender, KeyboardFocusChangedEventArgs e)
        {
            if (!IsEnabled) return;
            if (!(sender is PasswordBox pb)) return;

            _activePasswordBox = new WeakReference<PasswordBox>(pb);
            _activeTextBox = null;
            Show(VirtualKeyboardMode.Alpha);
        }

        private static void OnAnyLostFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            if (e.NewFocus is TextBox || e.NewFocus is PasswordBox)
                return;

            Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
            {
                var focused = Keyboard.FocusedElement;
                if (focused is TextBox || focused is PasswordBox)
                    return;

                // No ocultar al sacar captura / Alt-Tab (la app pierde el foco).
                var main = Application.Current?.MainWindow;
                if (focused == null && main != null && !main.IsActive)
                    return;

                Hide();
            }), DispatcherPriority.Background);
        }

        private static VirtualKeyboardMode DetectMode(TextBox tb)
        {
            var tag = (tb.Tag as string)?.Trim().ToLowerInvariant() ?? "";
            if (tag == "alpha" || tag == "text" || tag == "alfanumerico")
                return VirtualKeyboardMode.Alpha;
            if (tag == "numeric" || tag == "number" || tag == "decimal" ||
                tag == "precio" || tag == "cantidad" || tag == "monto")
                return VirtualKeyboardMode.Numeric;

            if (tb.AcceptsReturn || (tb.TextWrapping == TextWrapping.Wrap && tb.MinLines > 1))
                return VirtualKeyboardMode.Alpha;

            string name = (tb.Name ?? "").ToLowerInvariant();
            string tip = (tb.ToolTip as string)?.ToLowerInvariant() ?? "";
            string combined = name + " " + tip;

            foreach (var kw in AlphaForceKeywords)
            {
                if (combined.Contains(kw))
                    return VirtualKeyboardMode.Alpha;
            }

            if (IsInsideNumericSpinner(tb))
                return VirtualKeyboardMode.Numeric;

            foreach (var kw in NumericNameKeywords)
            {
                if (name.Contains(kw))
                    return VirtualKeyboardMode.Numeric;
            }

            if (tb.InputScope?.Names?.Count > 0 && tb.InputScope.Names[0] is InputScopeName scopeName)
            {
                var sv = scopeName.NameValue;
                if (sv == InputScopeNameValue.Number ||
                    sv == InputScopeNameValue.TelephoneNumber ||
                    sv == InputScopeNameValue.CurrencyAmount ||
                    sv == InputScopeNameValue.Digits)
                    return VirtualKeyboardMode.Numeric;
            }

            if (!string.IsNullOrWhiteSpace(tb.Text) &&
                tb.Text.Trim().Length <= 18 &&
                LooksLikeNumber(tb.Text))
                return VirtualKeyboardMode.Numeric;

            return VirtualKeyboardMode.Alpha;
        }

        private static bool LooksLikeNumber(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;
            string t = text.Trim().Replace(" ", "");
            if (t.StartsWith("-")) t = t.Substring(1);
            if (t.Length == 0) return false;
            int separators = 0;
            foreach (char c in t)
            {
                if (c == '.' || c == ',')
                {
                    separators++;
                    if (separators > 1) return false;
                    continue;
                }
                if (!char.IsDigit(c)) return false;
            }
            return true;
        }

        private static bool IsInsideNumericSpinner(DependencyObject element)
        {
            var parent = VisualTreeHelper.GetParent(element);
            int depth = 0;
            while (parent != null && depth < 12)
            {
                var typeName = parent.GetType().Name;
                if (typeName.EndsWith("UpDown", StringComparison.Ordinal))
                    return true;
                if (parent is Window || (parent is UserControl && depth > 3))
                    break;
                parent = VisualTreeHelper.GetParent(parent);
                depth++;
            }
            return false;
        }

        public static void Show(VirtualKeyboardMode mode)
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null) return;

            // No bloquear el hilo de UI con Invoke síncrono en cada foco de TextBox.
            if (dispatcher.CheckAccess())
                ShowCore(mode);
            else
                dispatcher.BeginInvoke(new Action(() => ShowCore(mode)), DispatcherPriority.Input);
        }

        private static void ShowCore(VirtualKeyboardMode mode)
        {
            EnsureShell();
            bool wasVisible = _shell.IsVisible;
            VirtualKeyboardMode prev = wasVisible ? _shell.Keyboard.CurrentMode : (VirtualKeyboardMode)(-1);

            _shell.SetMode(mode);

            if (!wasVisible)
                _shell.Show();
            else if (prev != mode)
                _shell.PositionToAnchor(false);

            KeyboardTop = _shell.Top;

            if (!wasVisible || prev != mode)
            {
                LiftActiveModalIfNeeded();
                VisibilityChanged?.Invoke(true);
            }
        }

        public static void Hide()
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null) return;

            Action hide = () =>
            {
                RestoreLiftedModal();
                _shell?.Hide();
                KeyboardTop = double.MaxValue;
                VisibilityChanged?.Invoke(false);
            };

            if (dispatcher.CheckAccess()) hide();
            else dispatcher.BeginInvoke(hide, DispatcherPriority.Background);
        }

        private static void EnsureShell()
        {
            if (_shell == null || !_shell.IsLoaded)
                _shell = new VirtualKeyboardShellWindow();
        }

        /// <summary>
        /// Sube el modal activo (Nuevo Producto, etc.) para que no tape el teclado Topmost.
        /// </summary>
        private static void LiftActiveModalIfNeeded()
        {
            if (_shell == null || !_shell.IsVisible) return;

            Window modal = null;
            if (_activeTextBox != null && _activeTextBox.TryGetTarget(out var tb) && tb != null)
                modal = Window.GetWindow(tb);
            else if (_activePasswordBox != null && _activePasswordBox.TryGetTarget(out var pb) && pb != null)
                modal = Window.GetWindow(pb);

            if (modal == null || modal is PrincipalWindow || modal is LoginWindow
                || modal is VirtualKeyboardShellWindow)
                return;

            // Restaurar el anterior si cambió de modal
            if (_liftedModal != null && !ReferenceEquals(_liftedModal, modal))
                RestoreLiftedModal();

            double kbTop = _shell.Top;
            double modalBottom = modal.Top + modal.ActualHeight;
            if (modalBottom <= kbTop - 8) return; // ya queda arriba del teclado

            if (_liftedModal == null)
            {
                _liftedModal = modal;
                _liftedModalOriginalTop = modal.Top;
            }

            double overlap = modalBottom - kbTop + 12;
            double newTop = modal.Top - overlap;
            if (newTop < 4) newTop = 4;
            modal.Top = newTop;
        }

        private static void RestoreLiftedModal()
        {
            if (_liftedModal == null) return;
            try
            {
                if (!double.IsNaN(_liftedModalOriginalTop) && _liftedModal.IsLoaded)
                    _liftedModal.Top = _liftedModalOriginalTop;
            }
            catch { /* modal ya cerrado */ }
            _liftedModal = null;
            _liftedModalOriginalTop = double.NaN;
        }

        public static void InsertText(string text)
        {
            if (_activeTextBox != null && _activeTextBox.TryGetTarget(out var tb) && tb != null)
            {
                var start = tb.SelectionStart;
                var len = tb.SelectionLength;
                var current = tb.Text ?? "";

                tb.Text = current.Substring(0, start) + text + current.Substring(start + len);
                tb.CaretIndex = start + text.Length;
                tb.Focus();
            }
            else if (_activePasswordBox != null && _activePasswordBox.TryGetTarget(out var pb) && pb != null)
            {
                pb.Password = (pb.Password ?? "") + text;
                pb.Focus();
            }
        }

        public static void Backspace()
        {
            if (_activeTextBox != null && _activeTextBox.TryGetTarget(out var tb) && tb != null)
            {
                var start = tb.SelectionStart;
                var len = tb.SelectionLength;
                var text = tb.Text ?? "";

                if (len > 0)
                {
                    tb.Text = text.Substring(0, start) + text.Substring(start + len);
                    tb.CaretIndex = start;
                }
                else if (start > 0)
                {
                    tb.Text = text.Substring(0, start - 1) + text.Substring(start);
                    tb.CaretIndex = start - 1;
                }
                tb.Focus();
            }
            else if (_activePasswordBox != null && _activePasswordBox.TryGetTarget(out var pb) && pb != null)
            {
                var pwd = pb.Password ?? "";
                if (pwd.Length > 0)
                    pb.Password = pwd.Substring(0, pwd.Length - 1);
                pb.Focus();
            }
        }

        public static void Enter()
        {
            if (_activeTextBox != null && _activeTextBox.TryGetTarget(out var tb) && tb != null)
                tb.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
            else if (_activePasswordBox != null && _activePasswordBox.TryGetTarget(out var pb) && pb != null)
                pb.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
        }

        public static void Confirm() => Hide();

        public static void ToggleSign()
        {
            if (_activeTextBox != null && _activeTextBox.TryGetTarget(out var tb) && tb != null)
            {
                var text = tb.Text ?? "";
                if (text.StartsWith("-"))
                    tb.Text = text.Substring(1);
                else if (text.Length > 0)
                    tb.Text = "-" + text;

                tb.CaretIndex = tb.Text.Length;
                tb.Focus();
            }
        }
    }
}
