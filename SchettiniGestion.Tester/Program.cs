using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using Microsoft.Data.SqlClient;
using SchettiniGestion;

namespace SchettiniGestion.Tester
{
    partial class Program
    {
        static List<string> Reporte = new List<string>();

        [STAThread]
        static void Main(string[] args)
        {
            Console.WriteLine("=================================================");
            Console.WriteLine("🤖 INICIANDO BOT DE PRUEBAS INTEGRALES SOCTECH...");
            Console.WriteLine("=================================================\n");
            
            Registrar("Inicio de pruebas: " + DateTime.Now.ToString());

            DatabaseService.OnDbError = msg =>
            {
                Registrar("DETALLE: " + msg);
            };

            // 1. Probar Conexión
            if (DatabaseService.InitializeDatabase())
                Registrar("✅ BASE DE DATOS: Conexión exitosa a localhost\\SQLEXPRESS.");
            else
            {
                Registrar("❌ BASE DE DATOS: Falló la conexión. Abortando pruebas.");
                GenerarReporte();
                return;
            }

            // 2. Sesión (admin inicial si BD vacía + contraseña establecida)
            DatabaseService.AsegurarUsuarioAdminInicial();
            if (!DatabaseService.ValidarUsuario("admin", DatabaseService.UsuarioBootstrapAdminContraseña))
            {
                Registrar("❌ SEGURIDAD: Login admin falló (usuario/contraseña bootstrap).");
                GenerarReporte();
                return;
            }
            if (!DatabaseService.CargarSesionUsuario("admin"))
            {
                Registrar("❌ SEGURIDAD: No se pudo cargar sesión de usuario.");
                GenerarReporte();
                return;
            }
            Registrar("✅ SEGURIDAD: Sesión de 'admin' iniciada correctamente.");

            // Tomar estado de caja ANTES de empezar
            decimal cajaInicial = DatabaseService.GetSaldoCaja();
            Registrar($"ℹ️ INFO: Saldo de caja inicial = ${cajaInicial}");

            // 3. Crear un Producto de Prueba
            string codPrueba = "BOT-" + new Random().Next(1000, 9999);
            bool prodCreado = DatabaseService.GuardarProducto(
                id: 0, cod: codPrueba, cb: "779000111", desc: "[TEST BOT] Coca Cola 2L",
                cat: "Bebidas", iva: "21.0", costo: 1000m, gan: 50m, imp: 0m, venta: 1500m, stock: 0, img: "");

            if (!prodCreado)
            {
                Registrar("❌ PRODUCTOS: No se pudo crear el producto de prueba.");
                GenerarReporte(); return;
            }
            Registrar($"✅ PRODUCTOS: Producto '{codPrueba}' creado exitosamente con Stock 0.");

            // Obtener el ID del producto recién creado
            DataRow rowProd = DatabaseService.BuscarProducto(codPrueba);
            int prodId = Convert.ToInt32(rowProd["ProductoID"]);

            // 4. Simular Compra a Proveedor (ID 1) para subir stock
            var itemsCompra = new List<FacturaItem> {
                new FacturaItem { ProductoID = prodId, Codigo = codPrueba, Descripcion = "[TEST BOT] Coca Cola 2L", Cantidad = 100, PrecioUnitario = 1000m }
            };
            
            bool compraOk = DatabaseService.GuardarCompra(pid: 1, tc: "Factura A", t: 100000m, its: itemsCompra, cond: "Contado");
            if (compraOk)
                Registrar("✅ COMPRAS: Compra simulada correctamente (Ingresan 100 unidades).");
            else
                Registrar("❌ COMPRAS: Falló el registro de la compra.");

            // 5. Simular Venta al Contado (Cliente ID 1)
            var itemsVenta = new List<FacturaItem> {
                new FacturaItem { ProductoID = prodId, Codigo = codPrueba, Descripcion = "[TEST BOT] Coca Cola 2L", Cantidad = 5, PrecioUnitario = 1500m }
            };

            bool ventaOk = DatabaseService.GuardarFactura(cid: 1, tc: "Ticket", t: 7500m, its: itemsVenta, condicionVenta: "Contado");
            if (ventaOk)
                Registrar("✅ VENTAS: Venta simulada correctamente (Salen 5 unidades por $7500).");
            else
                Registrar("❌ VENTAS: Falló el registro de la venta.");

            // 6. VERIFICACIONES MATEMÁTICAS CRÍTICAS
            DataRow rowProdFinal = DatabaseService.BuscarProducto(codPrueba);
            int stockFinal = Convert.ToInt32(rowProdFinal["StockActual"]);
            
            if (stockFinal == 95) // 0 + 100 - 5 = 95
                Registrar("✅ AUDITORÍA STOCK: Matemáticas correctas. El stock final es 95.");
            else
                Registrar($"❌ AUDITORÍA STOCK: ERROR CRÍTICO. Se esperaba 95, pero hay {stockFinal}.");

            decimal cajaFinal = DatabaseService.GetSaldoCaja();
            // La caja debería ser: Inicial - 100000 (Compra Contado) + 7500 (Venta Contado) = Inicial - 92500
            decimal cajaEsperada = cajaInicial - 100000m + 7500m;
            
            if (cajaFinal == cajaEsperada)
                Registrar("✅ AUDITORÍA CAJA: Matemáticas correctas. Los movimientos cuadran a la perfección.");
            else
                Registrar($"❌ AUDITORÍA CAJA: ERROR CRÍTICO. Se esperaba ${cajaEsperada}, pero hay ${cajaFinal}.");

            // 7. Limpieza producto
            DatabaseService.EliminarProducto(prodId);
            Registrar("🧹 LIMPIEZA: Producto de prueba eliminado de la base de datos.");

            // 8. Resto de módulos (clientes, CC, compras, caja, informes, remitos/pedidos, etc.)
            EjecutarPruebasModulosExtendidos();

            // 9. Usuarios, roles y permisos (misma lógica que GestionPermisos / UsuariosControl)
            EjecutarPruebasUsuariosRolesYPermisos();

            // 10. Frontend WPF (FlaUI): login, menú, auditoría visual UIA, capturas
            EjecutarPruebasFrontendFlaUI();

            GenerarReporte();
        }

        /// <summary>
        /// Crea un rol de prueba, asigna permisos, crea usuario, valida login y sesión, limpia.
        /// </summary>
        static void EjecutarPruebasUsuariosRolesYPermisos()
        {
            Registrar("");
            Registrar("--- USUARIOS / ROLES / PERMISOS ---");

            var permisos = DatabaseService.GetPermisos();
            if (permisos == null || permisos.Count == 0)
            {
                Registrar("❌ PERMISOS: Catálogo vacío (tabla Permisos).");
                return;
            }
            Registrar($"✅ PERMISOS: Catálogo con {permisos.Count} permiso(s).");

            var rolesIni = DatabaseService.GetRoles();
            if (rolesIni == null || rolesIni.Count == 0)
            {
                Registrar("❌ ROLES: No se pudieron leer roles.");
                return;
            }
            Registrar($"✅ ROLES: Lectura OK ({rolesIni.Count} rol(es)).");

            string rolNombre = "[TEST BOT] Rol " + Guid.NewGuid().ToString("N").Substring(0, 10);
            int nuevoRolId = 0;

            try
            {
                using (var conn = new SqlConnection(DatabaseService.ConnectionString))
                {
                    conn.Open();
                    using (var cmdCheck = new SqlCommand(
                               "SELECT COUNT(*) FROM Roles WHERE LOWER(LTRIM(RTRIM(NombreRol))) = LOWER(LTRIM(RTRIM(@n)))", conn))
                    {
                        cmdCheck.Parameters.AddWithValue("@n", rolNombre);
                        if (Convert.ToInt32(cmdCheck.ExecuteScalar()) > 0)
                        {
                            Registrar("❌ ROLES: Colisión de nombre de rol de prueba.");
                            return;
                        }
                    }

                    using (var cmd = new SqlCommand(
                               "INSERT INTO Roles (NombreRol) VALUES (@n); SELECT CAST(SCOPE_IDENTITY() AS INT);", conn))
                    {
                        cmd.Parameters.AddWithValue("@n", rolNombre);
                        nuevoRolId = Convert.ToInt32(cmd.ExecuteScalar());
                    }
                }
            }
            catch (Exception ex)
            {
                Registrar("❌ ROLES: Error al crear rol de prueba: " + ex.Message);
                return;
            }

            Registrar($"✅ ROLES: Creado rol de prueba '{rolNombre}' (RolID={nuevoRolId}).");

            int pid1 = permisos[0].PermisoId;
            int pid2 = permisos.Count > 1 ? permisos[1].PermisoId : pid1;
            var asignados = pid1 == pid2
                ? new List<int> { pid1 }
                : new List<int> { pid1, pid2 };

            DatabaseService.ActualizarPermisosParaRol(nuevoRolId, asignados);

            var mapa = DatabaseService.GetPermisosPorRol();
            if (!mapa.ContainsKey(nuevoRolId))
            {
                Registrar("❌ PERMISOS: El rol no aparece en Roles_Permisos tras guardar.");
                LimpiarRolPrueba(nuevoRolId);
                return;
            }
            var setDb = new HashSet<int>(mapa[nuevoRolId]);
            if (!asignados.All(id => setDb.Contains(id)) || setDb.Count != asignados.Count)
            {
                Registrar("❌ PERMISOS: Los permisos en BD no coinciden con los asignados.");
                LimpiarRolPrueba(nuevoRolId);
                return;
            }
            Registrar($"✅ PERMISOS: Roles_Permisos coherente ({asignados.Count} permiso(s)).");

            string uNom = "BOTUSER_" + Guid.NewGuid().ToString("N").Substring(0, 12);
            const string uPass = "BotTest#2026!";
            if (!DatabaseService.GuardarUsuario(0, uNom, uPass, nuevoRolId, rolNombre))
            {
                Registrar("❌ USUARIOS: GuardarUsuario falló al crear usuario de prueba.");
                LimpiarRolPrueba(nuevoRolId);
                return;
            }
            Registrar($"✅ USUARIOS: Usuario '{uNom}' creado con el rol de prueba.");

            if (!DatabaseService.ValidarUsuario(uNom, uPass))
            {
                Registrar("❌ USUARIOS: ValidarUsuario falló (hash / contraseña).");
            }
            else
            {
                Registrar("✅ USUARIOS: ValidarUsuario OK.");
            }

            DatabaseService.CargarSesionUsuario(uNom);
            bool okSesion = true;
            foreach (var pid in asignados)
            {
                string nom = permisos.First(p => p.PermisoId == pid).Nombre;
                if (!SesionUsuario.TienePermiso(nom))
                {
                    Registrar("❌ SESIÓN: Falta permiso '" + nom + "' para el usuario de prueba.");
                    okSesion = false;
                }
            }
            if (okSesion && asignados.Count > 0)
                Registrar("✅ SESIÓN: TienePermiso OK para permisos asignados (rol no admin).");

            if (permisos.Count > asignados.Count)
            {
                var noAsignado = permisos.First(p => !asignados.Contains(p.PermisoId));
                if (SesionUsuario.TienePermiso(noAsignado.Nombre))
                    Registrar("❌ SESIÓN: Tiene permiso no asignado: " + noAsignado.Nombre);
                else
                    Registrar("✅ SESIÓN: Permiso no asignado ausente (coherente).");
            }
            else
                Registrar("ℹ️ SESIÓN: Catálogo pequeño; omito prueba de permiso no asignado.");

            DatabaseService.ValidarUsuario("admin", DatabaseService.UsuarioBootstrapAdminContraseña);
            DatabaseService.CargarSesionUsuario("admin");
            Registrar("ℹ️ SESIÓN: Restaurado sesión 'admin' para salida del bot.");

            int uid = ObtenerUsuarioIdPorNombreExacto(uNom);
            if (uid == 0)
                Registrar("❌ USUARIOS: No se encontró UsuarioID del usuario de prueba.");
            else if (!uNom.StartsWith("BOTUSER_", StringComparison.Ordinal))
                Registrar("❌ USUARIOS: No se elimina por seguridad (nombre inesperado).");
            else if (DatabaseService.EliminarUsuario(uid))
                Registrar("🧹 USUARIOS: Usuario de prueba eliminado (UsuarioID=" + uid + ").");
            else
                Registrar("❌ USUARIOS: No se pudo eliminar el usuario de prueba.");

            LimpiarRolPrueba(nuevoRolId);
        }

        static int ObtenerUsuarioIdPorNombreExacto(string nombreUsuario)
        {
            try
            {
                using (var conn = new SqlConnection(DatabaseService.ConnectionString))
                {
                    conn.Open();
                    using (var cmd = new SqlCommand(
                               "SELECT UsuarioID FROM Usuarios WHERE NombreUsuario=@u", conn))
                    {
                        cmd.Parameters.AddWithValue("@u", nombreUsuario);
                        object o = cmd.ExecuteScalar();
                        if (o == null || o == DBNull.Value) return 0;
                        return Convert.ToInt32(o);
                    }
                }
            }
            catch
            {
                return 0;
            }
        }

        static void LimpiarRolPrueba(int rolId)
        {
            if (rolId <= 0) return;
            try
            {
                using (var conn = new SqlConnection(DatabaseService.ConnectionString))
                {
                    conn.Open();
                    new SqlCommand("DELETE FROM Roles_Permisos WHERE RolID=" + rolId, conn).ExecuteNonQuery();
                    new SqlCommand("DELETE FROM Roles WHERE RolID=" + rolId, conn).ExecuteNonQuery();
                }
                Registrar("🧹 ROLES: Rol de prueba eliminado (RolID=" + rolId + ").");
            }
            catch (Exception ex)
            {
                Registrar("❌ ROLES: Error al eliminar rol de prueba: " + ex.Message);
            }
        }

        static void Registrar(string mensaje)
        {
            Console.WriteLine(mensaje);
            Reporte.Add(mensaje);
        }

        static void GenerarReporte()
        {
            string ruta = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Reporte_QA_Soctech.txt");
            File.WriteAllLines(ruta, Reporte);
            Console.WriteLine("\n=================================================");
            Console.WriteLine($"📄 REPORTE GENERADO EN: {ruta}");
            Console.WriteLine("Presiona ENTER para salir...");
            Console.ReadLine();
        }
    }
}
