namespace SchettiniGestion
{
    /// <summary>Opciones de contenido y tamaño para impresión de etiquetas de producto.</summary>
    public class OpcionesEtiqueta
    {
        public int AnchoMm { get; set; } = 50;
        public int AltoMm { get; set; } = 25;
        public bool MostrarDescripcion { get; set; } = true;
        public bool MostrarPrecio { get; set; } = true;
        public bool MostrarCodigo { get; set; } = false;
        public bool MostrarCodigoBarras { get; set; } = true;
        public bool MostrarMarca { get; set; } = false;
    }

    /// <summary>Ítem a imprimir (una o más etiquetas del mismo producto).</summary>
    public class EtiquetaPrintItem
    {
        public int ProductoID { get; set; }
        public string Codigo { get; set; }
        public string CodigoBarra { get; set; }
        public string Descripcion { get; set; }
        public string Marca { get; set; }
        public decimal PrecioVenta { get; set; }
        public int Cantidad { get; set; } = 1;
    }
}
