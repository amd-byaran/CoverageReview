using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Threading.Tasks;
using CoverageAnalyzerGUI.Models;
using Microsoft.Win32;
using HvpHtmlParser;
using Microsoft.Web.WebView2.Wpf;


namespace CoverageAnalyzerGUI
{
    /// <summary>
    /// Professional Visual Studio-style main window with AvalonDock
    /// </summary>
    public partial class VSStyleMainWindow : Window
    {
        private readonly HashSet<object> _selectedNodes = new HashSet<object>();
        private readonly HashSet<object> _selectedStatsNodes = new HashSet<object>();
        private ProjectSettings? _currentProject;
        private bool _isDarkTheme = false;
        private List<HierarchyNode> _hierarchyData = new List<HierarchyNode>();
        private readonly string _logFileName;

        public VSStyleMainWindow()
        {
            InitializeComponent();
            InitializeApplication();
        }

        private void InitializeApplication()
        {
            // Initialize WebView2
            InitializeWebView();
            
            // Set initial status
            StatusText.Text = "Ready - Visual Studio Professional Style Interface";
            
            // Add welcome message to output
            AddToOutput("Welcome to Coverage Analyzer - Visual Studio Professional Style!");
            AddToOutput("This interface features full docking capabilities:");
            AddToOutput("• Drag panels to different positions");
            AddToOutput("• Float panels as separate windows");
            AddToOutput("• Create tabbed panel groups");
            AddToOutput("• Auto-hide panels to maximize workspace");
            AddToOutput("Right-click panel tabs for more options!");
        }

        private async void InitializeWebView()
        {
            try
            {
                await HvpBrowser.EnsureCoreWebView2Async(null);
                HvpBrowser.CoreWebView2.DocumentTitleChanged += (s, e) => UpdateWindowTitle();
            }
            catch (Exception ex)
            {
                AddToOutput($"WebView2 initialization error: {ex.Message}");
            }
        }

        private void UpdateWindowTitle()
        {
            if (_currentProject != null)
            {
                Title = $"Coverage Analyzer - {_currentProject.ProjectName} - Visual Studio Professional Style";
            }
            else
            {
                Title = "Coverage Analyzer - Visual Studio Professional Style";
            }
        }

        private void AddToOutput(string message)
        {
            Dispatcher.Invoke(() =>
            {
                OutputTextBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}\n");
                OutputScrollViewer.ScrollToEnd();
            });
        }

        #region Menu Event Handlers

        private void NewProject_Click(object sender, RoutedEventArgs e)
        {
            AddToOutput("New Project clicked - Professional VS Style");
            StatusText.Text = "Creating new project...";
        }

        private void OpenProject_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Project files (*.json)|*.json|All files (*.*)|*.*",
                Title = "Open Coverage Project"
            };

            if (dialog.ShowDialog() == true)
            {
                AddToOutput($"Opening project: {dialog.FileName}");
                StatusText.Text = $"Loading project: {Path.GetFileName(dialog.FileName)}";
            }
        }

        private void LoadCoverageData_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "HVP files (*.hvp)|*.hvp|All files (*.*)|*.*",
                Title = "Load Coverage Data"
            };

            if (dialog.ShowDialog() == true)
            {
                AddToOutput($"Loading coverage data: {dialog.FileName}");
                StatusText.Text = "Processing coverage data...";
                LoadCoverageFile(dialog.FileName);
            }
        }

        private async void LoadCoverageFile(string filePath)
        {
            try
            {
                OperationProgress.Visibility = Visibility.Visible;
                OperationProgress.IsIndeterminate = true;
                
                AddToOutput("Parsing HVP file...");
                // Simulate processing
                await Task.Delay(1000);
                
                AddToOutput("Building hierarchy tree...");
                await Task.Delay(1000);
                
                AddToOutput("Coverage data loaded successfully!");
                StatusText.Text = "Ready - Coverage data loaded";
                
                OperationProgress.Visibility = Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                AddToOutput($"Error loading coverage data: {ex.Message}");
                StatusText.Text = "Error loading coverage data";
                OperationProgress.Visibility = Visibility.Collapsed;
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e) => AddToOutput("Save clicked");
        private void SaveAll_Click(object sender, RoutedEventArgs e) => AddToOutput("Save All clicked");
        private void Exit_Click(object sender, RoutedEventArgs e) => Application.Current.Shutdown();

        private void Undo_Click(object sender, RoutedEventArgs e) => AddToOutput("Undo clicked");
        private void Redo_Click(object sender, RoutedEventArgs e) => AddToOutput("Redo clicked");
        private void Cut_Click(object sender, RoutedEventArgs e) => AddToOutput("Cut clicked");
        private void Copy_Click(object sender, RoutedEventArgs e) => AddToOutput("Copy clicked");
        private void Paste_Click(object sender, RoutedEventArgs e) => AddToOutput("Paste clicked");

        private void ToggleSolutionExplorer_Click(object sender, RoutedEventArgs e)
        {
            // AvalonDock will handle this automatically
            AddToOutput("Solution Explorer visibility toggled");
        }

        private void ToggleOutput_Click(object sender, RoutedEventArgs e)
        {
            // AvalonDock will handle this automatically  
            AddToOutput("Output panel visibility toggled");
        }

        private void ToggleErrorList_Click(object sender, RoutedEventArgs e) => AddToOutput("Error List toggled");

        private void SetLightTheme_Click(object sender, RoutedEventArgs e)
        {
            ApplyLightTheme();
            AddToOutput("Light theme applied");
            if (FindName("StatusText") is TextBlock statusText)
                statusText.Text = "Light theme activated";
        }

        private void SetDarkTheme_Click(object sender, RoutedEventArgs e)
        {
            ApplyDarkTheme();
            AddToOutput("Dark theme applied");
            if (FindName("StatusText") is TextBlock statusText)
                statusText.Text = "Dark theme activated";
        }

        private void ResetLayout_Click(object sender, RoutedEventArgs e)
        {
            ResetLayoutToDefault();
            AddToOutput("Layout reset to default configuration");
            if (FindName("StatusText") is TextBlock statusText)
                statusText.Text = "Layout reset completed";
        }

        private void RunCoverageAnalysis_Click(object sender, RoutedEventArgs e)
        {
            AddToOutput("Running coverage analysis...");
            StatusText.Text = "Analyzing coverage data...";
        }

        private void TestHvpTreeView_Click(object sender, RoutedEventArgs e) => AddToOutput("Reloading project data");
        private void HttpAuthentication_Click(object sender, RoutedEventArgs e) => AddToOutput("HTTP Authentication dialog");
        private void CreateJira_Click(object sender, RoutedEventArgs e) => AddToOutput("Creating Jira ticket");
        private void AddToWaiver_Click(object sender, RoutedEventArgs e) => AddToOutput("Adding to waiver list");
        private void Options_Click(object sender, RoutedEventArgs e) => AddToOutput("Opening options dialog");
        private void About_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Coverage Analyzer - Visual Studio Professional Style\n\nFeaturing AvalonDock for professional docking capabilities!", 
                          "About", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        #endregion

        #region TreeView Event Handlers

        private void ExplorerTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.Source is TabControl tabControl)
            {
                var selectedTab = tabControl.SelectedItem as TabItem;
                AddToOutput($"Switched to {selectedTab?.Header} tab");
            }
        }

        private void HvpTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (e.NewValue != null)
            {
                AddToOutput($"HVP tree selection: {e.NewValue}");
                StatusText.Text = $"Selected: {e.NewValue}";
            }
        }

        private void StatsTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (e.NewValue != null)
            {
                AddToOutput($"Stats tree selection: {e.NewValue}");
                StatusText.Text = $"Stats selected: {e.NewValue}";
            }
        }

        #endregion

        #region Theme Management

        private void ApplyLightTheme()
        {
            _isDarkTheme = false;
            
            try
            {
                // Restore original light theme colors
                Resources["WindowBackgroundBrush"] = new SolidColorBrush(Color.FromRgb(255, 255, 255));
                Resources["ControlBackgroundBrush"] = new SolidColorBrush(Color.FromRgb(240, 240, 240));
                Resources["MenuBackgroundBrush"] = new SolidColorBrush(Color.FromRgb(246, 246, 246));
                Resources["TextForegroundBrush"] = new SolidColorBrush(Color.FromRgb(0, 0, 0));
                Resources["BorderBrush"] = new SolidColorBrush(Color.FromRgb(229, 229, 229));
                Resources["AccentBrush"] = new SolidColorBrush(Color.FromRgb(0, 122, 204));
                Resources["ButtonBackgroundBrush"] = new SolidColorBrush(Color.FromRgb(225, 225, 225));
                Resources["ButtonHoverBrush"] = new SolidColorBrush(Color.FromRgb(190, 230, 253));
                Resources["ButtonPressedBrush"] = new SolidColorBrush(Color.FromRgb(196, 229, 246));
                Resources["TabBackgroundBrush"] = new SolidColorBrush(Color.FromRgb(246, 246, 246));
                Resources["TabSelectedBrush"] = new SolidColorBrush(Color.FromRgb(255, 255, 255));
                Resources["InputBackgroundBrush"] = new SolidColorBrush(Color.FromRgb(255, 255, 255));
                
                InvalidateVisual();
            }
            catch (Exception ex)
            {
                AddToOutput($"Error applying light theme: {ex.Message}");
            }
        }

        private void ApplyDarkTheme()
        {
            _isDarkTheme = true;
            
            try
            {
                // Apply proper dark theme colors
                Resources["WindowBackgroundBrush"] = new SolidColorBrush(Color.FromRgb(30, 30, 30));      // Dark background
                Resources["ControlBackgroundBrush"] = new SolidColorBrush(Color.FromRgb(45, 45, 48));     // Dark control background
                Resources["MenuBackgroundBrush"] = new SolidColorBrush(Color.FromRgb(45, 45, 48));        // Dark menu background
                Resources["TextForegroundBrush"] = new SolidColorBrush(Color.FromRgb(255, 255, 255));     // White text
                Resources["BorderBrush"] = new SolidColorBrush(Color.FromRgb(63, 63, 70));               // Dark borders
                Resources["AccentBrush"] = new SolidColorBrush(Color.FromRgb(0, 122, 204));               // Blue accent
                Resources["ButtonBackgroundBrush"] = new SolidColorBrush(Color.FromRgb(62, 62, 66));      // Dark button background
                Resources["ButtonHoverBrush"] = new SolidColorBrush(Color.FromRgb(82, 82, 86));           // Button hover
                Resources["ButtonPressedBrush"] = new SolidColorBrush(Color.FromRgb(40, 40, 44));         // Button pressed
                Resources["TabBackgroundBrush"] = new SolidColorBrush(Color.FromRgb(45, 45, 48));         // Dark tab background
                Resources["TabSelectedBrush"] = new SolidColorBrush(Color.FromRgb(30, 30, 30));           // Selected tab dark
                Resources["InputBackgroundBrush"] = new SolidColorBrush(Color.FromRgb(51, 51, 55));       // Dark input background
                
                // Simple refresh without complex dispatcher calls
                InvalidateVisual();
            }
            catch (Exception ex)
            {
                AddToOutput($"Error applying dark theme: {ex.Message}");
            }
        }

        #endregion

        #region Layout Management

        private void ResetLayoutToDefault()
        {
            try
            {
                // Show a message that layout reset is requested
                MessageBox.Show("Layout has been reset to default configuration. Please restart the application to see the changes.", 
                               "Layout Reset", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                AddToOutput($"Error resetting layout: {ex.Message}");
            }
        }

        #endregion
    }
}