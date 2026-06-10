using System.Globalization;
using System.Windows;
using System.Windows.Markup;
using Xceed.Wpf.Toolkit;

namespace SchettiniGestion.WPF
{
    /// <summary>Cultura y formato monetario para Argentina (peso $).</summary>
    public static class AppCulture
    {
        public static CultureInfo Argentine { get; private set; } = CultureInfo.CreateSpecificCulture("es-AR");

        private static bool _initialized;

        public static void Initialize()
        {
            if (_initialized) return;
            _initialized = true;

            Argentine = CultureInfo.CreateSpecificCulture("es-AR");

            CultureInfo.DefaultThreadCurrentCulture = Argentine;
            CultureInfo.DefaultThreadCurrentUICulture = Argentine;
            CultureInfo.CurrentCulture = Argentine;
            CultureInfo.CurrentUICulture = Argentine;

            var lang = XmlLanguage.GetLanguage(Argentine.IetfLanguageTag);
            FrameworkElement.LanguageProperty.OverrideMetadata(
                typeof(FrameworkElement),
                new FrameworkPropertyMetadata(lang));

            EventManager.RegisterClassHandler(typeof(DecimalUpDown), FrameworkElement.LoadedEvent,
                new RoutedEventHandler(ApplyCultureToNumeric));
            EventManager.RegisterClassHandler(typeof(IntegerUpDown), FrameworkElement.LoadedEvent,
                new RoutedEventHandler(ApplyCultureToNumeric));
        }

        private static void ApplyCultureToNumeric(object sender, RoutedEventArgs e)
        {
            if (sender is DecimalUpDown dud)
                dud.CultureInfo = Argentine;
            else if (sender is IntegerUpDown iud)
                iud.CultureInfo = Argentine;
        }

        public static string FormatCurrency(decimal amount) => amount.ToString("C2", Argentine);
    }
}
