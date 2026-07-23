using PdfSharp.Drawing;
using PdfSharp.Pdf;
using System;
using System.Data;
using System.IO;

namespace SchettiniGestion.WPF
{
    internal static class PdfInformeGenerator
    {
        private const double Margen = 40;
        private const double AnchoPagina = 595.28;
        private const double AltoPagina = 841.89;
        private const double AltoFila = 16;

        public static void GenerarInformeTabular(string rutaPdf, string titulo, DataTable datos, DateTime? desde, DateTime? hasta)
        {
            if (datos == null || datos.Columns.Count == 0)
                throw new InvalidOperationException("No hay datos para exportar.");

            var doc = new PdfDocument();
            doc.Info.Title = titulo;
            doc.Info.Author = "SCHPOS";

            var page = doc.AddPage();
            page.Width = AnchoPagina;
            page.Height = AltoPagina;
            var gfx = XGraphics.FromPdfPage(page);

            var fTitulo = new XFont("Arial", 14, XFontStyle.Bold);
            var fSub = new XFont("Arial", 9, XFontStyle.Regular);
            var fHead = new XFont("Arial", 8, XFontStyle.Bold);
            var fCell = new XFont("Arial", 8, XFontStyle.Regular);

            double y = Margen;
            double ancho = AnchoPagina - Margen * 2;
            int colCount = datos.Columns.Count;
            double colWidth = ancho / colCount;

            gfx.DrawString(titulo, fTitulo, XBrushes.Black, new XRect(Margen, y, ancho, 20), XStringFormats.TopLeft);
            y += 22;

            if (desde.HasValue && hasta.HasValue)
            {
                gfx.DrawString($"Período: {desde:dd/MM/yyyy} — {hasta:dd/MM/yyyy}", fSub, XBrushes.DimGray,
                    new XRect(Margen, y, ancho, 14), XStringFormats.TopLeft);
                y += 16;
            }

            gfx.DrawString($"Generado: {DateTime.Now:dd/MM/yyyy HH:mm}", fSub, XBrushes.DimGray,
                new XRect(Margen, y, ancho, 14), XStringFormats.TopLeft);
            y += 20;

            double x = Margen;
            for (int c = 0; c < colCount; c++)
            {
                string hdr = datos.Columns[c].ColumnName;
                gfx.DrawString(Truncar(hdr, 18), fHead, XBrushes.Black, new XRect(x + 2, y, colWidth - 4, AltoFila), XStringFormats.TopLeft);
                x += colWidth;
            }
            y += AltoFila;
            gfx.DrawLine(XPens.Gray, Margen, y, Margen + ancho, y);
            y += 4;

            foreach (DataRow row in datos.Rows)
            {
                if (y > AltoPagina - Margen - AltoFila)
                {
                    page = doc.AddPage();
                    page.Width = AnchoPagina;
                    page.Height = AltoPagina;
                    gfx = XGraphics.FromPdfPage(page);
                    y = Margen;
                }

                x = Margen;
                for (int c = 0; c < colCount; c++)
                {
                    string val = FormatearCelda(row[c]);
                    gfx.DrawString(Truncar(val, 22), fCell, XBrushes.Black, new XRect(x + 2, y, colWidth - 4, AltoFila), XStringFormats.TopLeft);
                    x += colWidth;
                }
                y += AltoFila;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(rutaPdf) ?? ".");
            doc.Save(rutaPdf);
        }

        private static string FormatearCelda(object value)
        {
            if (value == null || value == DBNull.Value) return "";
            if (value is DateTime dt) return dt.ToString("dd/MM/yyyy HH:mm");
            if (value is decimal dec) return dec.ToString("N2");
            if (value is double dbl) return dbl.ToString("N2");
            return value.ToString();
        }

        private static string Truncar(string s, int max)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Length <= max ? s : s.Substring(0, max - 1) + "…";
        }
    }
}
