using System;
using System.Windows;
using SchettiniGestion.WPF.Properties;

namespace SchettiniGestion.WPF
{
    public static class ThemeManager
    {
        private const string DarkThemeUri = "Themes/DarkTheme.xaml";
        private const string LightThemeUri = "Themes/LightTheme.xaml";

        public static bool IsDark { get; private set; } = true;

        /// <summary>Se dispara cada vez que se aplica un diccionario de tema (toda la app).</summary>
        public static event EventHandler ThemeChanged;

        public static void LoadSavedTheme()
        {
            try
            {
                string saved = Settings.Default.Theme;
                IsDark = !string.Equals(saved, "Light", StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                IsDark = true;
            }
            ApplyTheme();
        }

        public static void ToggleTheme()
        {
            SetTheme(!IsDark);
        }

        public static void SetTheme(bool dark)
        {
            IsDark = dark;
            try
            {
                Settings.Default.Theme = dark ? "Dark" : "Light";
                Settings.Default.Save();
            }
            catch { /* sin userSettings en config antiguo */ }
            ApplyTheme();
        }

        private static void ApplyTheme()
        {
            var dict = new ResourceDictionary
            {
                Source = new Uri(IsDark ? DarkThemeUri : LightThemeUri, UriKind.Relative)
            };

            var appDicts = Application.Current.Resources.MergedDictionaries;
            for (int i = appDicts.Count - 1; i >= 0; i--)
            {
                string src = appDicts[i].Source?.OriginalString ?? string.Empty;
                if (src.EndsWith("DarkTheme.xaml", StringComparison.OrdinalIgnoreCase) ||
                    src.EndsWith("LightTheme.xaml", StringComparison.OrdinalIgnoreCase))
                {
                    appDicts.RemoveAt(i);
                }
            }
            appDicts.Add(dict);
            ThemeChanged?.Invoke(null, EventArgs.Empty);
        }
    }
}
