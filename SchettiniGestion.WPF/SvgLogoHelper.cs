using System;
using System.IO;
using System.Linq;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SharpVectors.Converters;
using SharpVectors.Renderers.Wpf;

namespace SchettiniGestion.WPF
{
    internal static class SvgLogoHelper
    {
        private static readonly Uri PackLogoIcoUri =
            new Uri("pack://application:,,,/Resources/schpos-logo.ico", UriKind.Absolute);

        private static readonly Uri PackLogoUri =
            new Uri("pack://application:,,,/Resources/logo.svg", UriKind.Absolute);

        private static ImageSource _cachedBrand;

        public static ImageSource LoadEmbeddedLogo()
        {
            if (_cachedBrand != null)
                return _cachedBrand;

            try
            {
                var decoder = BitmapDecoder.Create(
                    PackLogoIcoUri,
                    BitmapCreateOptions.PreservePixelFormat,
                    BitmapCacheOption.OnLoad);
                var frame = decoder.Frames
                    .OrderByDescending(f => f.PixelWidth * f.PixelHeight)
                    .FirstOrDefault();
                if (frame != null)
                {
                    frame.Freeze();
                    _cachedBrand = frame;
                    return _cachedBrand;
                }
            }
            catch { /* ico opcional */ }

            var svg = LoadFromUri(PackLogoUri);
            if (svg != null)
                _cachedBrand = svg;
            return _cachedBrand;
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
