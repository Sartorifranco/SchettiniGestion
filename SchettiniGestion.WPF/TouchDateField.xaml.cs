using System;
using System.Windows;
using System.Windows.Controls;

namespace SchettiniGestion.WPF
{
    public partial class TouchDateField : UserControl
    {
        private static TouchDateField _campoAbierto;

        public static readonly DependencyProperty SelectedDateProperty =
            DependencyProperty.Register(
                nameof(SelectedDate),
                typeof(DateTime?),
                typeof(TouchDateField),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSelectedDateChanged));

        public DateTime? SelectedDate
        {
            get => (DateTime?)GetValue(SelectedDateProperty);
            set => SetValue(SelectedDateProperty, value);
        }

        public TouchDateField()
        {
            InitializeComponent();
            Loaded += TouchDateField_Loaded;
            ActualizarTexto();
        }

        private void TouchDateField_Loaded(object sender, RoutedEventArgs e)
        {
            var estilo = Application.Current.TryFindResource("TouchCalendarStyle") as Style;
            if (estilo != null)
                calInterno.Style = estilo;
        }

        private static void OnSelectedDateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is TouchDateField campo)
                campo.ActualizarTexto();
        }

        private void bordeFecha_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (panelCalendario.Visibility == Visibility.Visible)
            {
                CerrarCalendario();
                return;
            }

            if (_campoAbierto != null && _campoAbierto != this)
                _campoAbierto.CerrarCalendario();

            _campoAbierto = this;
            panelCalendario.Visibility = Visibility.Visible;
            txtChevron.Text = "▲";
            bordeFecha.BorderBrush = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#1E88E5"));

            if (SelectedDate.HasValue)
                calInterno.SelectedDate = SelectedDate;
            else
                calInterno.SelectedDate = DateTime.Today;
        }

        private void calInterno_SelectedDatesChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!calInterno.SelectedDate.HasValue)
                return;

            SelectedDate = calInterno.SelectedDate.Value.Date;
            CerrarCalendario();
        }

        public void CerrarCalendario()
        {
            panelCalendario.Visibility = Visibility.Collapsed;
            txtChevron.Text = "▼";
            bordeFecha.BorderBrush = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#B0BEC5"));
            if (_campoAbierto == this)
                _campoAbierto = null;
        }

        private void ActualizarTexto()
        {
            txtFecha.Text = SelectedDate.HasValue
                ? SelectedDate.Value.ToString("dd/MM/yyyy")
                : "--/--/----";
        }
    }
}
