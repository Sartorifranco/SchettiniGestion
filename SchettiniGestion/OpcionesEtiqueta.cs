namespace SchettiniGestion
{
    /// <summary>Opciones de contenido y tamaño para impresión de etiquetas de producto.</summary>
    public class OpcionesEtiqueta
    {
        public int AnchoMm { get; set; } = 50;
        public int AltoMm { get; set; } = 25;
        public int GapHorizontalMm { get; set; } = 2;
        public int GapVerticalMm { get; set; } = 2;
        public int MargenIzquierdoMm { get; set; } = 5;
        public int MargenSuperiorMm { get; set; } = 5;
        public int MargenDerechoMm { get; set; } = 5;
        public int MargenInferiorMm { get; set; } = 5;
        public int Columnas { get; set; } = 3;
        public string Orientacion { get; set; } = "Vertical";
        public string ModoImpresion { get; set; } = "Rollo";
        public bool MostrarDescripcion { get; set; } = true;
        public bool MostrarDescripcionExtra { get; set; } = false;
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
        public string DescripcionExtra { get; set; }
        public string Marca { get; set; }
        public decimal PrecioVenta { get; set; }
        public int Cantidad { get; set; } = 1;
    }
}
