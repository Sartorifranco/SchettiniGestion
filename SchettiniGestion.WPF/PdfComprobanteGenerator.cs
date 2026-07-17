using PdfSharp.Drawing;
using PdfSharp.Pdf;
using System;
using System.Data;
using System.IO;

namespace SchettiniGestion.WPF
{
    internal static class PdfComprobanteGenerator
    {
        private const double Margen = 36;
        private const double AnchoPagina = 595.28;
        private const double AltoPagina = 841.89;

        public static void GenerarComprobanteVenta(
            string rutaPdf,
            DataRow cabecera,
            DataTable items,
            string tituloDocumento,
            string letra,
            int numeroComprobante,
            decimal total,
            string condicionPago,
            string pieFiscal,
            string pieLegal,
            bool mostrarCodigo = true)
        {
            DataRow conf = DatabaseService.GetConfiguracion();
            string razonSocial = conf?["RazonSocial"]?.ToString()?.Trim() ?? "";
            string nombreFantasia = conf?["NombreFantasia"]?.ToString()?.Trim() ?? "";
            string cuit = conf?["CUIT"]?.ToString()?.Trim() ?? "";
            string direccion = conf?["Direccion"]?.ToString()?.Trim() ?? "";
            string telefono = conf?["Telefono"]?.ToString()?.Trim() ?? "";
            string email = conf?["Email"]?.ToString()?.Trim() ?? "";
            string puntoVenta = conf?["PuntoVenta"]?.ToString()?.Trim() ?? "";
            string condicionIva = conf != null && conf.Table.Columns.Contains("CondicionIVAEmpresa")
                ? conf["CondicionIVAEmpresa"]?.ToString()?.Trim() ?? "" : "";
            string logoPath = conf != null && conf.Table.Columns.Contains("LogoPath")
                ? conf["LogoPath"]?.ToString()?.Trim() ?? "" : "";
            string nombrePersonal = cabecera.Table.Columns.Contains("NombrePersonal")
                ? cabecera["NombrePersonal"]?.ToString()?.Trim() ?? "" : "";

            var opciones = DatabaseService.GetOpcionesImpresionTicket();

            if (string.IsNullOrWhiteSpace(razonSocial))
                razonSocial = nombreFantasia;
            if (string.IsNullOrWhiteSpace(razonSocial))
                razonSocial = "Mi Negocio";

            string cliente = cabecera["ClienteNombre"]?.ToString() ?? "Consumidor Final";
            string clienteCuit = cabecera.Table.Columns.Contains("ClienteCUIT") ? cabecera["ClienteCUIT"]?.ToString() ?? "-" : "-";
            string clienteIva = cabecera.Table.Columns.Contains("ClienteIVA") ? cabecera["ClienteIVA"]?.ToString() ?? "-" : "-";
            string clienteDir = cabecera.Table.Columns.Contains("ClienteDireccion") ? cabecera["ClienteDireccion"]?.ToString() ?? "-" : "-";
            DateTime fecha = Convert.ToDateTime(cabecera["Fecha"]);

            var doc = new PdfDocument();
            doc.Info.Title = $"{tituloDocumento} {numeroComprobante:D8}";
            doc.Info.Author = razonSocial;

            var page = doc.AddPage();
            page.Width = AnchoPagina;
            page.Height = AltoPagina;

            var gfx = XGraphics.FromPdfPage(page);
            gfx.SmoothingMode = XSmoothingMode.HighQuality;

            var fEmpresaTitulo = new XFont("Arial", 14, XFontStyle.Bold);
            var fEmpresaSub = new XFont("Arial", 10, XFontStyle.Regular);
            var fEmpresaDet = new XFont("Arial", 9, XFontStyle.Regular);
            var fDocDet = new XFont("Arial", 10, XFontStyle.Regular);
            var fSeccion = new XFont("Arial", 10, XFontStyle.Bold);
            var fTablaHead = new XFont("Arial", 8.5, XFontStyle.Bold);
            var fTabla = new XFont("Arial", 8.5, XFontStyle.Regular);
            var fTotal = new XFont("Arial", 14, XFontStyle.Bold);
            var fPie = new XFont("Arial", 8.5, XFontStyle.Italic);

            double y = Margen;
            double anchoContenido = AnchoPagina - Margen * 2;
            double cajaDocAncho = 190;
            double cajaDocX = AnchoPagina - Margen - cajaDocAncho;
            const double padCaja = 14;
            double innerCajaW = cajaDocAncho - padCaja * 2;

            // Logo + datos empresa
            double infoX = Margen;
            if (!string.IsNullOrWhiteSpace(logoPath) && File.Exists(logoPath))
            {
                try
                {
                    using (var img = XImage.FromFile(logoPath))
                    {
                        double logoH = 58;
                        double logoW = logoH * (img.PixelWidth / (double)Math.Max(1, img.PixelHeight));
                        if (logoW > 90) { logoW = 90; logoH = logoW * (img.PixelHeight / (double)Math.Max(1, img.PixelWidth)); }
                        gfx.DrawImage(img, Margen, y, logoW, logoH);
                        infoX = Margen + logoW + 12;
                    }
                }
                catch { infoX = Margen; }
            }

            gfx.DrawString(razonSocial, fEmpresaTitulo, XBrushes.Black, new XRect(infoX, y, cajaDocX - infoX - 8, 18), XStringFormats.TopLeft);
            y += 24;

            if (!string.IsNullOrWhiteSpace(nombreFantasia)
                && !nombreFantasia.Equals(razonSocial, StringComparison.OrdinalIgnoreCase))
            {
                gfx.DrawString(nombreFantasia, fEmpresaSub, XBrushes.DimGray, new XRect(infoX, y, cajaDocX - infoX - 8, 14), XStringFormats.TopLeft);
                y += 14;
            }

            if (!string.IsNullOrWhiteSpace(cuit))
            {
                gfx.DrawString($"CUIT: {cuit}", fEmpresaDet, XBrushes.Black, infoX, y);
                y += 12;
            }
            if (!string.IsNullOrWhiteSpace(condicionIva))
            {
                gfx.DrawString($"IVA: {condicionIva}", fEmpresaDet, XBrushes.Black, infoX, y);
                y += 12;
            }
            if (!string.IsNullOrWhiteSpace(direccion))
            {
                gfx.DrawString($"Domicilio: {direccion}", fEmpresaDet, XBrushes.Black, new XRect(infoX, y, cajaDocX - infoX - 8, 12), XStringFormats.TopLeft);
                y += 12;
            }
            if (!string.IsNullOrWhiteSpace(telefono) || !string.IsNullOrWhiteSpace(email))
            {
                string contacto = "";
                if (!string.IsNullOrWhiteSpace(telefono)) contacto = $"Tel: {telefono}";
                if (!string.IsNullOrWhiteSpace(email))
                    contacto += (contacto.Length > 0 ? "  |  " : "") + email;
                gfx.DrawString(contacto, fEmpresaDet, XBrushes.Black, new XRect(infoX, y, cajaDocX - infoX - 8, 12), XStringFormats.TopLeft);
                y += 12;
            }

            double yEmpresaFin = y;

            // Caja documento (recuadro sobrio, texto contenido dentro del marco)
            double cajaY = Margen;
            double ty = cajaY + padCaja;
            var rectCaja = new XRect(cajaDocX + padCaja, ty, innerCajaW, 16);
            gfx.DrawString(tituloDocumento.ToUpper(), new XFont("Arial", 12, XFontStyle.Bold), XBrushes.Black, rectCaja, XStringFormats.TopLeft);
            ty += 17;
            gfx.DrawString($"Letra: {letra}", fDocDet, XBrushes.Black,
                new XRect(cajaDocX + padCaja, ty, innerCajaW, 13), XStringFormats.TopLeft);
            ty += 14;
            gfx.DrawString($"N° {numeroComprobante:D8}", fDocDet, XBrushes.Black,
                new XRect(cajaDocX + padCaja, ty, innerCajaW, 13), XStringFormats.TopLeft);
            ty += 14;
            gfx.DrawString($"Fecha: {fecha:dd/MM/yyyy HH:mm}", fDocDet, XBrushes.Black,
                new XRect(cajaDocX + padCaja, ty, innerCajaW, 13), XStringFormats.TopLeft);
            ty += 14;
            if (opciones.MostrarPuntoVenta && !string.IsNullOrWhiteSpace(puntoVenta))
            {
                gfx.DrawString($"P. Venta: {puntoVenta.PadLeft(4, '0')}", fDocDet, XBrushes.Black,
                    new XRect(cajaDocX + padCaja, ty, innerCajaW, 13), XStringFormats.TopLeft);
                ty += 14;
            }
            if (opciones.MostrarVendedor && !string.IsNullOrWhiteSpace(nombrePersonal))
            {
                gfx.DrawString($"Atendido por: {nombrePersonal}", fDocDet, XBrushes.Black,
                    new XRect(cajaDocX + padCaja, ty, innerCajaW, 13), XStringFormats.TopLeft);
                ty += 14;
            }
            double cajaDocAlto = (ty - cajaY) + padCaja;
            gfx.DrawRectangle(XPens.Black, cajaDocX, cajaY, cajaDocAncho, cajaDocAlto);

            y = Math.Max(yEmpresaFin, cajaY + cajaDocAlto + 8);
            DibujarLineaHorizontal(gfx, y, Margen, AnchoPagina - Margen);
            y += 14;

            // Cliente
            gfx.DrawString("DATOS DEL CLIENTE", fSeccion, XBrushes.Black, Margen, y);
            y += 16;
            gfx.DrawString(cliente.ToUpper(), fEmpresaSub, XBrushes.Black, Margen, y);
            y += 14;
            string lineaCli = $"CUIT: {clienteCuit}    |    IVA: {clienteIva}";
            if (!string.IsNullOrWhiteSpace(clienteDir) && clienteDir != "-")
                lineaCli += $"    |    Domicilio: {clienteDir}";
            gfx.DrawString(lineaCli, fEmpresaDet, XBrushes.Black, new XRect(Margen, y, anchoContenido, 24), XStringFormats.TopLeft);
            y += 22;
            DibujarLineaHorizontal(gfx, y, Margen, AnchoPagina - Margen);
            y += 12;

            // Tabla de ítems
            const double altoFila = 20;
            double colCant = 40;
            double colCod = mostrarCodigo ? 65 : 0;
            double colUnit = 78;
            double colTotal = 82;
            double colDesc = anchoContenido - colCant - colCod - colUnit - colTotal;

            double yTablaInicio = y;
            var gris = new XSolidBrush(XColor.FromArgb(240, 240, 240));
            gfx.DrawRectangle(gris, Margen, y, anchoContenido, altoFila);
            gfx.DrawRectangle(XPens.Gray, Margen, y, anchoContenido, altoFila);

            double xCol = Margen + 8;
            double yHead = y + 5;
            double hHead = altoFila - 8;
            gfx.DrawString("CANT", fTablaHead, XBrushes.Black, new XRect(xCol, yHead, colCant - 4, hHead), XStringFormats.TopLeft);
            xCol += colCant;
            if (mostrarCodigo)
            {
                gfx.DrawString("CÓDIGO", fTablaHead, XBrushes.Black, new XRect(xCol, yHead, colCod - 4, hHead), XStringFormats.TopLeft);
                xCol += colCod;
            }
            gfx.DrawString("DESCRIPCIÓN", fTablaHead, XBrushes.Black, new XRect(xCol, yHead, colDesc - 8, hHead), XStringFormats.TopLeft);
            var rectUnitHead = new XRect(Margen + anchoContenido - colUnit - colTotal + 4, yHead, colUnit - 10, hHead);
            var rectTotalHead = new XRect(Margen + anchoContenido - colTotal + 4, yHead, colTotal - 10, hHead);
            gfx.DrawString("P. UNIT.", fTablaHead, XBrushes.Black, rectUnitHead, XStringFormats.TopRight);
            gfx.DrawString("TOTAL", fTablaHead, XBrushes.Black, rectTotalHead, XStringFormats.TopRight);
            y += altoFila;

            foreach (DataRow item in items.Rows)
            {
                if (y > AltoPagina - 120)
                {
                    gfx.Dispose();
                    page = doc.AddPage();
                    page.Width = AnchoPagina;
                    page.Height = AltoPagina;
                    gfx = XGraphics.FromPdfPage(page);
                    y = Margen;
                }

                gfx.DrawLine(XPens.LightGray, Margen, y, Margen + anchoContenido, y);
                double yTexto = y + 5;
                double hTexto = altoFila - 8;
                xCol = Margen + 8;
                gfx.DrawString(item["Cantidad"]?.ToString() ?? "0", fTabla, XBrushes.Black, new XRect(xCol, yTexto, colCant - 4, hTexto), XStringFormats.TopLeft);
                xCol += colCant;
                if (mostrarCodigo)
                {
                    string cod = item.Table.Columns.Contains("Codigo") ? item["Codigo"]?.ToString() ?? "" : "";
                    gfx.DrawString(cod, fTabla, XBrushes.Black, new XRect(xCol, yTexto, colCod - 4, hTexto), XStringFormats.TopLeft);
                    xCol += colCod;
                }
                string desc = item["Descripcion"]?.ToString() ?? "";
                gfx.DrawString(desc, fTabla, XBrushes.Black, new XRect(xCol, yTexto, colDesc - 12, hTexto), XStringFormats.TopLeft);
                decimal unit = Convert.ToDecimal(item["PrecioUnitario"]);
                decimal sub = Convert.ToDecimal(item["Subtotal"]);
                var rectUnit = new XRect(Margen + anchoContenido - colUnit - colTotal + 4, yTexto, colUnit - 10, hTexto);
                var rectSub = new XRect(Margen + anchoContenido - colTotal + 4, yTexto, colTotal - 10, hTexto);
                gfx.DrawString(unit.ToString("C2"), fTabla, XBrushes.Black, rectUnit, XStringFormats.TopRight);
                gfx.DrawString(sub.ToString("C2"), fTabla, XBrushes.Black, rectSub, XStringFormats.TopRight);
                y += altoFila;
            }

            gfx.DrawRectangle(XPens.Gray, Margen, yTablaInicio, anchoContenido, y - yTablaInicio);
            y += 18;

            // Total
            var rectTotalLbl = new XRect(Margen, y, anchoContenido - 140, 22);
            var rectTotalVal = new XRect(Margen + anchoContenido - 135, y, 130, 24);
            gfx.DrawString("TOTAL A PAGAR:", fSeccion, XBrushes.Black, rectTotalLbl, XStringFormats.TopRight);
            gfx.DrawString(total.ToString("C2"), fTotal, XBrushes.Black, rectTotalVal, XStringFormats.TopRight);
            y += 34;

            if (!string.IsNullOrWhiteSpace(condicionPago))
            {
                gfx.DrawString($"Forma de pago: {condicionPago}", fEmpresaDet, XBrushes.Black,
                    new XRect(Margen, y, anchoContenido, 24), XStringFormats.TopLeft);
                y += 22;
            }

            if (!string.IsNullOrWhiteSpace(pieFiscal))
            {
                foreach (string linea in pieFiscal.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    gfx.DrawString(linea.Trim(), fEmpresaDet, XBrushes.Black, Margen, y);
                    y += 16;
                }
            }

            y += 12;
            DibujarLineaHorizontal(gfx, y, Margen, AnchoPagina - Margen);
            y += 18;

            if (!string.IsNullOrWhiteSpace(pieLegal))
            {
                gfx.DrawString(pieLegal, fPie, XBrushes.Gray,
                    new XRect(Margen, y, anchoContenido, 30), XStringFormats.TopCenter);
            }

            gfx.Dispose();
            doc.Save(rutaPdf);
        }

        private static void DibujarLineaHorizontal(XGraphics gfx, double y, double x1, double x2)
        {
            gfx.DrawLine(XPens.Black, x1, y, x2, y);
        }

        private static string Truncar(string texto, int max)
        {
            if (string.IsNullOrEmpty(texto) || texto.Length <= max) return texto ?? "";
            return texto.Substring(0, max - 1) + "…";
        }
    }
}
