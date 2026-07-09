using System;
using System.Windows;

namespace SchettiniGestion.WPF
{
    /// <summary>
    /// Utilidades de escala y detección de layout compacto para monitores POS (18" / 1366×768)
    /// hasta pantallas grandes de escritorio.
    /// </summary>
    public static class UiScaleHelper
    {
        public const double DesignContentWidth = 1280;
        public const double DesignContentHeight = 700;
        public const double CompactWidthThreshold = 1280;
        public const double VeryCompactWidthThreshold = 1080;
        public const double CompactHeightThreshold = 680;

        public static double ScreenWidth => SystemParameters.PrimaryScreenWidth;
        public static double ScreenHeight => SystemParameters.PrimaryScreenHeight;
        public static Rect WorkArea => SystemParameters.WorkArea;

        public static double ComputeContentScale(double availableWidth, double availableHeight)
        {
            if (availableWidth <= 0 || availableHeight <= 0)
                return 1.0;

            double scaleX = availableWidth / DesignContentWidth;
            double scaleY = availableHeight / DesignContentHeight;
            double scale = Math.Min(scaleX, scaleY);
            return Math.Max(0.72, Math.Min(1.0, scale));
        }

        public static bool IsCompactWidth(double width) => width > 0 && width < CompactWidthThreshold;

        public static bool IsVeryCompactWidth(double width) => width > 0 && width < VeryCompactWidthThreshold;

        public static bool IsCompactHeight(double height) => height > 0 && height < CompactHeightThreshold;

        public static bool IsSmallScreen()
        {
            return ScreenWidth < CompactWidthThreshold || ScreenHeight < 800;
        }

        public static double PosTotalFontSize(double panelHeight)
        {
            if (panelHeight < 480) return 20;
            if (panelHeight < 560) return 26;
            if (panelHeight < 640) return 32;
            return 36;
        }

        public static Thickness ContentMargin(bool compact)
        {
            return compact ? new Thickness(8) : new Thickness(12);
        }

        public static Thickness ModulePadding(bool compact)
        {
            return compact ? new Thickness(6, 4, 8, 6) : new Thickness(10, 8, 12, 10);
        }

        public static Thickness HeaderPadding(bool compactHeight)
        {
            return compactHeight ? new Thickness(10, 6, 10, 6) : new Thickness(12, 10, 12, 10);
        }

        /// <summary>Ancho útil del área de módulo (ventana menos sidebar y padding).</summary>
        public static double GetViewportWidthForModule(FrameworkElement element)
        {
            var window = Window.GetWindow(element);
            if (window != null && window.ActualWidth > 0)
            {
                double sidebar = IsCompactWidth(window.ActualWidth) ? 72 : 260;
                double padding = IsCompactWidth(window.ActualWidth) ? 14 : 22;
                return Math.Max(640, window.ActualWidth - sidebar - padding);
            }

            return Math.Max(640, WorkArea.Width - 72);
        }

        /// <summary>Alto útil del área de módulo (ventana menos header global y padding).</summary>
        public static double GetViewportHeightForModule(FrameworkElement element)
        {
            var window = Window.GetWindow(element);
            if (window != null && window.ActualHeight > 0)
            {
                double header = IsCompactHeight(window.ActualHeight) ? 50 : 88;
                double padding = IsCompactHeight(window.ActualHeight) ? 10 : 18;
                return Math.Max(400, window.ActualHeight - header - padding);
            }

            return Math.Max(400, WorkArea.Height - 88);
        }
    }
}
