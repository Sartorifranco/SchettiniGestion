namespace SchettiniGestion.WPF
{
    /// <summary>Textos de ayuda reutilizables (botones «¿Cómo funciona?» y mensajes informativos).</summary>
    internal static class TextosAyudaUi
    {
        public const string ArcaChecklist =
            "Pasos para facturar con ARCA:\n\n" +
            "1) Completá los Datos de la Empresa (CUIT, razón social, condición IVA) y guardá.\n" +
            "2) Generá el Pedido de Certificado (CSR) desde esta pantalla.\n" +
            "3) En ARCA (Clave Fiscal) subí el CSR y descargá el certificado .crt.\n" +
            "4) Subí el .crt aquí y autorizá el servicio wsfe (Factura Electrónica) en ARCA.\n" +
            "5) Indicá el Punto de Venta habilitado en ARCA (debe coincidir exactamente).\n" +
            "6) Dejá «producción» desmarcada y usá Probar conexión (ambiente de homologación / prueba).\n" +
            "7) Cuando todo funcione, marcá Ambiente ARCA: producción para emitir CAE reales.\n\n" +
            "Homologación = pruebas (no válidas fiscalmente).\n" +
            "Producción = comprobantes reales con validez fiscal.\n\n" +
            "Si no tenés el extra ARCA en la licencia, esta sección no aparece.";

        public const string ArcaAmbienteHint =
            "Desmarcado = homologación (pruebas). Marcado = producción (CAE reales). Activá producción solo con certificado y punto de venta de producción autorizados en ARCA.";

        public const string ArcaPuntoVentaHint =
            "Número de punto de venta habilitado en ARCA (ej. 1). Debe coincidir exactamente. Sin PV no se pueden emitir facturas electrónicas.";

        public const string LicenciaPasos =
            "Cómo activar o renovar la licencia:\n\n" +
            "1) Copiá el ID de máquina de esta PC.\n" +
            "2) Enviálo a soporte (info@schettini.com.ar) indicando los extras que necesitás.\n" +
            "3) Pegá aquí la clave recibida (o cargá el archivo licencia.key) y tocá Validar y activar.\n" +
            "4) Reiniciá SCHPOS para que se apliquen los módulos.\n\n" +
            "Extras habituales: Red (varias cajas), ARCA (factura electrónica), Etiquetas, Visor cliente, Mercado Pago QR, Point Smart, Soporte.";

        public const string ImpresionDestinoVsPreguntar =
            "Cómo se relacionan estas opciones:\n\n" +
            "• Destino al cobrar: define el formato por defecto (Ticket, A4, PDF o «Preguntar al cobrar»).\n" +
            "• Si Destino = Preguntar al cobrar, al finalizar la venta se elige ticket / A4 / PDF.\n" +
            "• El checkbox «Preguntar antes de imprimir» se usa cuando el Destino ya está fijo: " +
            "si está marcado, confirma antes de enviar a la impresora; si está desmarcado, imprime directo.\n\n" +
            "Ejemplo: Destino = Ticket + Preguntar desmarcado → al cobrar sale el ticket sin diálogo.";

        public const string NcNdDesdePos =
            "Nota de crédito / débito desde el POS:\n\n" +
            "Desde acá se genera una nota libre (interna), sin vincular automáticamente a una factura anterior.\n\n" +
            "Para anular o acreditar una factura ya emitida (recomendada con ARCA):\n" +
            "Ventas → historial → acción NC (nota vinculada a la factura original, con CAE si corresponde).\n\n" +
            "Eliminar una nota del listado no cancela el CAE en ARCA.";

        public const string ModosEtiqueta =
            "Modos de impresión de etiquetas:\n\n" +
            "• Rollo — impresora térmica de etiquetas (medida del rollo en mm). El auto-corte solo aplica acá.\n" +
            "• A4 — hoja A4 con varias etiquetas (columnas y márgenes en la pestaña Hoja A4).\n" +
            "• Cartel — formato grande en A4 para exhibir precio (impresora A4/etiquetas).\n" +
            "• Góndola — similar a cartel, pensado para precios en góndola en hoja A4.\n\n" +
            "La impresora se elige en Configuración → Impresoras.";

        public const string PointModoPdv =
            "Modo PDV (Point of Sale) en Mercado Pago:\n\n" +
            "Sin activarlo, SCHPOS no puede enviar el importe a la terminal Point.\n" +
            "Necesitás internet en la PC y en el Point, y la terminal vinculada a la misma cuenta del Access Token.\n\n" +
            "Al cobrar, el cliente paga en la terminal. Si cancelás en SCHPOS, también se cancela el cobro en Mercado Pago.";
    }
}
