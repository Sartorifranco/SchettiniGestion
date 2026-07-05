using AdminLicencias.Models;
using AdminLicencias.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;

namespace AdminLicencias.Views
{
    public partial class DashboardView : Page
    {
        private readonly MainWindow _main;

        public DashboardView(MainWindow main)
        {
            InitializeComponent();
            _main = main;
            Cargar();
        }

        private void Cargar()
        {
            txtFecha.Text = DateTime.Now.ToString("dddd, dd 'de' MMMM 'de' yyyy",
                new System.Globalization.CultureInfo("es-AR"));

            numActivos.Text   = DataStore.ClientesActivos.ToString();
            numPorVencer.Text = DataStore.ClientesPorVencer.ToString();
            numVencidos.Text  = DataStore.ClientesVencidos.ToString();
            numTotal.Text     = DataStore.Clientes.Count.ToString();

            ingMes.Text   = DataStore.IngresosMesActual.ToString("C0",
                new System.Globalization.CultureInfo("es-AR"));
            ingAnio.Text  = DataStore.IngresosAnioActual.ToString("C0",
                new System.Globalization.CultureInfo("es-AR"));
            ingTotal.Text = DataStore.IngresosTotal.ToString("C0",
                new System.Globalization.CultureInfo("es-AR"));

            // Clientes próximos a vencer o ya vencidos (últimas licencias)
            var filas = DataStore.Clientes
                .Where(c => c.Activo)
                .Select(c => {
                    var lic = DataStore.UltimaLicencia(c.Id);
                    return new {
                        c.RazonSocial, c.CUIT, c.Ciudad,
                        Vencimiento    = lic?.FechaVencimiento.ToString("dd/MM/yyyy") ?? "Sin licencia",
                        DiasRestantes  = lic != null ? lic.DiasRestantes.ToString() : "-",
                        Estado         = lic?.Estado.ToString() ?? "Vencida",
                        _dias          = lic?.DiasRestantes ?? int.MinValue
                    };
                })
                .Where(f => f._dias <= 30)
                .OrderBy(f => f._dias)
                .ToList();

            gridVencer.ItemsSource = filas;
        }
    }
}
