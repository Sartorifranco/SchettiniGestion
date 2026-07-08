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
            return compact ? new Thickness(8, 6, 8, 8) : new Thickness(12, 10, 12, 12);
        }
    }
}
