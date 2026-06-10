using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

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

        /// <summary>
        /// La lista de permisos (ej: "ACCESO_USUARIOS", "ACCESO_FACTURACION").
        /// </summary>
        public static HashSet<string> Permisos { get; private set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Inicia la sesión. Este método es llamado por DatabaseService.
        /// </summary>
        public static void Iniciar(string nombreUsuario, int rolId, List<string> permisos)
        {
            NombreUsuario = nombreUsuario;
            RolID = rolId;
            NombreRol = rolId == 1 ? "Administrador" : $"Rol {rolId}";
            // Normalizamos para evitar fallos por casing/espacios.
            Permisos = new HashSet<string>(
                (permisos ?? new List<string>())
                    .Where(p => !string.IsNullOrWhiteSpace(p))
                    .Select(p => p.Trim()),
                StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Comprueba si el usuario actual tiene un permiso específico.
        /// </summary>
        /// <param name="permiso">El nombre del permiso a comprobar (ej: DatabaseService.PERMISO_USUARIOS)</param>
        /// <returns>True si tiene el permiso, False si no.</returns>
        public static bool TienePermiso(string permisoRequerido)
        {
            if (string.Equals(NombreUsuario, "admin", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (Permisos == null)
            {
                return false; // Sesión no iniciada
            }

            // Un Admin (RolID 1) siempre tiene todos los permisos, sin importar la tabla.
            if (RolID == 1)
            {
                return true;
            }

            bool tiene = Permisos.Contains((permisoRequerido ?? string.Empty).Trim());
#if DEBUG
            if (!tiene)
            {
                string cargados = string.Join(", ", Permisos);
                Debug.WriteLine($"[SesionUsuario permisos] Buscando: '{permisoRequerido}' | En sesión ({Permisos.Count}): {cargados}");
            }
#endif

            return tiene;
        }

        /// <summary>
        /// Cierra la sesión al salir del sistema.
        /// </summary>
        public static void Cerrar()
        {
            NombreUsuario = null;
            RolID = 0;
            Permisos?.Clear();
            Permisos = null;
        }
    }
}