using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SchettiniGestion.WPF
{
    /// <summary>
    /// Teclado virtual embebido (overlay) dentro de la ventana del sistema.
    /// No usa Window flotante: así no puede saltar entre monitores ni quedar cortado.
    /// </summary>
    public partial class VirtualKeyboardControl : UserControl
    {
        private VirtualKeyboardMode _mode = VirtualKeyboardMode.Alpha;
        private bool _shiftActive;
        private bool _shiftLocked;
        private bool _symbolsMode;
        private DateTime _lastShiftTap;
        private bool _built;

        private const double NumericPadWidth = 360;

        private enum KT { Normal, Special, Back, Enter, Confirm, Space }

        private struct AK
        {
            public readonly string Lo, Up, Sym;
            public readonly double W;
            public readonly KT Type;
            public AK(string lo, string up, string sym, double w = 1.0, KT t = KT.Normal)
            { Lo = lo; Up = up; Sym = sym; W = w; Type = t; }
            public static AK Fix(string s, double w = 1.0, KT t = KT.Normal)
                => new AK(s, s, s, w, t);
            public static AK Gap(double w)
                => new AK(null, null, null, w, KT.Normal);
        }

        private readonly List<(Button btn, string lo, string up, string sym)> _alphaKeys
            = new List<(Button, string, string, string)>();

        public VirtualKeyboardControl()
        {
            InitializeComponent();
            Loaded += (s, e) => EnsureBuilt();
        }

        public VirtualKeyboardMode CurrentMode => _mode;

        public void SetMode(VirtualKeyboardMode mode)
        {
            EnsureBuilt();
            _mode = mode;
            _shiftActive = _shiftLocked = _symbolsMode = false;

            pnlAlpha.Visibility = mode == VirtualKeyboardMode.Alpha
                ? Visibility.Visible : Visibility.Collapsed;
            pnlNumeric.Visibility = mode == VirtualKeyboardMode.Numeric
                ? Visibility.Visible : Visibility.Collapsed;

            lblModo.Text = mode == VirtualKeyboardMode.Numeric ? "123" : "ABC";

            if (mode == VirtualKeyboardMode.Numeric)
            {
                // Solo el ancho del pad, pegado a la derecha del host (sin negro lateral).
                Width = NumericPadWidth;
                HorizontalAlignment = HorizontalAlignment.Right;
                mainBorder.CornerRadius = new CornerRadius(16);
                mainBorder.ClearValue(WidthProperty);
                mainBorder.HorizontalAlignment = HorizontalAlignment.Stretch;
            }
            else
            {
                // 100% del ancho del host (= ventana del sistema).
                ClearValue(WidthProperty);
                HorizontalAlignment = HorizontalAlignment.Stretch;
                mainBorder.CornerRadius = new CornerRadius(20, 20, 0, 0);
                mainBorder.ClearValue(WidthProperty);
                mainBorder.HorizontalAlignment = HorizontalAlignment.Stretch;
            }

            if (mode == VirtualKeyboardMode.Alpha)
                RefreshAlpha();
        }

        private void EnsureBuilt()
        {
            if (_built) return;
            BuildAlpha();
            BuildNumpad();
            _built = true;
        }

        private void BuildAlpha()
        {
            pnlAlpha.Children.Clear();
            _alphaKeys.Clear();

            AddAlphaRow(
                AK.Fix("1"), AK.Fix("2"), AK.Fix("3"), AK.Fix("4"), AK.Fix("5"),
                AK.Fix("6"), AK.Fix("7"), AK.Fix("8"), AK.Fix("9"), AK.Fix("0"),
                AK.Fix("⌫", 1.8, KT.Back));

            AddAlphaRow(
                AK.Gap(0.6),
                new AK("q", "Q", "@"), new AK("w", "W", "#"), new AK("e", "E", "$"),
                new AK("r", "R", "%"), new AK("t", "T", "&"), new AK("y", "Y", "*"),
                new AK("u", "U", "-"), new AK("i", "I", "+"), new AK("o", "O", "("),
                new AK("p", "P", ")"),
                AK.Gap(0.6));

            AddAlphaRow(
                AK.Gap(1.0),
                new AK("a", "A", "!"), new AK("s", "S", "\""), new AK("d", "D", "'"),
                new AK("f", "F", ":"), new AK("g", "G", ";"), new AK("h", "H", "/"),
                new AK("j", "J", "_"), new AK("k", "K", "~"), new AK("l", "L", "·"),
                new AK("ñ", "Ñ", "…"),
                AK.Fix("↵", 2.0, KT.Enter));

            AddAlphaRow(
                AK.Fix("⇧", 1.8, KT.Special),
                new AK("z", "Z", "?"), new AK("x", "X", "~"), new AK("c", "C", "["),
                new AK("v", "V", "]"), new AK("b", "B", "{"), new AK("n", "N", "}"),
                new AK("m", "M", "\\"), new AK(",", ";", "<"), new AK(".", ":", ">"),
                AK.Fix("⇧", 1.8, KT.Special));

            var g = MkGrid();
            g.ColumnDefinitions.Add(MkCol(1.5));
            g.ColumnDefinitions.Add(MkCol(6.5));
            g.ColumnDefinitions.Add(MkCol(1.0));
            g.ColumnDefinitions.Add(MkCol(1.5));
            PlaceBtn(g, Btn("?#", KT.Special), 0);
            PlaceBtn(g, Btn("espacio", KT.Space), 1);
            PlaceBtn(g, Btn("@", KT.Normal), 2);
            PlaceBtn(g, Btn("✓", KT.Confirm), 3);
            pnlAlpha.Children.Add(g);
        }

        private void AddAlphaRow(params AK[] keys)
        {
            var grid = MkGrid();
            foreach (var k in keys) grid.ColumnDefinitions.Add(MkCol(k.W));

            for (int i = 0; i < keys.Length; i++)
            {
                if (keys[i].Lo == null) continue;
                var btn = Btn(keys[i].Lo, keys[i].Type);
                PlaceBtn(grid, btn, i);
                if (keys[i].Lo != keys[i].Up || keys[i].Lo != keys[i].Sym)
                    _alphaKeys.Add((btn, keys[i].Lo, keys[i].Up, keys[i].Sym));
            }
            pnlAlpha.Children.Add(grid);
        }

        private void RefreshAlpha()
        {
            string mode = _symbolsMode ? "sym" : (_shiftActive || _shiftLocked ? "up" : "lo");
            foreach (var (btn, lo, up, sym) in _alphaKeys)
            {
                string lbl = mode == "sym" ? sym : (mode == "up" ? up : lo);
                btn.Content = lbl;
                btn.Tag = lbl;
            }

            foreach (UIElement row in pnlAlpha.Children)
            {
                if (!(row is Grid g)) continue;
                foreach (UIElement el in g.Children)
                {
                    if (!(el is Button b)) continue;
                    if (!("⇧".Equals(b.Tag) || "⇧".Equals(b.Content))) continue;
                    bool active = _shiftActive || _shiftLocked;
                    b.Background = active
                        ? (Brush)FindResource("VKShiftActiveBg")
                        : (Brush)FindResource("VKKeySpecialBg");
                    b.Foreground = active ? Brushes.White : (Brush)FindResource("TextPrimary");
                }
            }
        }

        private void BuildNumpad()
        {
            pnlNumeric.Children.Clear();

            var rows = new (string, KT)[][]
            {
                new[] { ("7", KT.Normal), ("8", KT.Normal), ("9", KT.Normal), ("⌫", KT.Back) },
                new[] { ("4", KT.Normal), ("5", KT.Normal), ("6", KT.Normal), (",", KT.Special) },
                new[] { ("1", KT.Normal), ("2", KT.Normal), ("3", KT.Normal), (".", KT.Special) },
                new[] { ("±", KT.Special), ("0", KT.Normal), ("00", KT.Normal), ("✓", KT.Confirm) },
            };

            foreach (var row in rows)
            {
                var g = MkGrid();
                for (int c = 0; c < row.Length; c++) g.ColumnDefinitions.Add(MkCol(1.0));
                for (int c = 0; c < row.Length; c++)
                {
                    var btn = Btn(row[c].Item1, row[c].Item2);
                    btn.Height = 68;
                    PlaceBtn(g, btn, c);
                }
                pnlNumeric.Children.Add(g);
            }
        }

        private static Grid MkGrid()
            => new Grid { HorizontalAlignment = HorizontalAlignment.Stretch };

        private static ColumnDefinition MkCol(double w)
            => new ColumnDefinition { Width = new GridLength(w, GridUnitType.Star) };

        private static void PlaceBtn(Grid g, Button btn, int col)
        {
            Grid.SetColumn(btn, col);
            g.Children.Add(btn);
        }

        private Button Btn(string label, KT type)
        {
            string sty = type == KT.Back ? "VKKeyBack"
                       : type == KT.Confirm ? "VKKeyConfirm"
                       : type == KT.Enter ? "VKKeyEnter"
                       : type == KT.Space ? "VKKeySpace"
                       : type == KT.Special ? "VKKeySpecial"
                       : "VKKey";
            var btn = new Button
            {
                Content = label,
                Tag = label,
                Style = (Style)Resources[sty],
                Focusable = false,
            };
            btn.Click += OnKeyClick;
            return btn;
        }

        private void OnKeyClick(object sender, RoutedEventArgs e)
        {
            if (!(sender is Button btn)) return;
            var key = (btn.Tag as string) ?? (btn.Content as string) ?? "";

            switch (key)
            {
                case "⌫":
                    KeyboardService.Backspace(); return;
                case "↵":
                    KeyboardService.Enter(); return;
                case "✓":
                    KeyboardService.Confirm(); return;
                case "⇧":
                    HandleShift(); return;
                case "?#":
                    EnableSymbols(); return;
                case "ABC":
                    DisableSymbols(); return;
                case "espacio":
                    KeyboardService.InsertText(" "); return;
                case "±":
                    KeyboardService.ToggleSign(); return;
                default:
                    KeyboardService.InsertText(key);
                    if (_shiftActive && !_shiftLocked && !_symbolsMode)
                    { _shiftActive = false; RefreshAlpha(); }
                    return;
            }
        }

        private void HandleShift()
        {
            var now = DateTime.UtcNow;
            if ((now - _lastShiftTap).TotalMilliseconds < 450)
            {
                _shiftLocked = !_shiftLocked;
                _shiftActive = _shiftLocked;
            }
            else
            {
                if (_shiftLocked) { _shiftLocked = _shiftActive = false; }
                else { _shiftActive = !_shiftActive; }
            }
            _lastShiftTap = now;
            RefreshAlpha();
        }

        private void EnableSymbols()
        {
            _symbolsMode = true; _shiftActive = _shiftLocked = false;
            RefreshAlpha();
            lblModo.Text = "?#";
            SwapBottomLabel("?#", "ABC");
        }

        private void DisableSymbols()
        {
            _symbolsMode = false; _shiftActive = _shiftLocked = false;
            RefreshAlpha();
            lblModo.Text = "ABC";
            SwapBottomLabel("ABC", "?#");
        }

        private void SwapBottomLabel(string fromTag, string toLabel)
        {
            if (pnlAlpha.Children.Count == 0) return;
            if (!(pnlAlpha.Children[pnlAlpha.Children.Count - 1] is Grid last)) return;
            foreach (UIElement el in last.Children)
            {
                if (!(el is Button b) || !fromTag.Equals(b.Tag)) continue;
                b.Content = toLabel; b.Tag = toLabel;
            }
        }

        private void btnCerrar_Click(object sender, RoutedEventArgs e)
            => KeyboardService.Confirm();
    }
}
