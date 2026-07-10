namespace AdminLicencias.Api.Contracts;

public sealed class UpsertClienteRequest
{
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
    public bool Activo { get; set; } = true;
}

public sealed class ClienteResponse
{
    public Guid Id { get; set; }
    public string RazonSocial { get; set; } = "";
    public string CUIT { get; set; } = "";
    public string Contacto { get; set; } = "";
    public string Email { get; set; } = "";
    public string Telefono { get; set; } = "";
    public string Ciudad { get; set; } = "";
    public string Provincia { get; set; } = "";
    public string IPServidor { get; set; } = "";
    public int PuertoServidor { get; set; }
    public int CantidadPuestos { get; set; }
    public string CanalContacto { get; set; } = "";
    public string Notas { get; set; } = "";
    public DateTime FechaAlta { get; set; }
    public bool Activo { get; set; }
}

public sealed class RevokeLicenseRequest
{
    public Guid LicenciaId { get; set; }
}
