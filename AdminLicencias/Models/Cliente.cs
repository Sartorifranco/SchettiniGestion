using System;

namespace AdminLicencias.Models
{
    public class Cliente
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string RazonSocial { get; set; } = "";
        public string CUIT { get; set; } = "";
        public string Contacto { get; set; } = "";
        public string Email { get; set; } = "";
        public string Telefono { get; set; } = "";
        public string Ciudad { get; set; } = "";
        public string Provincia { get; set; } = "";
        /// <summary>IP del servidor SQL (para test de conexión)</summary>
        public string IPServidor { get; set; } = "";
        public int PuertoServidor { get; set; } = 1433;
        /// <summary>Cantidad de puestos instalados (PCs con SCHPOS)</summary>
        public int CantidadPuestos { get; set; } = 1;
        /// <summary>Canal preferido de contacto: WhatsApp / Email / Teléfono</summary>
        public string CanalContacto { get; set; } = "WhatsApp";
        public string Notas { get; set; } = "";
        public DateTime FechaAlta { get; set; } = DateTime.Today;
        public bool Activo { get; set; } = true;
    }
}
