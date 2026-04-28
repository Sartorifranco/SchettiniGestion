using System;
using System.Globalization;
using System.IO;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace SchettiniGestion.WPF.Converters
{
    public class PathToImageConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string path = value as string;
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return null;

            try
            {
                var imagen = new BitmapImage();
                imagen.BeginInit();
                imagen.UriSource = new Uri(path, UriKind.Absolute);
                imagen.CacheOption = BitmapCacheOption.OnLoad;
                imagen.EndInit();
                return imagen;
            }
            catch
            {
                return null;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
    }
}
