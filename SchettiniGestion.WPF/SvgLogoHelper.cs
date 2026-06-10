using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SharpVectors.Converters;
using SharpVectors.Renderers.Wpf;

namespace SchettiniGestion.WPF
{
    internal static class SvgLogoHelper
    {
        private static readonly Uri PackLogoUri =
            new Uri("pack://application:,,,/Resources/logo.svg", UriKind.Absolute);

        /// <summary>Marca de la app embebida (<c>Resources/logo.svg</c>).</summary>
        public static DrawingImage LoadEmbeddedLogo()
        {
            return LoadFromUri(PackLogoUri);
        }

        /// <summary>Asigna el icono de ventana / barra de tareas desde el SVG embebido.</summary>
        public static void ApplyWindowIcon(Window window)
        {
            if (window == null) return;
            try
            {
                var logo = LoadEmbeddedLogo();
                if (logo != null)
                    window.Icon = logo;
            }
            catch { /* opcional */ }
        }

        /// <summary>Asigna <see cref="Image.Source"/> al logo embebido (pantallas de login, sidebar, etc.).</summary>
        public static void ApplyToImage(Image image)
        {
            if (image == null) return;
            try
            {
                var logo = LoadEmbeddedLogo();
                if (logo != null)
                    image.Source = logo;
            }
            catch { /* opcional */ }
        }

        public static DrawingImage LoadFromUri(Uri uri)
        {
            try
            {
                var reader = new FileSvgReader(new WpfDrawingSettings(), false);
                var dg = reader.Read(uri);
                if (dg != null)
                    return new DrawingImage(dg);
            }
            catch
            {
                /* recurso opcional */
            }
            return null;
        }

        public static ImageSource LoadImageFromPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return null;

            var ext = Path.GetExtension(path)?.ToLowerInvariant();
            if (ext == ".svg")
            {
                try
                {
                    var reader = new FileSvgReader(new WpfDrawingSettings(), false);
                    var dg = reader.Read(path);
                    if (dg != null)
                        return new DrawingImage(dg);
                }
                catch
                {
                    /* ignorar logo inválido */
                }
                return null;
            }

            try
            {
                var bi = new BitmapImage();
                bi.BeginInit();
                bi.UriSource = new Uri(path, UriKind.Absolute);
                bi.CacheOption = BitmapCacheOption.OnLoad;
                bi.EndInit();
                return bi;
            }
            catch
            {
                return null;
            }
        }
    }
}
