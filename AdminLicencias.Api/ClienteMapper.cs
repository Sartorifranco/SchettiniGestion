using AdminLicencias.Api.Contracts;
using AdminLicencias.Core.Models;
using AdminLicencias.Core.Services;

namespace AdminLicencias.Api;

internal static class ClienteMapper
{
    public static string? Validar(UpsertClienteRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.RazonSocial))
            return "La Razón Social es obligatoria.";

        if (!string.IsNullOrWhiteSpace(req.CUIT))
        {
            string cuit = new string(req.CUIT.Where(char.IsDigit).ToArray());
            if (cuit.Length != 11)
                return "El CUIT debe tener 11 dígitos.";
        }

        if (req.PuertoServidor is < 1 or > 65535)
            return "El puerto del servidor debe estar entre 1 y 65535.";

        if (req.CantidadPuestos < 1)
            return "La cantidad de puestos debe ser al menos 1.";

        return null;
    }

    public static Cliente ToEntity(UpsertClienteRequest req, Cliente? existing = null)
    {
        var c = existing ?? new Cliente
        {
            Id = Guid.NewGuid(),
            FechaAlta = DateTime.Today
        };

        c.RazonSocial = req.RazonSocial.Trim();
        c.CUIT = string.IsNullOrWhiteSpace(req.CUIT)
            ? ""
            : new string(req.CUIT.Where(char.IsDigit).ToArray());
        c.Contacto = req.Contacto?.Trim() ?? "";
        c.Email = req.Email?.Trim() ?? "";
        c.Telefono = req.Telefono?.Trim() ?? "";
        c.Ciudad = req.Ciudad?.Trim() ?? "";
        c.Provincia = req.Provincia?.Trim() ?? "";
        c.IPServidor = req.IPServidor?.Trim() ?? "";
        c.PuertoServidor = req.PuertoServidor > 0 ? req.PuertoServidor : 1433;
        c.CantidadPuestos = req.CantidadPuestos > 0 ? req.CantidadPuestos : 1;
        c.CanalContacto = string.IsNullOrWhiteSpace(req.CanalContacto) ? "WhatsApp" : req.CanalContacto.Trim();
        c.Notas = req.Notas?.Trim() ?? "";
        c.Activo = req.Activo;

        return c;
    }

    public static ClienteResponse ToResponse(ClienteDetalleDto dto) => new()
    {
        Id = dto.Id,
        RazonSocial = dto.RazonSocial,
        CUIT = dto.CUIT,
        Contacto = dto.Contacto,
        Email = dto.Email,
        Telefono = dto.Telefono,
        Ciudad = dto.Ciudad,
        Provincia = dto.Provincia,
        IPServidor = dto.IPServidor,
        PuertoServidor = dto.PuertoServidor,
        CantidadPuestos = dto.CantidadPuestos,
        CanalContacto = dto.CanalContacto,
        Notas = dto.Notas,
        FechaAlta = dto.FechaAlta,
        Activo = dto.Activo
    };
}
