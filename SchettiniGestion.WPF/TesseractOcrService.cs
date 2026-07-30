using System;
using System.IO;
using Tesseract;

namespace SchettiniGestion.WPF
{
    /// <summary>OCR local/offline usando el motor Tesseract (no requiere internet ni cuentas externas).</summary>
    public static class TesseractOcrService
    {
        public static string RutaTessdata => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tessdata");

        public static bool EstaDisponible()
        {
            return File.Exists(Path.Combine(RutaTessdata, "spa.traineddata"));
        }

        public static string ExtraerTexto(string rutaImagen)
        {
            if (!EstaDisponible())
                throw new InvalidOperationException(
                    "No se encontraron los datos de idioma de Tesseract (carpeta tessdata\\spa.traineddata). Reinstale SCHPOS o cambie el motor a Azure en Configuración.");

            using (var engine = new TesseractEngine(RutaTessdata, "spa", EngineMode.Default))
            using (var img = Pix.LoadFromFile(rutaImagen))
            using (var page = engine.Process(img))
            {
                return page.GetText() ?? "";
            }
        }
    }
}
