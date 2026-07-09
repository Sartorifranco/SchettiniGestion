namespace SchettiniGestion
{
    public class OpcionesImpresionTicket
    {
        public int AnchoMm { get; set; } = 80;
        public bool MostrarLogo { get; set; } = true;
        public bool MostrarDireccion { get; set; } = true;
        public bool MostrarTelefono { get; set; } = true;
        public bool MostrarCuit { get; set; } = true;
        public bool MostrarCliente { get; set; } = true;
        public bool MostrarCodigo { get; set; }
        public bool MostrarFormaPago { get; set; } = true;
        public bool MostrarGracias { get; set; } = true;
        public bool MostrarPieFiscal { get; set; } = true;
        /// <summary>Número de punto de venta (valor en Negocio y AFIP).</summary>
        public bool MostrarPuntoVenta { get; set; } = true;
        /// <summary>Nombre del personal que cobró / emitió la venta.</summary>
        public bool MostrarVendedor { get; set; }
    }
}
