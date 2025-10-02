using System;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Web.WebView2.Wpf;

namespace CoverageAnalyzerGUI.Helpers
{
    /// <summary>
    /// Attached behavior to fix WebView2 sizing issues in AvalonDock
    /// This ensures WebView2 controls properly resize when their container changes size
    /// </summary>
    public static class WebView2SizingBehavior
    {
        public static readonly DependencyProperty EnableAutoSizingProperty =
            DependencyProperty.RegisterAttached(
                "EnableAutoSizing",
                typeof(bool),
                typeof(WebView2SizingBehavior),
                new PropertyMetadata(false, OnEnableAutoSizingChanged));

        public static bool GetEnableAutoSizing(DependencyObject obj)
        {
            return (bool)obj.GetValue(EnableAutoSizingProperty);
        }

        public static void SetEnableAutoSizing(DependencyObject obj, bool value)
        {
            obj.SetValue(EnableAutoSizingProperty, value);
        }

        private static void OnEnableAutoSizingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is WebView2 webView && e.NewValue is bool enable && enable)
            {
                webView.Loaded += WebView_Loaded;
            }
        }

        private static void WebView_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is WebView2 webView)
            {
                // Find the parent Grid
                var parent = FindParent<Grid>(webView);
                if (parent != null)
                {
                    // Subscribe to parent size changes
                    parent.SizeChanged += (s, args) => UpdateWebViewSize(webView, parent);
                    
                    // Initial size update
                    UpdateWebViewSize(webView, parent);
                }
            }
        }

        private static void UpdateWebViewSize(WebView2 webView, Grid parent)
        {
            try
            {
                // Find which row the WebView2 is in
                int row = Grid.GetRow(webView);
                
                // Calculate the available height for this row
                double availableHeight = CalculateRowHeight(parent, row);
                
                if (availableHeight > 0 && !double.IsNaN(availableHeight) && !double.IsInfinity(availableHeight))
                {
                    // Set explicit height to force WebView2 to respect the layout
                    webView.Height = availableHeight;
                }
            }
            catch
            {
                // Silently ignore errors
            }
        }

        private static double CalculateRowHeight(Grid grid, int targetRow)
        {
            if (grid.RowDefinitions.Count <= targetRow)
                return grid.ActualHeight;

            var rowDef = grid.RowDefinitions[targetRow];
            
            // If it's a star-sized row, calculate actual height
            if (rowDef.Height.IsStar)
            {
                double totalHeight = grid.ActualHeight;
                double usedHeight = 0;

                // Subtract heights of Auto and Absolute rows
                for (int i = 0; i < grid.RowDefinitions.Count; i++)
                {
                    if (i != targetRow)
                    {
                        var rd = grid.RowDefinitions[i];
                        if (rd.Height.IsAuto)
                        {
                            // For Auto rows, we need to measure the actual content
                            // For simplicity, assume Auto rows take their ActualHeight
                            usedHeight += rd.ActualHeight;
                        }
                        else if (rd.Height.IsAbsolute)
                        {
                            usedHeight += rd.Height.Value;
                        }
                    }
                }

                return Math.Max(0, totalHeight - usedHeight);
            }
            else if (rowDef.Height.IsAbsolute)
            {
                return rowDef.Height.Value;
            }
            else
            {
                return rowDef.ActualHeight;
            }
        }

        private static T? FindParent<T>(DependencyObject child) where T : DependencyObject
        {
            DependencyObject? parentObject = System.Windows.Media.VisualTreeHelper.GetParent(child);

            if (parentObject == null)
                return null;

            if (parentObject is T parent)
                return parent;

            return FindParent<T>(parentObject);
        }
    }
}
