using System.Globalization;

namespace SchettiniGestion.WPF
{
    /// <summary>Cultura y formato monetario para Argentina (peso $).</summary>
    public static class AppCulture
    {
        public static readonly CultureInfo Argentine = CultureInfo.CreateSpecificCulture("es-AR");

        public static void Initialize()
        {
            CultureInfo.DefaultThreadCurrentCulture = Argentine;
            CultureInfo.DefaultThreadCurrentUICulture = Argentine;
        }

        public static string FormatCurrency(decimal amount) => amount.ToString("C2", Argentine);
    }
}
