using System.Windows.Controls;

namespace SchettiniGestion.WPF
{
    /// <summary>Navegación por teclado en listas de sugerencias dentro de Popup (sin mover el foco del TextBox).</summary>
    internal static class AutocompleteListHelper
    {
        public static void ReiniciarSeleccion(ListBox list)
        {
            if (list == null) return;
            list.SelectedIndex = list.Items.Count > 0 ? 0 : -1;
        }

        public static void MoverSeleccion(ListBox list, int delta)
        {
            if (list == null || list.Items.Count == 0) return;

            int idx = list.SelectedIndex;
            if (idx < 0) idx = 0;
            else idx = System.Math.Max(0, System.Math.Min(list.Items.Count - 1, idx + delta));

            list.SelectedIndex = idx;
            if (list.SelectedItem != null)
                list.ScrollIntoView(list.SelectedItem);
        }
    }
}
