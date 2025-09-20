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

namespace CoverageAnalyzerGUI;

/// <summary>
/// Represents a node in the coverage hierarchy tree
/// </summary>
public class HierarchyNode
{
    public string Name { get; set; }
    public string FullPath { get; set; }
    public double CoveragePercentage { get; set; }
    public int LinesCovered { get; set; }
    public int TotalLines { get; set; }
    public List<HierarchyNode> Children { get; set; } = new List<HierarchyNode>();
    public bool IsExpanded { get; set; } = false;
    
    public HierarchyNode(string name, string fullPath = "")
    {
        Name = name;
        FullPath = fullPath;
    }
    
    public void AddChild(HierarchyNode child)
    {
        Children.Add(child);
    }
    
    public override string ToString()
    {
        if (TotalLines > 0)
        {
            return $"{Name} ({CoveragePercentage:F1}%)";
        }
        return Name;
    }
}

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private ProjectSettings? _currentProject;
    
    // Project information for display in status bar
    public string ReleaseName { get; private set; } = string.Empty;
    public string CoverageType { get; private set; } = string.Empty;
    public string ReportName { get; private set; } = string.Empty;
    public string ReportType { get; private set; } = string.Empty;
    public string Changelist { get; private set; } = string.Empty;

    public MainWindow()
    {
        Console.WriteLine("=== MAINWINDOW CONSTRUCTOR ===");
        Console.WriteLine("MainWindow constructor called");
        
        InitializeComponent();
        Console.WriteLine("InitializeComponent completed");
        
        try
        {
            AddToOutput("Welcome to Coverage Analyzer GUI");
            AddToOutput("Ready to create or open a project");
            UpdateWindowTitle();
            
            // Ensure window is visible and activated
            this.Show();
            this.Activate();
            this.WindowState = WindowState.Normal;
            this.Topmost = true;
            this.Topmost = false; // Reset topmost after activation
            
            Console.WriteLine("MainWindow initialization completed successfully");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR in MainWindow constructor: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            MessageBox.Show($"Initialization error: {ex.Message}", "Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void LoadCoverageData_Click(object sender, RoutedEventArgs e)
    {
        StatusText.Text = "Loading coverage data...";
        AddToOutput("Load Coverage Data command executed.");
        
        try
        {
            // Paths to the data files
            string coverageHierarchyPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "hierarchy.txt");
            
            AddToOutput($"Loading coverage data from: {coverageHierarchyPath}");
            
            // Check if files exist
            if (!File.Exists(coverageHierarchyPath))
            {
                string error = $"ERROR: Coverage hierarchy.txt not found at {coverageHierarchyPath}";
                AddToOutput(error);
                StatusText.Text = "Error: Coverage hierarchy.txt not found";
                return;
            }
            
            // Read and parse the hierarchy.txt file
            LogToFile("Reading hierarchy.txt file...");
            
            var hierarchyData = ParseHierarchyFile(coverageHierarchyPath);
            LogToFile($"Parsed {hierarchyData.Count} hierarchy entries");
            
            // Build hierarchy tree from parsed data
            var rootHierarchy = BuildHierarchyFromParserData(hierarchyData);
            LogToFile($"Root hierarchy has {rootHierarchy.Children.Count} children");
            
            // Update UI on main thread
            Dispatcher.Invoke(() =>
            {
                // Clear existing items
                SolutionExplorer.Items.Clear();
                
                var rootItem = CreateTreeViewItemFromHierarchy(rootHierarchy);
                rootItem.IsExpanded = true;
                SolutionExplorer.Items.Add(rootItem);
                
                LogToFile("TreeView updated with hierarchy data");
            });
            
            LogToFile("✅ Hierarchy loaded successfully");
            AddToOutput("✓ Hierarchy loaded from hierarchy.txt");
            StatusText.Text = "Coverage data loaded successfully";
            AddToOutput("✓ Coverage data loading completed!");
        }
        catch (Exception ex)
        {
            string errorMsg = $"ERROR loading coverage data: {ex.Message}";
            AddToOutput($"✗ {errorMsg}");
            LogToFile($"Parser error: {ex.Message}\nStack trace: {ex.StackTrace}");
            StatusText.Text = "Error loading coverage data";
        }
    }

    /// <summary>
    /// Simple hierarchy entry structure
    /// </summary>
    public class HierarchyEntry
    {
        public string Name { get; set; }
        public string Path { get; set; }
        public double CoveragePercentage { get; set; }
        public int LinesCovered { get; set; }
        public int TotalLines { get; set; }

        public HierarchyEntry(string name, string path, double coverage, int linesCovered, int totalLines)
        {
            Name = name;
            Path = path;
            CoveragePercentage = coverage;
            LinesCovered = linesCovered;
            TotalLines = totalLines;
        }
    }

    /// <summary>
    /// Parses the hierarchy.txt file and returns hierarchy entries
    /// </summary>
    private List<HierarchyEntry> ParseHierarchyFile(string filePath)
    {
        var entries = new List<HierarchyEntry>();
        
        try
        {
            var lines = File.ReadAllLines(filePath);
            
            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                
                // Parse lines like: "  75.23  82.15  68.90  91.45  88.20  79.60  top"
                var parts = line.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 7)
                {
                    var moduleName = parts[parts.Length - 1];
                    var coveragePercent = double.Parse(parts[0]);
                    var lineCoverage = double.Parse(parts[1]);
                    var condCoverage = double.Parse(parts[2]);
                    var toggleCoverage = double.Parse(parts[3]);
                    var fsmCoverage = double.Parse(parts[4]);
                    var branchCoverage = double.Parse(parts[5]);
                    
                    // Calculate indentation level to determine hierarchy
                    var indentLevel = line.Length - line.TrimStart().Length;
                    var path = GetPathFromIndentation(moduleName, indentLevel, entries);
                    
                    entries.Add(new HierarchyEntry(moduleName, path, coveragePercent, (int)(lineCoverage * 1000), 10000));
                }
            }
        }
        catch (Exception ex)
        {
            LogToFile($"Error parsing hierarchy file: {ex.Message}");
        }
        
        return entries;
    }

    /// <summary>
    /// Builds hierarchy from parsed data
    /// </summary>
    private HierarchyNode BuildHierarchyFromParserData(List<HierarchyEntry> hierarchyData)
    {
        LogToFile("Building hierarchy from parsed data...");
        
        // Create root node
        var rootNode = new HierarchyNode("Coverage Data", "");
        
        // Group entries by their hierarchical path
        var hierarchyMap = new Dictionary<string, HierarchyNode>();
        hierarchyMap[""] = rootNode;
        
        foreach (var entry in hierarchyData)
        {
            try
            {
                // Create hierarchy node from parser entry
                var hierarchyNode = new HierarchyNode(entry.Name, entry.Path);

                // Set coverage data from parser entry
                hierarchyNode.CoveragePercentage = entry.CoveragePercentage;
                hierarchyNode.LinesCovered = entry.LinesCovered;
                hierarchyNode.TotalLines = entry.TotalLines;

                // Find parent node
                var parentPath = GetParentPath(entry.Path);
                if (hierarchyMap.ContainsKey(parentPath))
                {
                    hierarchyMap[parentPath].AddChild(hierarchyNode);
                    hierarchyMap[entry.Path] = hierarchyNode;

                    LogToFile($"Added hierarchy node: {entry.Name} (Path: {entry.Path})");
                }
                else
                {
                    // If parent not found, add to root
                    rootNode.AddChild(hierarchyNode);
                    hierarchyMap[entry.Path] = hierarchyNode;

                    LogToFile($"Added hierarchy node to root: {entry.Name} (Path: {entry.Path})");
                }
            }
            catch (Exception ex)
            {
                LogToFile($"Error processing hierarchy entry {entry.Name}: {ex.Message}");
            }
        }
        
        LogToFile($"✅ Hierarchy built with {rootNode.Children.Count} top-level nodes");
        return rootNode;
    }
    
    /// <summary>
    /// Extracts parent path from a hierarchical path
    /// </summary>
    private string GetParentPath(string path)
    {
        if (string.IsNullOrEmpty(path))
            return "";
            
        var lastSeparatorIndex = path.LastIndexOf('.');
        if (lastSeparatorIndex == -1)
            return "";
            
        return path.Substring(0, lastSeparatorIndex);
    }
    
    /// <summary>
    /// Builds hierarchical path based on indentation level
    /// </summary>
    private string GetPathFromIndentation(string moduleName, int indentLevel, List<HierarchyEntry> existingEntries)
    {
        if (indentLevel == 0)
            return moduleName;
            
        // Find the parent based on indentation level
        var parentIndent = indentLevel - 2; // Assuming 2 spaces per level
        var parentEntry = existingEntries.LastOrDefault(e => 
            existingEntries.IndexOf(e) < existingEntries.Count && 
            GetIndentLevel(e.Path) == parentIndent);
            
        if (parentEntry != null)
        {
            return $"{parentEntry.Path}.{moduleName}";
        }
        
        return moduleName;
    }
    
    /// <summary>
    /// Gets indentation level from path
    /// </summary>
    private int GetIndentLevel(string path)
    {
        if (string.IsNullOrEmpty(path))
            return 0;
            
        return path.Split('.').Length - 1;
    }
    
    /// <summary>
    /// Creates a TreeViewItem from a HierarchyNode recursively
    /// </summary>
    private TreeViewItem CreateTreeViewItemFromHierarchy(HierarchyNode node)
    {
        var treeItem = new TreeViewItem
        {
            Header = node.ToString(),
            IsExpanded = node.IsExpanded
        };
        
        // Add coverage details for modules with coverage data
        if (node.TotalLines > 0)
        {
            treeItem.Items.Add(new TreeViewItem { Header = $"Lines Covered: {node.LinesCovered}/{node.TotalLines}" });
            treeItem.Items.Add(new TreeViewItem { Header = $"Coverage: {node.CoveragePercentage:F2}%" });
        }
        
        // Recursively add children
        foreach (var child in node.Children)
        {
            treeItem.Items.Add(CreateTreeViewItemFromHierarchy(child));
        }
        
        return treeItem;
    }

    public void AddToOutput(string message)
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss");
        var logMessage = $"[{timestamp}] {message}";
        OutputTextBox.Text += $"\n{logMessage}";
        OutputTextBox.ScrollToEnd();
        
        // Also log to file
        LogToFile(logMessage);
    }

    /// <summary>
    /// Updates the project information and refreshes the status bar display
    /// </summary>
    public void SetProjectInformation(string releaseName, string coverageType, string reportName, string reportType, string changelist)
    {
        ReleaseName = releaseName ?? string.Empty;
        CoverageType = coverageType ?? string.Empty;
        ReportName = reportName ?? string.Empty;
        ReportType = reportType ?? string.Empty;
        Changelist = changelist ?? string.Empty;
        
        UpdateProjectStatusBar();
        AddToOutput($"Project info updated: {ReleaseName} | {CoverageType} | {ReportName} | {ReportType} | {Changelist}");
    }

    /// <summary>
    /// Updates the status bar to display current project information
    /// </summary>
    private void UpdateProjectStatusBar()
    {
        if (string.IsNullOrEmpty(ReleaseName) || string.IsNullOrEmpty(ReportName))
        {
            ProjectInfoText.Text = "No project loaded";
        }
        else
        {
            var displayCoverage = CoverageType switch
            {
                "func_cov" => "Functional",
                "code_cov" => "Code",
                _ => CoverageType
            };
            
            var displayReportType = ReportType switch
            {
                "individual" => "Individual",
                "accumulate" => "Accumulate",
                _ => ReportType
            };
            
            ProjectInfoText.Text = $"Release: {ReleaseName} | Coverage: {displayCoverage} | Report: {ReportName} | Type: {displayReportType} | CL: {Changelist}";
        }
    }
    
    private void LogToFile(string message)
    {
        try
        {
            var logFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "CoverageAnalyzerDebug.log");
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            var logEntry = $"[{timestamp}] {message}";
            File.AppendAllText(logFilePath, logEntry + Environment.NewLine);
        }
        catch (Exception ex)
        {
            // If logging fails, don't crash the app
            System.Diagnostics.Debug.WriteLine($"Failed to log to file: {ex.Message}");
        }
    }

    // Project management methods
    private void NewProject_Click(object sender, RoutedEventArgs e)
    {
        var wizard = new ProjectWizard(this)
        {
            Owner = this
        };

        if (wizard.ShowDialog() == true && wizard.CompletedProject != null)
        {
            _currentProject = wizard.CompletedProject;
            UpdateWindowTitle();
            AddToOutput($"New project created: {_currentProject.ProjectName}");
            AddToOutput($"Project folder: {_currentProject.ProjectFolderPath}");
            
            // Set project information in status bar
            if (_currentProject.SelectedRelease != null && _currentProject.SelectedReport != null)
            {
                SetProjectInformation(
                    _currentProject.SelectedRelease.Name,
                    _currentProject.GetCoverageTypeString(),
                    _currentProject.SelectedReport.Name,
                    _currentProject.GetReportTypeString(),
                    _currentProject.SelectedChangelist
                );
            }
            
            // Load the project data
            LoadProjectData();
        }
    }

    private void OpenProject_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog()
        {
            Title = "Select project folder containing project.json",
            Multiselect = false
        };

        if (dialog.ShowDialog() == true)
        {
            var projectSettings = ProjectSettings.Load(dialog.FolderName);
            if (projectSettings != null)
            {
                _currentProject = projectSettings;
                UpdateWindowTitle();
                AddToOutput($"Opened project: {_currentProject.ProjectName}");
                AddToOutput($"Project folder: {_currentProject.ProjectFolderPath}");
                
                // Set project information in status bar - exactly like ProjectWizard does
                if (_currentProject.SelectedRelease != null && _currentProject.SelectedReport != null)
                {
                    SetProjectInformation(
                        _currentProject.SelectedRelease.Name,
                        _currentProject.GetCoverageTypeString(),
                        _currentProject.SelectedReport.Name,
                        _currentProject.GetReportTypeString(),
                        _currentProject.SelectedChangelist
                    );
                }
                
                // Load the project data - exactly like ProjectWizard does
                LoadProjectData();
            }
            else
            {
                MessageBox.Show("No valid project found in the selected folder.", "Open Project", 
                              MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (_currentProject != null)
        {
            _currentProject.Save();
            AddToOutput("Project settings saved");
        }
        else
        {
            AddToOutput("No project open to save");
        }
    }

    private void SaveAll_Click(object sender, RoutedEventArgs e) => Save_Click(sender, e);

    private void LoadProjectData()
    {
        if (_currentProject == null) return;

        try
        {
            var dataPath = _currentProject.LocalDataPath;
            if (Directory.Exists(dataPath))
            {
                var txtFiles = Directory.GetFiles(dataPath, "*.txt");
                AddToOutput($"Found {txtFiles.Length} data files in project");

                // Look for hierarchy.txt specifically
                var hierarchyFile = Path.Combine(dataPath, "hierarchy.txt");
                if (File.Exists(hierarchyFile))
                {
                    LoadHierarchyFromProject(hierarchyFile);
                }
            }
            else
            {
                AddToOutput("Project data folder not found - you may need to re-run the project wizard");
            }
        }
        catch (Exception ex)
        {
            AddToOutput($"Error loading project data: {ex.Message}");
        }
    }

    private void LoadHierarchyFromProject(string hierarchyFilePath)
    {
        try
        {
            AddToOutput($"Loading hierarchy from project: {hierarchyFilePath}");
            
            var hierarchyData = ParseHierarchyFile(hierarchyFilePath);
            var rootHierarchy = BuildHierarchyFromParserData(hierarchyData);
            
            // Update UI
            SolutionExplorer.Items.Clear();
            var rootItem = CreateTreeViewItemFromHierarchy(rootHierarchy);
            rootItem.IsExpanded = true;
            SolutionExplorer.Items.Add(rootItem);
            
            StatusText.Text = $"Loaded project: {_currentProject?.ProjectName}";
            AddToOutput("✓ Project hierarchy loaded successfully");
        }
        catch (Exception ex)
        {
            AddToOutput($"Error loading hierarchy: {ex.Message}");
        }
    }

    private void UpdateWindowTitle()
    {
        if (_currentProject != null)
        {
            Title = $"Coverage Analyzer - {_currentProject.ProjectName}";
        }
        else
        {
            Title = "Coverage Analyzer - No Project";
        }
    }

    private void Exit_Click(object sender, RoutedEventArgs e) => Application.Current.Shutdown();
    private void Undo_Click(object sender, RoutedEventArgs e) => AddToOutput("Undo clicked");
    private void Redo_Click(object sender, RoutedEventArgs e) => AddToOutput("Redo clicked");
    private void Cut_Click(object sender, RoutedEventArgs e) => AddToOutput("Cut clicked");
    private void Copy_Click(object sender, RoutedEventArgs e) => AddToOutput("Copy clicked");
    private void Paste_Click(object sender, RoutedEventArgs e) => AddToOutput("Paste clicked");
    private void ToggleSolutionExplorer_Click(object sender, RoutedEventArgs e) => AddToOutput("Toggle Solution Explorer clicked");
    private void ToggleOutput_Click(object sender, RoutedEventArgs e) => AddToOutput("Toggle Output clicked");
    private void ToggleErrorList_Click(object sender, RoutedEventArgs e) => AddToOutput("Toggle Error List clicked");
    private void SetLightTheme_Click(object sender, RoutedEventArgs e) => AddToOutput("Light theme selected");
    private void SetDarkTheme_Click(object sender, RoutedEventArgs e) => AddToOutput("Dark theme selected");
    private void RunCoverageAnalysis_Click(object sender, RoutedEventArgs e) => AddToOutput("Run Coverage Analysis clicked");
    private void Options_Click(object sender, RoutedEventArgs e) => AddToOutput("Options clicked");
    private void About_Click(object sender, RoutedEventArgs e) => MessageBox.Show("Coverage Analyzer GUI\nVersion 1.0", "About");
}
