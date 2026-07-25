using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using SchettiniGestion.WPF.Properties;

namespace SchettiniGestion.WPF
{
    /// <summary>
    /// Servicio global de teclado virtual.
    /// Se inicializa una sola vez en App.xaml.cs y luego trabaja de forma transparente:
    /// cada TextBox/PasswordBox que recibe foco abre automáticamente el teclado correcto.
    ///
    /// Detección de modo:
    ///   1. Tag="numeric"  → numérico
    ///   2. Nombre del control contiene palabras clave numéricas → numérico
    ///   3. InputScope = Number / CurrencyAmount / Digits / TelephoneNumber → numérico
    ///   4. Todo lo demás → alfanumérico
    /// </summary>
    public static class KeyboardService
    {
        private static VirtualKeyboardWindow _window;
        private static WeakReference<TextBox>     _activeTextBox;
        private static WeakReference<PasswordBox> _activePasswordBox;

        /// <summary>Indica si el teclado virtual está habilitado.</summary>
        public static bool IsEnabled { get; private set; }

        /// <summary>Posición Y superior del teclado cuando está visible (0 si está oculto).</summary>
        public static double KeyboardTop { get; private set; } = double.MaxValue;

        /// <summary>
        /// Se dispara cuando el teclado se muestra u oculta.
        /// Parámetro: true = visible, false = oculto.
        /// </summary>
        public static event Action<bool> VisibilityChanged;

        /// <summary>Se dispara cuando cambia el estado habilitado/deshabilitado.</summary>
        public static event Action EnabledChanged;

        /// <summary>Carga la preferencia persistida (OFF por defecto para no tapar el Login).</summary>
        public static void LoadSavedPreference()
        {
            try { IsEnabled = Settings.Default.KeyboardEnabled; }
            catch { IsEnabled = false; }
            if (!IsEnabled) Hide();
            EnabledChanged?.Invoke();
        }

        /// <summary>Activa o desactiva el teclado y guarda la preferencia entre sesiones.</summary>
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

        /// <summary>Alterna el estado habilitado/deshabilitado del teclado virtual.</summary>
        public static void Toggle()
        {
            SetEnabled(!IsEnabled);
        }

        // ── Palabras clave que indican campo numérico ─────────────
        private static readonly string[] NumericNameKeywords =
        {
            "precio", "importe", "monto", "costo", "cantidad", "descuento",
            "stock", "saldo", "interes", "tasa", "telefono", "fax", "postal",
            "numero", "puntoventa", "puntos", "cuotas", "dias"
        };

        // ─────────────────────────────────────────────────────────
        // Inicialización (llamar una sola vez desde App.OnStartup)
        // ─────────────────────────────────────────────────────────

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

        // ─────────────────────────────────────────────────────────
        // Handlers globales de foco
        // ─────────────────────────────────────────────────────────

        private static void OnTextBoxFocused(object sender, KeyboardFocusChangedEventArgs e)
        {
            if (!IsEnabled) return;
            if (!(sender is TextBox tb)) return;

            // No mostrar teclado si el TextBox es solo lectura
            if (tb.IsReadOnly) return;

            _activeTextBox     = new WeakReference<TextBox>(tb);
            _activePasswordBox = null;

            Show(DetectMode(tb));
        }

        private static void OnPasswordBoxFocused(object sender, KeyboardFocusChangedEventArgs e)
        {
            if (!IsEnabled) return;
            if (!(sender is PasswordBox pb)) return;

            _activePasswordBox = new WeakReference<PasswordBox>(pb);
            _activeTextBox     = null;

            Show(VirtualKeyboardMode.Alpha);
        }

        private static void OnAnyLostFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            var newFocus = e.NewFocus;

            // Si el foco se mueve a otro TextBox/PasswordBox, deja que
            // OnTextBoxFocused/OnPasswordBoxFocused se encarguen
            if (newFocus is TextBox || newFocus is PasswordBox)
                return;

            // Programar verificación diferida para evitar parpadeos al moverse entre campos
            Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
            {
                var focused = Keyboard.FocusedElement;
                if (focused is TextBox || focused is PasswordBox)
                    return; // ya hay un campo activo → no ocultar

                Hide();
            }), DispatcherPriority.Background);
        }

        // ─────────────────────────────────────────────────────────
        // Detección inteligente de modo
        // ─────────────────────────────────────────────────────────

        private static VirtualKeyboardMode DetectMode(TextBox tb)
        {
            // 1. Propiedad Tag explícita
            var tag = (tb.Tag as string)?.ToLowerInvariant() ?? "";
            if (tag == "numeric" || tag == "number" || tag == "decimal" ||
                tag == "precio"  || tag == "cantidad" || tag == "monto")
                return VirtualKeyboardMode.Numeric;

            // 2. Detectar si el TextBox está dentro de un spinner numérico (xctk DecimalUpDown, etc.)
            if (IsInsideNumericSpinner(tb))
                return VirtualKeyboardMode.Numeric;

            // 3. Nombre del control
            var name = (tb.Name ?? "").ToLowerInvariant();
            foreach (var kw in NumericNameKeywords)
            {
                if (name.Contains(kw))
                    return VirtualKeyboardMode.Numeric;
            }

            // 4. InputScope
            if (tb.InputScope?.Names?.Count > 0)
            {
                var scopeObj = tb.InputScope.Names[0];
                if (scopeObj is InputScopeName scopeName)
                {
                    var sv = scopeName.NameValue;
                    if (sv == InputScopeNameValue.Number          ||
                        sv == InputScopeNameValue.TelephoneNumber ||
                        sv == InputScopeNameValue.CurrencyAmount  ||
                        sv == InputScopeNameValue.Digits)
                        return VirtualKeyboardMode.Numeric;
                }
            }

            return VirtualKeyboardMode.Alpha;
        }

        /// <summary>
        /// Recorre el árbol visual hacia arriba buscando controles numéricos tipo spinner
        /// (xctk:DecimalUpDown, IntegerUpDown, DoubleUpDown, etc.).
        /// Si el TextBox vive dentro de uno de esos controles, el modo es Numérico.
        /// </summary>
        private static bool IsInsideNumericSpinner(DependencyObject element)
        {
            var parent = System.Windows.Media.VisualTreeHelper.GetParent(element);
            int depth = 0;
            while (parent != null && depth < 12)
            {
                var typeName = parent.GetType().Name;
                // Cualquier control cuyo nombre termine en "UpDown" es numérico
                if (typeName.EndsWith("UpDown", StringComparison.Ordinal))
                    return true;
                // Parar al llegar a una ventana o control de usuario raíz
                if (parent is Window || (parent is UserControl && depth > 3))
                    break;
                parent = System.Windows.Media.VisualTreeHelper.GetParent(parent);
                depth++;
            }
            return false;
        }

        // ─────────────────────────────────────────────────────────
        // Mostrar / ocultar
        // ─────────────────────────────────────────────────────────

        public static void Show(VirtualKeyboardMode mode)
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                EnsureWindow();
                _window.SetMode(mode);

                if (!_window.IsVisible)
                {
                    _window.Show();
                    // Disparar evento después de que la animación (260ms) termine
                    var timer = new System.Windows.Threading.DispatcherTimer
                        { Interval = TimeSpan.FromMilliseconds(300) };
                    timer.Tick += (s, e) =>
                    {
                        timer.Stop();
                        if (_window != null && _window.IsVisible)
                        {
                            KeyboardTop = _window.Top;
                            VisibilityChanged?.Invoke(true);
                        }
                    };
                    timer.Start();
                }
            });
        }

        public static void Hide()
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                _window?.Hide();
                KeyboardTop = double.MaxValue;
                VisibilityChanged?.Invoke(false);
            });
        }

        private static void EnsureWindow()
        {
            if (_window == null || !_window.IsLoaded)
                _window = new VirtualKeyboardWindow();
        }

        // ─────────────────────────────────────────────────────────
        // Operaciones de texto
        // ─────────────────────────────────────────────────────────

        public static void InsertText(string text)
        {
            if (_activeTextBox != null && _activeTextBox.TryGetTarget(out var tb) && tb != null)
            {
                var start  = tb.SelectionStart;
                var len    = tb.SelectionLength;
                var current = tb.Text ?? "";

                tb.Text       = current.Substring(0, start) + text + current.Substring(start + len);
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
                var len   = tb.SelectionLength;
                var text  = tb.Text ?? "";

                if (len > 0)
                {
                    tb.Text       = text.Substring(0, start) + text.Substring(start + len);
                    tb.CaretIndex = start;
                }
                else if (start > 0)
                {
                    tb.Text       = text.Substring(0, start - 1) + text.Substring(start);
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
            // Mueve el foco al siguiente control (como Tab)
            if (_activeTextBox != null && _activeTextBox.TryGetTarget(out var tb) && tb != null)
                tb.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
            else if (_activePasswordBox != null && _activePasswordBox.TryGetTarget(out var pb) && pb != null)
                pb.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
        }

        public static void Confirm()
        {
            Hide();
        }

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
