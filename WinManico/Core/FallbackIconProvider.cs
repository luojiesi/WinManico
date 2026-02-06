using System;
using System.Collections.Generic;
using System.Windows.Media.Imaging;
using System.Windows;

namespace WinManico.Core
{
    public static class FallbackIconProvider
    {
        // Map of common process names to colors for generated icons
        private static readonly Dictionary<string, System.Windows.Media.Color> ProcessColors = new()
        {
            { "taskmgr", System.Windows.Media.Color.FromRgb(0, 120, 215) },      // Blue for Task Manager
            { "regedit", System.Windows.Media.Color.FromRgb(220, 20, 60) },      // Red for Registry Editor
            { "cmd", System.Windows.Media.Color.FromRgb(12, 12, 12) },           // Black for Command Prompt
            { "powershell", System.Windows.Media.Color.FromRgb(1, 36, 86) },     // Dark Blue for PowerShell
            { "mmc", System.Windows.Media.Color.FromRgb(0, 135, 81) },           // Green for MMC consoles
        };

        public static System.Windows.Media.ImageSource GetFallbackIcon(string processName, string title)
        {
            if (string.IsNullOrEmpty(processName))
                return GenerateDefaultIcon();

            // Normalize process name (remove .exe)
            var normalizedName = processName.ToLower().Replace(".exe", "");
            
            // Get color for this process, or use a generic color
            var color = ProcessColors.TryGetValue(normalizedName, out var c) 
                ? c 
                : System.Windows.Media.Color.FromRgb(100, 100, 100);

            // Get first letter for the icon
            var letter = GetFirstLetter(normalizedName, title);
            
            return GenerateLetterIcon(letter, color);
        }

        private static string GetFirstLetter(string processName, string title)
        {
            // Try to get a meaningful first letter
            if (!string.IsNullOrEmpty(processName) && char.IsLetter(processName[0]))
                return processName[0].ToString().ToUpper();
            
            if (!string.IsNullOrEmpty(title) && char.IsLetter(title[0]))
                return title[0].ToString().ToUpper();
            
            return "?";
        }

        private static System.Windows.Media.ImageSource GenerateLetterIcon(string letter, System.Windows.Media.Color bgColor)
        {
            const int size = 48;
            var drawingVisual = new System.Windows.Media.DrawingVisual();
            
            using (var context = drawingVisual.RenderOpen())
            {
                // Background circle
                var brush = new System.Windows.Media.SolidColorBrush(bgColor);
                context.DrawEllipse(brush, null, new System.Windows.Point(size / 2.0, size / 2.0), size / 2.0, size / 2.0);
                
                // Letter text
                var formattedText = new System.Windows.Media.FormattedText(
                    letter,
                    System.Globalization.CultureInfo.CurrentCulture,
                    System.Windows.FlowDirection.LeftToRight,
                    new System.Windows.Media.Typeface("Segoe UI"),
                    size * 0.6,
                    System.Windows.Media.Brushes.White,
                    System.Windows.Media.VisualTreeHelper.GetDpi(drawingVisual).PixelsPerDip);
                
                var textX = (size - formattedText.Width) / 2;
                var textY = (size - formattedText.Height) / 2;
                
                context.DrawText(formattedText, new System.Windows.Point(textX, textY));
            }
            
            var renderTarget = new RenderTargetBitmap(size, size, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
            renderTarget.Render(drawingVisual);
            
            return renderTarget;
        }

        private static System.Windows.Media.ImageSource GenerateDefaultIcon()
        {
            return GenerateLetterIcon("?", System.Windows.Media.Color.FromRgb(128, 128, 128));
        }
    }
}
