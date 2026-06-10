using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;

namespace SchettiniGestion.WPF
{
    /// <summary>
    /// Refuerza contraste y tamaño táctil del DatePicker aunque falle el estilo global en XAML.
    /// </summary>
    public static class TouchDatePickerHelper
    {
        private static readonly SolidColorBrush InputBackground = new SolidColorBrush(Color.FromRgb(255, 255, 255));
        private static readonly SolidColorBrush InputForeground = new SolidColorBrush(Color.FromRgb(26, 26, 26));

        public static void Apply(DatePicker picker)
        {
            if (picker == null) return;

            picker.MinHeight = 52;
            picker.MinWidth = 190;
            picker.FontSize = 17;
            picker.FontWeight = FontWeights.SemiBold;

            var calendarStyle = Application.Current.TryFindResource("TouchCalendarStyle") as Style;
            if (calendarStyle != null)
                picker.CalendarStyle = calendarStyle;

            picker.Loaded += (_, __) => Configure(picker);
            picker.LayoutUpdated += (_, __) => Configure(picker);
            Configure(picker);
        }

        private static void Configure(DatePicker picker)
        {
            picker.ApplyTemplate();

            if (picker.Template.FindName("PART_TextBox", picker) is DatePickerTextBox textBox)
            {
                textBox.Background = InputBackground;
                textBox.Foreground = InputForeground;
                textBox.CaretBrush = InputForeground;
                textBox.FontSize = 17;
                textBox.FontWeight = FontWeights.SemiBold;
                textBox.MinHeight = 46;
            }

            if (picker.Template.FindName("PART_Button", picker) is Button button)
            {
                button.MinWidth = 48;
                button.MinHeight = 48;
                button.FontSize = 20;
            }

            if (picker.Template.FindName("PART_Calendar", picker) is Calendar calendar)
                ApplyCalendarSize(calendar);

            if (picker.Template.FindName("PART_Popup", picker) is Popup popup)
            {
                popup.Opened -= Popup_Opened;
                popup.Opened += Popup_Opened;
            }
        }

        private static void Popup_Opened(object sender, EventArgs e)
        {
            if (!(sender is Popup popup)) return;
            popup.Dispatcher.BeginInvoke(new Action(() =>
            {
                if (popup.Child is FrameworkElement root)
                {
                    var calendar = FindChild<Calendar>(root);
                    if (calendar != null)
                        ApplyCalendarSize(calendar);
                }
            }), DispatcherPriority.Loaded);
        }

        private static void ApplyCalendarSize(Calendar calendar)
        {
            var calendarStyle = Application.Current.TryFindResource("TouchCalendarStyle") as Style;
            if (calendarStyle != null && calendar.Style != calendarStyle)
                calendar.Style = calendarStyle;

            calendar.MinWidth = 400;
            calendar.MinHeight = 380;
            calendar.FontSize = 17;
        }

        private static T FindChild<T>(DependencyObject parent) where T : DependencyObject
        {
            if (parent == null) return null;
            int count = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T match)
                    return match;
                var nested = FindChild<T>(child);
                if (nested != null)
                    return nested;
            }
            return null;
        }
    }
}
