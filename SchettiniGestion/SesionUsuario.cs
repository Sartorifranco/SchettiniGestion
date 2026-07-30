using System;
using System.Collections.Generic;

namespace SchettiniGestion
{
    /// <summary>
    /// Almacena de forma global los datos del usuario que ha iniciado sesión.
    /// </summary>
    public static class SesionUsuario
    {
        /// <summary>
        /// El nombre del usuario logueado (ej: "admin", "gaston").
        /// </summary>
        public static string NombreUsuario { get; private set; }

        /// <summary>
        /// El ID del rol del usuario (ej: 1 para Admin).
        /// </summary>
        public static int RolID { get; private set; }

        public static string NombreRol { get; private set; }

        /// <summary>Nombre real del personal (vendedor/cajero) para reportes.</summary>
        public static string NombrePersonal { get; private set; }

        public static int UsuarioID { get; private set; }

        /// <summary>
        /// Usuario técnico oculto de soporte (login 9999). Permisos:
        /// acceso total a módulos, recuperación/reset de contraseñas, borrado duro
        /// de productos/registros, configuración avanzada, mantenimiento de BD y
        /// activación de funciones especiales. Todas sus acciones sensibles deben
        /// registrarse en AccionesTecnicas.
        /// </summary>
        public static bool EsUsuarioTecnico { get; private set; }

        /// <summary>Nombre a persistir en ventas y movimientos: personal real o login si no hay.</summary>
        public static string NombreParaRegistro()
        {
            return !string.IsNullOrWhiteSpace(NombrePersonal) ? NombrePersonal.Trim() : (NombreUsuario ?? "");
        }

        /// <summary>
        /// La lista de permisos (ej: "ACCESO_USUARIOS", "ACCESO_FACTURACION").
        /// </summary>
        private static HashSet<string> Permisos { get; set; }

        /// <summary>
        /// Inicia la sesión. Este método es llamado por DatabaseService.
        /// </summary>
        public static void Iniciar(string nombreUsuario, int rolId, string nombreRol, int usuarioId, string nombrePersonal, List<string> permisos)
        {
            NombreUsuario = nombreUsuario;
            RolID = rolId;
            NombreRol = string.IsNullOrWhiteSpace(nombreRol)
                ? (rolId == 1 ? "Administrador" : $"Rol {rolId}")
                : nombreRol;
            UsuarioID = usuarioId;
            NombrePersonal = nombrePersonal;
            Permisos = new HashSet<string>(permisos);
            EsUsuarioTecnico = false;
        }

        public static void IniciarTecnico(string nombreUsuario, List<string> permisos)
        {
            NombreUsuario = nombreUsuario;
            RolID = 1;
            NombreRol = "Soporte Tecnico";
            UsuarioID = -9999;
            NombrePersonal = "Soporte Tecnico";
            Permisos = new HashSet<string>(permisos ?? new List<string>());
            Permisos.Add("ACCESO_TOTAL");
            EsUsuarioTecnico = true;
        }

        /// <summary>
        /// Comprueba si el usuario actual tiene un permiso específico.
        /// </summary>
        /// <param name="permiso">El nombre del permiso a comprobar (ej: DatabaseService.PERMISO_USUARIOS)</param>
        /// <returns>True si tiene el permiso, False si no.</returns>
        public static bool TienePermiso(string permiso)
        {
            if (Permisos == null)
            {
                return false; // Sesión no iniciada
            }

            if (EsUsuarioTecnico)
                return true;

            if (Permisos.Contains("ACCESO_TOTAL"))
                return true;

            return Permisos.Contains(permiso);
        }

        /// <summary>
        /// Cierra la sesión al salir del sistema.
        /// </summary>
        public static void Cerrar()
        {
            NombreUsuario = null;
            RolID = 0;
            NombreRol = null;
            NombrePersonal = null;
            UsuarioID = 0;
            EsUsuarioTecnico = false;
            Permisos?.Clear();
            Permisos = null;
        }
    }
}