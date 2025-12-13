using System.Windows;
using System.Windows.Media;

namespace SchettiniGestion.WPF
{
    /// <summary>
    /// Una clase de ayuda para navegar el árbol visual de WPF.
    /// </summary>
    public static class VisualTreeHelpers
    {
        /// <summary>
        /// Busca un control hijo del tipo especificado.
        /// </summary>
        public static T FindChild<T>(DependencyObject parent) where T : DependencyObject
        {
            if (parent == null) return null;

            T foundChild = null;
            int childrenCount = VisualTreeHelper.GetChildrenCount(parent);

            for (int i = 0; i < childrenCount; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                T childType = child as T;

                if (childType != null)
                {
                    foundChild = childType;
                    break;
                }

                // Si no es el tipo, buscamos dentro de *ese* hijo
                foundChild = FindChild<T>(child);

                if (foundChild != null) break;
            }
            return foundChild;
        }
    }
}