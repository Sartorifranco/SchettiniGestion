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

        /// <summary>
        /// La lista de permisos (ej: "ACCESO_USUARIOS", "ACCESO_FACTURACION").
        /// </summary>
        private static HashSet<string> Permisos { get; set; }

        /// <summary>
        /// Inicia la sesión. Este método es llamado por DatabaseService.
        /// </summary>
        public static void Iniciar(string nombreUsuario, int rolId, List<string> permisos)
        {
            NombreUsuario = nombreUsuario;
            RolID = rolId;
            // Usamos un HashSet para búsquedas de permisos ultra-rápidas
            Permisos = new HashSet<string>(permisos);
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

            // Un Admin (RolID 1) siempre tiene todos los permisos, sin importar la tabla.
            if (RolID == 1)
            {
                return true;
            }

            // Para otros roles, comprobamos la lista
            return Permisos.Contains(permiso);
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