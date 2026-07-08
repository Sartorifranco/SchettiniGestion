namespace AdminLicencias.Core.Models;

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
    public string IPServidor { get; set; } = "";
    public int PuertoServidor { get; set; } = 1433;
    public int CantidadPuestos { get; set; } = 1;
    public string CanalContacto { get; set; } = "WhatsApp";
    public string Notas { get; set; } = "";
    public DateTime FechaAlta { get; set; } = DateTime.Today;
    public bool Activo { get; set; } = true;
}
