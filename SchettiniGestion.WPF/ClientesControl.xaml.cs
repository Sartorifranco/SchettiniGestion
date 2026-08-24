using Microsoft.Win32;
using OfficeOpenXml;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SchettiniGestion;

namespace SchettiniGestion.WPF
{
    public partial class ClientesControl : UserControl, ISincronizableEnRed
    {
        private List<ClienteListadoItem> _clientesTodos = new List<ClienteListadoItem>();

        public ClientesControl()
        {
            InitializeComponent();
        }

        private bool _inicializado;

        private void ClientesControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (_inicializado) return;
            _inicializado = true;
            CargarClientes();
        }

        public void AplicarCambioRed(string entidad)
        {
            if (!_inicializado) return;
            if (!string.IsNullOrEmpty(entidad) && entidad != "Clientes" && entidad != "CuentaCorriente")
                return;
            if (RedSyncWatcher.HayVentanaVisible<ClienteModalWindow>())
                return;
            CargarClientes();
        }

        private void CargarClientes()
        {
            try
            {
                _clientesTodos = DatabaseService.GetClientesLista() ?? new List<ClienteListadoItem>();
                AplicarFiltro();
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show("Error al cargar clientes: " + ex.Message);
            }
        }

        private void AplicarFiltro()
        {
            if (_clientesTodos == null) { dgvClientes.ItemsSource = null; return; }

            string t = (txtFiltroClientes?.Text ?? "").Trim();
            IEnumerable<ClienteListadoItem> q = _clientesTodos;
            if (!string.IsNullOrEmpty(t))
            {
                q = q.Where(c =>
                    (c.RazonSocial ?? "").IndexOf(t, StringComparison.OrdinalIgnoreCase) >= 0
                    || (c.CUIT ?? "").IndexOf(t, StringComparison.OrdinalIgnoreCase) >= 0);
            }

            dgvClientes.ItemsSource = q.ToList();
        }

        private void txtFiltroClientes_TextChanged(object sender, TextChangedEventArgs e)
        {
            AplicarFiltro();
        }

        private void btnNuevo_Click(object sender, RoutedEventArgs e)
        {
            var modal = new ClienteModalWindow(0, CargarClientes) { Owner = Window.GetWindow(this) };
            modal.ShowDialog();
        }

        private void dgvClientes_MouseDoubleClick(object sender, MouseButtonEventArgs e) => AbrirEditar();

        private void MenuItemEditar_Click(object sender, RoutedEventArgs e) => AbrirEditar();

        private void MenuItemEliminar_Click(object sender, RoutedEventArgs e)
        {
            if (!(dgvClientes.SelectedItem is ClienteListadoItem item)) return;

            if (CustomMessageBox.Show($"¿Eliminar el cliente '{item.RazonSocial}'?", "Confirmar", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                if (DatabaseService.EliminarCliente(item.ClienteID))
                    CargarClientes();
                else
                    CustomMessageBox.Show("No se pudo eliminar el cliente. Puede tener facturas o movimientos asociados.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void AbrirEditar()
        {
            if (!(dgvClientes.SelectedItem is ClienteListadoItem item)) return;
            var modal = new ClienteModalWindow(item.ClienteID, CargarClientes) { Owner = Window.GetWindow(this) };
            modal.ShowDialog();
        }

        private void btnPlantillaClientes_Click(object sender, RoutedEventArgs e)
        {
            var sfd = new SaveFileDialog
            {
                Filter = "Excel (*.xlsx)|*.xlsx|CSV (*.csv)|*.csv",
                FileName = "plantilla-clientes-schpos",
                Title = "Guardar plantilla de clientes"
            };
            if (sfd.ShowDialog() != true) return;
            try
            {
                if (sfd.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
                    GuardarPlantillaClientesExcel(sfd.FileName);
                else
                    File.WriteAllText(sfd.FileName, EncabezadoPlantillaClientes() + Environment.NewLine, Encoding.UTF8);
                CustomMessageBox.Show("Plantilla guardada.\n\nCompletá una fila por cliente. Si el CUIT ya existe, se actualiza.",
                    "Plantilla", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show("No se pudo guardar la plantilla: " + ex.Message, "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static string EncabezadoPlantillaClientes()
        {
            return "RazonSocial;CUIT;CondicionIVA;Telefono;Email;Direccion;PermiteCuentaCorriente";
        }

        private static void GuardarPlantillaClientesExcel(string ruta)
        {
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            using (var pkg = new ExcelPackage())
            {
                var ws = pkg.Workbook.Worksheets.Add("Clientes");
                string[] cols = EncabezadoPlantillaClientes().Split(';');
                for (int i = 0; i < cols.Length; i++)
                    ws.Cells[1, i + 1].Value = cols[i];
                ws.Cells[2, 1].Value = "Ejemplo SA";
                ws.Cells[2, 2].Value = "30-12345678-9";
                ws.Cells[2, 3].Value = "Responsable Inscripto";
                ws.Cells[2, 4].Value = "11-5555-5555";
                ws.Cells[2, 5].Value = "mail@ejemplo.com";
                ws.Cells[2, 6].Value = "Calle 123";
                ws.Cells[2, 7].Value = "SI";
                ws.Cells[1, 1, 1, cols.Length].Style.Font.Bold = true;
                pkg.SaveAs(new FileInfo(ruta));
            }
        }

        private void btnImportarClientes_Click(object sender, RoutedEventArgs e)
        {
            var ofd = new OpenFileDialog
            {
                Filter = "Clientes|*.csv;*.xlsx|CSV (*.csv)|*.csv|Excel (*.xlsx)|*.xlsx",
                Title = "Importar clientes"
            };
            if (ofd.ShowDialog() != true) return;
            try
            {
                List<DatabaseService.ClienteImportacionItem> filas = ofd.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase)
                    ? LeerClientesExcel(ofd.FileName)
                    : LeerClientesCsv(ofd.FileName);
                if (filas == null || filas.Count == 0)
                {
                    CustomMessageBox.Show("No hay filas para importar (revisá el encabezado y que no esté vacía).",
                        "Importar", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                var res = DatabaseService.ImportarClientesMasivo(filas);
                var extra = res.Mensajes.Count == 0 ? "" : "\n\n" + string.Join("\n", res.Mensajes);
                CustomMessageBox.Show(
                    "Altas: " + res.Altas + "\nActualizados: " + res.Actualizados + "\nErrores: " + res.Errores + extra,
                    "Importar clientes", MessageBoxButton.OK,
                    res.Errores > 0 ? MessageBoxImage.Warning : MessageBoxImage.Information);
                CargarClientes();
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show("Error al importar: " + ex.Message, "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static List<DatabaseService.ClienteImportacionItem> LeerClientesCsv(string ruta)
        {
            var lineas = File.ReadAllLines(ruta, DetectarEncoding(ruta));
            if (lineas.Length < 2) return new List<DatabaseService.ClienteImportacionItem>();
            char sep = lineas[0].Contains(";") ? ';' : ',';
            int[] mapa = MapearColumnasClientes(lineas[0].Split(sep));
            var list = new List<DatabaseService.ClienteImportacionItem>();
            for (int i = 1; i < lineas.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(lineas[i])) continue;
                var cols = lineas[i].Split(sep);
                var item = ItemDesdeColumnas(cols, mapa, i + 1);
                if (item != null) list.Add(item);
            }
            return list;
        }

        private static List<DatabaseService.ClienteImportacionItem> LeerClientesExcel(string ruta)
        {
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            using (var pkg = new ExcelPackage(new FileInfo(ruta)))
            {
                var ws = pkg.Workbook.Worksheets.FirstOrDefault();
                if (ws?.Dimension == null) return new List<DatabaseService.ClienteImportacionItem>();
                int cols = ws.Dimension.End.Column;
                int rows = ws.Dimension.End.Row;
                var headers = new List<string>();
                for (int c = 1; c <= cols; c++)
                    headers.Add(ws.Cells[1, c].Text ?? "");
                int[] mapa = MapearColumnasClientes(headers);
                var list = new List<DatabaseService.ClienteImportacionItem>();
                for (int r = 2; r <= rows; r++)
                {
                    var datos = new string[cols];
                    for (int c = 1; c <= cols; c++)
                        datos[c - 1] = ws.Cells[r, c].Text ?? "";
                    var item = ItemDesdeColumnas(datos, mapa, r);
                    if (item != null) list.Add(item);
                }
                return list;
            }
        }

        private static int[] MapearColumnasClientes(IList<string> headers)
        {
            int[] mapa = { -1, -1, -1, -1, -1, -1, -1 };
            for (int i = 0; i < headers.Count; i++)
            {
                string h = (headers[i] ?? "").Trim().ToUpperInvariant()
                    .Replace(" ", "").Replace("_", "").Replace("Ó", "O").Replace("Í", "I");
                if (h.Contains("RAZON")) mapa[0] = i;
                else if (h.Contains("CUIT") || h == "CUIL") mapa[1] = i;
                else if (h.Contains("CONDICION") || h.Contains("IVA")) mapa[2] = i;
                else if (h.Contains("TELEFONO") || h.Contains("TEL")) mapa[3] = i;
                else if (h.Contains("MAIL")) mapa[4] = i;
                else if (h.Contains("DIREC") || h.Contains("DOMIC")) mapa[5] = i;
                else if (h.Contains("CUENTA") || h.Contains("CTACTE") || h.Contains("PERMITE")) mapa[6] = i;
            }
            return mapa;
        }

        private static DatabaseService.ClienteImportacionItem ItemDesdeColumnas(string[] cols, int[] mapa, int fila)
        {
            string Get(int idx)
            {
                int col = mapa[idx];
                if (col < 0 || col >= cols.Length) return "";
                return (cols[col] ?? "").Trim();
            }
            string razon = Get(0);
            if (string.IsNullOrWhiteSpace(razon)) return null;
            string cta = Get(6).ToUpperInvariant();
            bool permite = cta == "SI" || cta == "SÍ" || cta == "1" || cta == "TRUE" || cta == "VERDADERO";
            return new DatabaseService.ClienteImportacionItem
            {
                Fila = fila,
                RazonSocial = razon,
                Cuit = Get(1),
                CondicionIva = Get(2),
                Telefono = Get(3),
                Email = Get(4),
                Direccion = Get(5),
                PermiteCuentaCorriente = permite
            };
        }

        private static Encoding DetectarEncoding(string ruta)
        {
            using (var fs = new FileStream(ruta, FileMode.Open, FileAccess.Read))
            {
                if (fs.Length >= 3)
                {
                    int b1 = fs.ReadByte(), b2 = fs.ReadByte(), b3 = fs.ReadByte();
                    if (b1 == 0xEF && b2 == 0xBB && b3 == 0xBF) return Encoding.UTF8;
                }
            }
            return Encoding.Default;
        }
    }
}
