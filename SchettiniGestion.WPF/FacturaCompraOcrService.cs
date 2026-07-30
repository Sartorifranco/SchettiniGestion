using System;

namespace SchettiniGestion.WPF
{
    /// <summary>Punto de entrada único para importar una factura de compra a partir de una FOTO (no PDF), eligiendo el motor de OCR configurado.</summary>
    public static class FacturaCompraOcrService
    {
        public static bool HayMotorConfigurado(out string motorActual)
        {
            var cfg = DatabaseService.ObtenerConfigOcr();
            motorActual = cfg?.Motor ?? "Ninguno";
            return !string.IsNullOrWhiteSpace(motorActual) && !string.Equals(motorActual, "Ninguno", StringComparison.OrdinalIgnoreCase);
        }

        public static FacturaCompraPdfImportResult ImportarDesdeFoto(string rutaImagen)
        {
            var cfg = DatabaseService.ObtenerConfigOcr();
            string motor = (cfg?.Motor ?? "Ninguno").Trim();

            FacturaCompraPdfParseResult parse;
            switch (motor)
            {
                case "Azure":
                    parse = AzureDocumentIntelligenceService.AnalizarFactura(rutaImagen, cfg.AzureEndpoint, cfg.AzureClave);
                    break;

                case "Tesseract":
                    string texto = TesseractOcrService.ExtraerTexto(rutaImagen);
                    parse = FacturaCompraPdfService.ParsearTexto(texto);
                    if (string.IsNullOrWhiteSpace(texto))
                        parse.MensajeAdvertencia = "No se pudo leer texto de la imagen. Pruebe con mejor luz/enfoque, recorte solo la factura, o cambie el motor a Azure en Configuración.";
                    break;

                default:
                    throw new InvalidOperationException(
                        "No hay un motor de reconocimiento (OCR) configurado. Vaya a Configuración > Facturas de Compra y elija Tesseract (local) o Azure (nube).");
            }

            return FacturaCompraPdfService.ArmarImportacion(parse);
        }
    }
}
