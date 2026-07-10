namespace AdminLicencias.Api.Services;

public sealed class AuditLogEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime Fecha { get; set; } = DateTime.Now;
    public string Usuario { get; set; } = "";
    public string Accion { get; set; } = "";
    public string Ip { get; set; } = "";
    public string Navegador { get; set; } = "";
    public string Metodo { get; set; } = "";
    public string Ruta { get; set; } = "";
}

public sealed class AuditLogEntryDto
{
    public Guid Id { get; set; }
    public DateTime Fecha { get; set; }
    public string Usuario { get; set; } = "";
    public string Accion { get; set; } = "";
    public string Ip { get; set; } = "";
    public string Navegador { get; set; } = "";
}
