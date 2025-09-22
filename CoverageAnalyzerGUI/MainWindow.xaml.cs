using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Threading.Tasks;
using CoverageAnalyzerGUI.Models;
using Microsoft.Win32;
using HvpHtmlParser;

namespace CoverageAnalyzerGUI;

/// <summary>
/// Log severity levels for output filtering
/// </summary>
public enum LogSeverity
{
    DEBUG,
    INFO,
    WARNING,
    ERROR
}

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
    private HttpClient? _authenticatedHttpClient;
    
    // Project information for display in status bar
    public string ReleaseName { get; private set; } = string.Empty;
    public string CoverageType { get; private set; } = string.Empty;
    public string ReportName { get; private set; } = string.Empty;
    public string ReportType { get; private set; } = string.Empty;
    public string Changelist { get; private set; } = string.Empty;

    public MainWindow()
    {
        InitializeComponent();
        
        try
        {
            LogToFile("=== APPLICATION STARTUP ===");
            LogToFile($"MainWindow constructor started at {DateTime.Now}");
            
            AddToOutput("Welcome to Coverage Analyzer GUI");
            AddToOutput("Ready to create or open a project");
            UpdateWindowTitle();
            
            // Ensure window is visible and activated
            this.Show();
            this.Activate();
            this.WindowState = WindowState.Normal;
            this.Topmost = true;
            this.Topmost = false; // Reset topmost after activation
            
            LogToFile("MainWindow initialization completed successfully");
        }
        catch (Exception ex)
        {
            AddToOutput($"Initialization error: {ex.Message}", LogSeverity.ERROR);
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

    public void AddToOutput(string message, LogSeverity severity = LogSeverity.INFO)
    {
        // Always log to file first with detailed timestamp
        LogToFile($"OUTPUT-{severity}: {message}");

#if DEBUG
        // In debug builds, show all messages
        var showMessage = true;
#else
        // In release builds, filter out DEBUG messages
        var showMessage = severity != LogSeverity.DEBUG;
#endif

        if (showMessage)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            var severityPrefix = severity switch
            {
                LogSeverity.DEBUG => "[DBG]",
                LogSeverity.INFO => "[INFO]",
                LogSeverity.WARNING => "[WARN]",
                LogSeverity.ERROR => "[ERROR]",
                _ => "[INFO]"
            };
            var logMessage = $"[{timestamp}] {severityPrefix} {message}";
            
            // Ensure UI updates happen on UI thread
            if (Dispatcher.CheckAccess())
            {
                // Already on UI thread
                OutputTextBox.Text += $"\n{logMessage}";
                OutputTextBox.ScrollToEnd();
            }
            else
            {
                // Not on UI thread, dispatch to UI thread
                Dispatcher.Invoke(() => {
                    OutputTextBox.Text += $"\n{logMessage}";
                    OutputTextBox.ScrollToEnd();
                });
            }
        }
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
        
        // Display HvpTop if configured
        if (_currentProject != null && !string.IsNullOrEmpty(_currentProject.HvpTop))
        {
            AddToOutput($"HvpTop: {_currentProject.HvpTop}");
        }
        else
        {
            AddToOutput("HvpTop: Not configured");
        }
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
            
            // Also output the log file path on first write
            if (!File.Exists(logFilePath + ".info"))
            {
                File.WriteAllText(logFilePath + ".info", $"Debug log location: {logFilePath}\nCreated: {DateTime.Now}\n");
                System.Diagnostics.Debug.WriteLine($"Debug log created at: {logFilePath}");
            }
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
            
            // Auto-set HttpServerUrl based on HvpTop URL
            if (!string.IsNullOrEmpty(_currentProject.HvpTop) && 
                (_currentProject.HvpTop.StartsWith("http://") || _currentProject.HvpTop.StartsWith("https://")))
            {
                try
                {
                    var hvpUri = new Uri(_currentProject.HvpTop);
                    var serverUrl = $"{hvpUri.Scheme}://{hvpUri.Host}";
                    if (!hvpUri.IsDefaultPort)
                    {
                        serverUrl += $":{hvpUri.Port}";
                    }
                    
                    _currentProject.HttpServerUrl = serverUrl;
                    AddToOutput($"✓ Auto-set HttpServerUrl to: {serverUrl}");
                    
                    // Save the updated project settings
                    _currentProject.Save();
                }
                catch (Exception ex)
                {
                    AddToOutput($"⚠ Could not parse HvpTop URL to set HttpServerUrl: {ex.Message}");
                }
            }
            
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
                
                // Auto-set HttpServerUrl based on HvpTop URL if not already set
                if (string.IsNullOrEmpty(_currentProject.HttpServerUrl) && 
                    !string.IsNullOrEmpty(_currentProject.HvpTop) && 
                    (_currentProject.HvpTop.StartsWith("http://") || _currentProject.HvpTop.StartsWith("https://")))
                {
                    try
                    {
                        var hvpUri = new Uri(_currentProject.HvpTop);
                        var serverUrl = $"{hvpUri.Scheme}://{hvpUri.Host}";
                        if (!hvpUri.IsDefaultPort)
                        {
                            serverUrl += $":{hvpUri.Port}";
                        }
                        
                        _currentProject.HttpServerUrl = serverUrl;
                        AddToOutput($"✓ Auto-set HttpServerUrl to: {serverUrl}");
                        
                        // Save the updated project settings
                        _currentProject.Save();
                    }
                    catch (Exception ex)
                    {
                        AddToOutput($"⚠ Could not parse HvpTop URL to set HttpServerUrl: {ex.Message}");
                    }
                }
                
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
                
                // Load the project data - but don't auto-parse HVP
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

    private async void LoadProjectData()
    {
        if (_currentProject == null) return;
       
        // Check if we need HTTP authentication based on project URLs
        if (RequiresHttpAuthentication())
        {
            AddToOutput("Project requires HTTP authentication for HVP access");
        }
        else
        {
            AddToOutput("Project loaded successfully");
        }
        
        AddToOutput("✓ Project ready. Auto-loading HVP data...");
        
        // Automatically load HVP data
        await AutoLoadHvpData();
    }

    /// <summary>
    /// Checks if the current project has any URLs that require HTTP authentication
    /// </summary>
    private bool RequiresHttpAuthentication()
    {
        if (_currentProject == null) return false;

        // Check HvpTop URL
        if (!string.IsNullOrEmpty(_currentProject.HvpTop) && 
            (_currentProject.HvpTop.StartsWith("http://") || _currentProject.HvpTop.StartsWith("https://")))
        {
            return true;
        }

        // Check HttpServerUrl
        if (!string.IsNullOrEmpty(_currentProject.HttpServerUrl))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Prompts for HTTP credentials and creates authentication object for the session
    /// </summary>
    private void PromptForHttpCredentials()
    {
        if (_authenticatedHttpClient != null)
        {
            AddToOutput("HTTP credentials already configured for this session");
            return;
        }

        AddToOutput($"  User should provide Credentials");
        AddToOutput("=== PromptForHttpCredentials START ===", LogSeverity.DEBUG);
        
        try
        {
            // Determine the server URL from the current project
            string serverUrl = "Unknown Server";
            if (_currentProject != null)
            {
                // Try to get server from HvpTop first
                if (!string.IsNullOrEmpty(_currentProject.HvpTop))
                {
                    if (Uri.TryCreate(_currentProject.HvpTop, UriKind.Absolute, out Uri? hvpUri))
                    {
                        serverUrl = $"{hvpUri.Scheme}://{hvpUri.Host}";
                        if (!hvpUri.IsDefaultPort)
                        {
                            serverUrl += $":{hvpUri.Port}";
                        }
                    }
                }
                // Fallback to HttpServerUrl if available
                else if (!string.IsNullOrEmpty(_currentProject.HttpServerUrl))
                {
                    serverUrl = _currentProject.HttpServerUrl;
                }
            }

            AddToOutput($"✓ server: {serverUrl}", LogSeverity.DEBUG);
            
            // Use HttpAuthDialog to get authenticated HttpClient - this works!
            AddToOutput($"✓ Started HTTP authentication for server: {serverUrl}");
            
            var (success, httpClient, rememberCredentials) = HttpAuthDialog.GetHttpAuthentication(this, serverUrl);
            
            // Already on UI thread, no need for Dispatcher.Invoke after dialog
            AddToOutput("✓ Authentication dialog closed", LogSeverity.DEBUG);
            
            AddToOutput($"✓ Authentication dialog returned: success={success}", LogSeverity.DEBUG);
            
            if (success && httpClient != null)
            {
                AddToOutput("Authentication successful", LogSeverity.DEBUG);
                
                // Store the authenticated HTTP client
                _authenticatedHttpClient?.Dispose();
                _authenticatedHttpClient = httpClient;
                
                AddToOutput($"✓ HTTP authentication configured for server: {serverUrl}");
                
                if (rememberCredentials)
                {
                    AddToOutput("✓ Credentials will be remembered for this session");
                }
            }
            else
            {
                AddToOutput("✓ HTTP authentication cancelled - some features may not work with protected resources");
            }
        }
        catch (Exception ex)
        {
            var errorMsg = $"Error configuring HTTP authentication: {ex.Message}";
            AddToOutput(errorMsg, LogSeverity.ERROR);
            AddToOutput($"EXCEPTION in PromptForHttpCredentials: {ex.Message}", LogSeverity.DEBUG);
            AddToOutput($"Stack trace: {ex.StackTrace}", LogSeverity.DEBUG);
        }
        finally
        {
            AddToOutput("=== PromptForHttpCredentials END ===", LogSeverity.DEBUG);
        }
        
        AddToOutput($"✓ PromptForHttpCredentials completed", LogSeverity.DEBUG);
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

    /// <summary>
    /// Sets the authenticated HTTP client for file access
    /// </summary>
    public void SetHttpAuthentication(HttpClient httpClient)
    {
        // Dispose existing client if any
        _authenticatedHttpClient?.Dispose();
        _authenticatedHttpClient = httpClient;
        AddToOutput("HTTP authentication configured for file access");
    }

    /// <summary>
    /// Gets the authenticated HTTP client for file access
    /// </summary>
    public HttpClient? GetHttpClient() => _authenticatedHttpClient;

    /// <summary>
    /// Clears the stored HTTP authentication
    /// </summary>
    public void ClearHttpAuthentication()
    {
        _authenticatedHttpClient?.Dispose();
        _authenticatedHttpClient = null;
        AddToOutput("HTTP authentication cleared");
    }

    /// <summary>
    /// Downloads a file using the stored HTTP authentication
    /// </summary>
    public async Task<byte[]?> DownloadFileAsync(string url)
    {
        if (_authenticatedHttpClient == null)
        {
            throw new InvalidOperationException("No HTTP authentication configured. Please authenticate first.");
        }

        try
        {
            AddToOutput($"Downloading file from: {url}");
            var response = await _authenticatedHttpClient.GetAsync(url);
            
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsByteArrayAsync();
                AddToOutput($"Successfully downloaded {content.Length} bytes");
                return content;
            }
            else
            {
                AddToOutput($"HTTP download failed: {response.StatusCode} - {response.ReasonPhrase}");
                return null;
            }
        }
        catch (Exception ex)
        {
            AddToOutput($"Error downloading file: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Downloads a file and saves it to a local path
    /// </summary>
    public async Task<bool> DownloadFileToPathAsync(string url, string localPath)
    {
        try
        {
            var content = await DownloadFileAsync(url);
            if (content != null)
            {
                await File.WriteAllBytesAsync(localPath, content);
                AddToOutput($"File saved to: {localPath}");
                return true;
            }
            return false;
        }
        catch (Exception ex)
        {
            AddToOutput($"Error saving file to {localPath}: {ex.Message}");
            return false;
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        // Clean up HTTP client
        _authenticatedHttpClient?.Dispose();
        base.OnClosed(e);
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

    /// <summary>
    /// Automatically load HVP data when project is created or opened
    /// </summary>
    private async Task AutoLoadHvpData()
    {
        AddToOutput("=== Auto-loading HVP TreeView ===");
        
        try
        {
            if (_currentProject == null)
            {
                AddToOutput("⚠ No project loaded.", LogSeverity.WARNING);
                return;
            }
            
            if (string.IsNullOrEmpty(_currentProject.HvpTop))
            {
                AddToOutput("⚠ No HVP file path configured. Skipping auto-load.", LogSeverity.WARNING);
                return;
            }

            AddToOutput($"Auto-loading HVP file: {_currentProject.HvpTop}");
            
            // Set authentication if needed for HTTP/HTTPS URLs
            if (_currentProject.HvpTop.StartsWith("http://") || _currentProject.HvpTop.StartsWith("https://"))
            {
                if (_authenticatedHttpClient != null)
                {
                    AddToOutput($"✓ Authentication already configured for this session");
                }
                else
                {
                    AddToOutput("⚠ No HTTP authentication configured for this session.", LogSeverity.WARNING);
                    AddToOutput("  Prompting for credentials...");
                    
                    // Prompt for credentials
                    PromptForHttpCredentials();
                    
                    if (_authenticatedHttpClient != null)
                    {
                        AddToOutput($"✓ HTTP authentication configured", LogSeverity.DEBUG);
                    }
                    else
                    {
                        AddToOutput("  No credentials provided - skipping auto-load.");
                        AddToOutput("⚠ Use 'Test HVP TreeView' menu to load data manually", LogSeverity.WARNING);
                        return;
                    }
                }
            }
            
            AddToOutput("✓ Starting HVP ParseFile operation...", LogSeverity.DEBUG);
            
            StatusText.Text = "Loading HVP data...";
            
            // Capture authentication credentials for background thread
            string? authCredentials = null;
            if (_authenticatedHttpClient != null && 
                _authenticatedHttpClient.DefaultRequestHeaders.Authorization != null)
            {
                authCredentials = _authenticatedHttpClient.DefaultRequestHeaders.Authorization.Parameter;
                AddToOutput("✓ Captured authentication credentials for background thread", LogSeverity.DEBUG);
            }
            
            var startTime = DateTime.Now;
            AddToOutput("⏱️ Starting ParseFile operation...", LogSeverity.INFO);
            
            var result = await Task.Run(async () => {
                // Create fresh instances for background thread
                var backgroundReader = new HtmlReader();
                
                // Set up authentication if we have credentials
                if (authCredentials != null)
                {
                    var backgroundHandler = new HttpClientHandler();
                    var backgroundHttpClient = new HttpClient(backgroundHandler);
                    backgroundHttpClient.DefaultRequestHeaders.Authorization = 
                        new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", authCredentials);
                    backgroundReader.SetHttpClient(backgroundHttpClient);
                }
                
                return await backgroundReader.ParseFile(_currentProject.HvpTop);
            });
            
            var duration = DateTime.Now - startTime;
            AddToOutput($"🎉 ParseFile completed successfully in {duration.TotalSeconds:F1} seconds!", LogSeverity.INFO);
            
            StatusText.Text = "Processing results...";
            
            if (result != null)
            {
                AddToOutput($"✓ ParseFile returned: {result.GetType().Name}", LogSeverity.DEBUG);
                
                // Convert to tree items and display
                try 
                {
                    var treeItems = ConvertHvpNodeToTreeItems(result);
                    
                    if (treeItems?.Count > 0)
                    {
                        // Ensure UI update happens on UI thread
                        if (Dispatcher.CheckAccess())
                        {
                            SolutionExplorer.Items.Clear();
                            SolutionExplorer.ItemsSource = treeItems;
                            AddToOutput($"✓ Auto-loaded {treeItems.Count} items in TreeView");
                        }
                        else
                        {
                            Dispatcher.Invoke(() => {
                                SolutionExplorer.Items.Clear();
                                SolutionExplorer.ItemsSource = treeItems;
                                AddToOutput($"✓ Auto-loaded {treeItems.Count} items in TreeView");
                            });
                        }
                        
                        StatusText.Text = "HVP data loaded successfully";
                    }
                    else
                    {
                        AddToOutput("⚠ No items found to display", LogSeverity.WARNING);
                        StatusText.Text = "No HVP data found";
                    }
                }
                catch (Exception uiEx)
                {
                    AddToOutput($"❌ UI update error: {uiEx.Message}", LogSeverity.ERROR);
                    StatusText.Text = "Error displaying HVP data";
                }
            }
            else
            {
                AddToOutput("❌ ParseFile returned null", LogSeverity.ERROR);
                StatusText.Text = "Failed to parse HVP data";
            }
        }
        catch (Exception ex)
        {
            AddToOutput($"❌ Auto-load HVP error: {ex.Message}", LogSeverity.ERROR);
            StatusText.Text = "Error loading HVP data";
        }
    }

    /// <summary>
    /// Reload Project functionality using HvpHtmlParser
    /// </summary>
    private async void TestHvpTreeView_Click(object sender, RoutedEventArgs e)
    {
        AddToOutput("=== Testing HVP TreeView ===");
        
        // Disable menu during operation to prevent multiple simultaneous calls
        if (sender is MenuItem menuItem)
        {
            menuItem.IsEnabled = false;
        }
        
        // Update status
        StatusText.Text = "Loading HVP data...";
        
        try
        {
            if (_currentProject == null)
            {
                AddToOutput("⚠ No project loaded. Please create or open a project first.", LogSeverity.WARNING);
                return;
            }
            
            if (string.IsNullOrEmpty(_currentProject.HvpTop))
            {
                AddToOutput("⚠ No HVP file path configured. Please set HvpTop in project settings.", LogSeverity.WARNING);
                return;
            }
            

            AddToOutput($"Loading HVP file: {_currentProject.HvpTop}");
            
            // We'll create the HtmlReader inside the background thread to avoid threading issues
            AddToOutput("✓ Preparing for background ParseFile operation", LogSeverity.DEBUG);
            
            // Set authentication if needed for HTTP/HTTPS URLs
            if (_currentProject.HvpTop.StartsWith("http://") || _currentProject.HvpTop.StartsWith("https://"))
            {
                if (_authenticatedHttpClient != null)
                {
                    AddToOutput($"✓ Authentication already configured for this session");
                }
                else
                {
                    AddToOutput("⚠ No HTTP authentication configured for this session.", LogSeverity.WARNING);
                    AddToOutput("  Prompting for credentials now...");
                    
                    // Prompt for credentials right now
                    PromptForHttpCredentials();
                    
                    if (_authenticatedHttpClient != null)
                    {
                        AddToOutput($"✓ HTTP authentication configured", LogSeverity.DEBUG);
                        AddToOutput("✓ HttpClient configured, proceeding to ParseFile", LogSeverity.DEBUG);
                    }
                    else
                    {
                        AddToOutput("  No credentials provided - continuing without authentication.");
                        AddToOutput("⚠ WARNING: ParseFile will likely fail for protected resources", LogSeverity.WARNING);
                    }
                }
            }
            
            AddToOutput("✓ About to call HtmlReader.ParseFile...", LogSeverity.DEBUG);
            
            // Minimal diagnostics - avoid any potential hanging operations
            AddToOutput($"🌐 Target URL: {_currentProject.HvpTop}", LogSeverity.DEBUG);
            
            StatusText.Text = "Connecting to server... (may take up to 2 minutes)";
            
            try
            {
                AddToOutput("🚀 CALLING ParseFile NOW...", LogSeverity.DEBUG);
                
                // Capture authentication credentials for background thread (simplified approach)
                string? authCredentials = null;
                if (_authenticatedHttpClient != null && 
                    _authenticatedHttpClient.DefaultRequestHeaders.Authorization != null)
                {
                    authCredentials = _authenticatedHttpClient.DefaultRequestHeaders.Authorization.Parameter;
                    AddToOutput("✓ Captured authentication credentials for background thread", LogSeverity.DEBUG);
                }
                else
                {
                    AddToOutput("⚠ No authentication available - proceeding without credentials", LogSeverity.DEBUG);
                }
                
                // Use ConfigureAwait(false) to avoid UI context issues
                var startTime = DateTime.Now;
                AddToOutput("⏱️ Starting ParseFile operation...", LogSeverity.INFO);
                AddToOutput("🚀 Launching background thread for ParseFile...", LogSeverity.DEBUG);
                
                var result = await Task.Run(async () => {
                    // Create fresh instances for background thread
                    var backgroundReader = new HtmlReader();
                    
                    // Set up authentication if we have credentials
                    if (authCredentials != null)
                    {
                        var backgroundHandler = new HttpClientHandler();
                        var backgroundHttpClient = new HttpClient(backgroundHandler);
                        backgroundHttpClient.DefaultRequestHeaders.Authorization = 
                            new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", authCredentials);
                        backgroundReader.SetHttpClient(backgroundHttpClient);
                    }
                    
                    return await backgroundReader.ParseFile(_currentProject.HvpTop);
                }); // Remove ConfigureAwait(false) to stay on UI thread
                
                AddToOutput("🔄 Background thread completed successfully!", LogSeverity.DEBUG);
                
                var duration = DateTime.Now - startTime;
                AddToOutput($"🎉 ParseFile call COMPLETED successfully in {duration.TotalSeconds:F1} seconds!", LogSeverity.DEBUG);
                
                // Reset status immediately after successful parse
                StatusText.Text = "Processing results...";
                
                AddToOutput($"✓ ParseFile completed successfully: {result?.GetType().Name ?? "null"}", LogSeverity.DEBUG);
                
                if (result != null)
                {
                    AddToOutput($"✓ ParseFile returned: {result.GetType().Name}", LogSeverity.DEBUG);
                    
                    // Convert to tree items and display - we should be on UI thread after await
                    try 
                    {
                        var treeItems = ConvertHvpNodeToTreeItems(result);
                        
                        if (treeItems?.Count > 0)
                        {
                            // Ensure UI update happens on UI thread
                            if (Dispatcher.CheckAccess())
                            {
                                // Clear existing items before setting ItemsSource
                                SolutionExplorer.Items.Clear();
                                SolutionExplorer.ItemsSource = treeItems;
                                AddToOutput($"✓ Displayed {treeItems.Count} items in TreeView");
                            }
                            else
                            {
                                Dispatcher.Invoke(() => {
                                    // Clear existing items before setting ItemsSource
                                    SolutionExplorer.Items.Clear();
                                    SolutionExplorer.ItemsSource = treeItems;
                                    AddToOutput($"✓ Displayed {treeItems.Count} items in TreeView");
                                });
                            }
                        }
                        else
                        {
                            AddToOutput("⚠ No items found to display", LogSeverity.WARNING);
                        }
                    }
                    catch (Exception uiEx)
                    {
                        AddToOutput($"❌ UI update error: {uiEx.Message}", LogSeverity.ERROR);
                    }
                }
                else
                {
                    AddToOutput("⚠ ParseFile returned null", LogSeverity.WARNING);
                }
            }
            catch (Exception parseEx)
            {
                AddToOutput($"ParseFile failed: {parseEx.Message}", LogSeverity.ERROR);
                if (parseEx.InnerException != null)
                {
                    AddToOutput($"Inner exception: {parseEx.InnerException.Message}", LogSeverity.ERROR);
                }
                
                // Provide comprehensive guidance based on error type
                if (parseEx.Message.Contains("Failed to download content from URL") || 
                    parseEx.Message.Contains("download") ||
                    parseEx.Message.Contains("network") ||
                    parseEx.Message.Contains("connection"))
                {
                    AddToOutput("", LogSeverity.INFO);
                    AddToOutput("🔧 NETWORK TROUBLESHOOTING GUIDE:", LogSeverity.INFO);
                    AddToOutput("1. 🛡️ VERIFY VPN CONNECTION (Critical for AMD servers):", LogSeverity.INFO);
                    AddToOutput("   - Connect to AMD VPN if not already connected", LogSeverity.INFO);
                    AddToOutput("   - Verify VPN status in system tray", LogSeverity.INFO);
                    AddToOutput("   - Try disconnecting and reconnecting VPN", LogSeverity.INFO);
                    AddToOutput("", LogSeverity.INFO);
                    AddToOutput("2. 🌐 TEST URL ACCESSIBILITY:", LogSeverity.INFO);
                    AddToOutput($"   - Open this URL in your browser: {_currentProject.HvpTop}", LogSeverity.INFO);
                    AddToOutput("   - Verify you can access the file manually", LogSeverity.INFO);
                    AddToOutput("", LogSeverity.INFO);
                    AddToOutput("3. 🔐 CHECK AUTHENTICATION:", LogSeverity.INFO);
                    AddToOutput("   - Verify your AMD credentials are correct", LogSeverity.INFO);
                    AddToOutput("   - Try re-entering credentials in this app", LogSeverity.INFO);
                    AddToOutput("", LogSeverity.INFO);
                    AddToOutput("4. 📁 VERIFY FILE LOCATION:", LogSeverity.INFO);
                    AddToOutput("   - Check if the file still exists at that path", LogSeverity.INFO);
                    AddToOutput("   - Contact the person who provided the URL", LogSeverity.INFO);
                    
                    // Specific guidance for logviewer-atl.amd.com
                    if (_currentProject?.HvpTop?.Contains("logviewer-atl.amd.com") == true)
                    {
                        AddToOutput("", LogSeverity.INFO);
                        AddToOutput("🏢 SPECIFIC to logviewer-atl.amd.com:", LogSeverity.INFO);
                        AddToOutput("   - This is an AMD internal server requiring VPN", LogSeverity.INFO);
                        AddToOutput("   - Must be connected to AMD corporate network or VPN", LogSeverity.INFO);
                        AddToOutput("   - Server may have restricted access hours", LogSeverity.INFO);
                    }
                }
                else if (parseEx.Message.Contains("timeout"))
                {
                    AddToOutput("", LogSeverity.INFO);
                    AddToOutput("⏱️ TIMEOUT TROUBLESHOOTING:", LogSeverity.INFO);
                    AddToOutput("1. File may be very large - try again later", LogSeverity.INFO);
                    AddToOutput("2. Network connection is slow", LogSeverity.INFO);
                    AddToOutput("3. VPN connection may be unstable", LogSeverity.INFO);
                }
                else if (parseEx.Message.Contains("unauthorized") || parseEx.Message.Contains("401"))
                {
                    AddToOutput("", LogSeverity.INFO);
                    AddToOutput("🔒 AUTHENTICATION TROUBLESHOOTING:", LogSeverity.INFO);
                    AddToOutput("1. Re-enter your AMD credentials", LogSeverity.INFO);
                    AddToOutput("2. Check if your account has access to this resource", LogSeverity.INFO);
                    AddToOutput("3. Contact IT if credentials are not working", LogSeverity.INFO);
                }
                
                throw; // Re-throw to hit the outer catch blocks
            }
        }
        catch (OperationCanceledException)
        {
            AddToOutput("❌ TIMEOUT: ParseFile operation timed out after 2 minutes", LogSeverity.ERROR);
            AddToOutput("📊 Console test worked faster - this suggests a difference in thread context", LogSeverity.INFO);
            AddToOutput("💡 The ParseFile call is working but slower in GUI context", LogSeverity.INFO);
        }
        catch (UnauthorizedAccessException ex)
        {
            AddToOutput($"AUTHENTICATION ERROR: {ex.Message}", LogSeverity.ERROR);
            AddToOutput("This usually means the username/password is incorrect or the account doesn't have access.", LogSeverity.WARNING);
            AddToOutput("Please verify your credentials and try again.", LogSeverity.WARNING);
        }
        catch (HttpRequestException ex)
        {
            AddToOutput($"NETWORK ERROR: {ex.Message}", LogSeverity.ERROR);
            AddToOutput("This could be due to:", LogSeverity.WARNING);
            AddToOutput("- VPN connection required but not active", LogSeverity.WARNING);
            AddToOutput("- Network connectivity issues", LogSeverity.WARNING);
            AddToOutput("- Server temporarily unavailable", LogSeverity.WARNING);
            AddToOutput("- SSL/TLS certificate issues", LogSeverity.WARNING);
            
            // Specific suggestions for logviewer-atl.amd.com
            if (_currentProject?.HvpTop?.Contains("logviewer-atl.amd.com") == true)
            {
                AddToOutput("", LogSeverity.INFO);
                AddToOutput("💡 SPECIFIC SUGGESTIONS for logviewer-atl.amd.com:", LogSeverity.INFO);
                AddToOutput("1. Connect to AMD VPN if not already connected", LogSeverity.INFO);
                AddToOutput("2. Try accessing the URL in your browser first", LogSeverity.INFO);
                AddToOutput("3. Verify you're on AMD network or VPN", LogSeverity.INFO);
                AddToOutput("4. Check if you need to refresh your authentication", LogSeverity.INFO);
            }
        }
        catch (Exception ex)
        {
            AddToOutput($"ERROR: Failed to parse file {_currentProject?.HvpTop ?? "unknown"}: {ex.Message}", LogSeverity.ERROR);
            if (ex.InnerException != null)
            {
                AddToOutput($"Inner exception: {ex.InnerException.Message}", LogSeverity.ERROR);
            }
            
            // Add debug information about authentication
            if (_authenticatedHttpClient != null)
            {
                AddToOutput($"Authentication was configured with HttpClient", LogSeverity.DEBUG);
            }
            else
            {
                AddToOutput("No authentication was set", LogSeverity.DEBUG);
            }
        }
        finally
        {
            // Re-enable menu and reset status
            if (sender is MenuItem senderMenuItem)
            {
                senderMenuItem.IsEnabled = true;
            }
            StatusText.Text = "Ready";
        }
    }
    
    // TestNetworkConnectivity method removed - was causing hangs
    // Use console test instead: dotnet run --project HvpParserTest

    /// <summary>
    /// Convert HvpNode object to TreeViewItem using simple hierarchical traversal
    /// </summary>
    private List<System.Windows.Controls.TreeViewItem>? ConvertHvpNodeToTreeItems(object hvpNode, int maxDepth = 3, int currentDepth = 0)
    {
        try
        {
            return new List<System.Windows.Controls.TreeViewItem> { CreateTreeViewItemFromHvpNode(hvpNode, isRoot: true) };
        }
        catch (Exception ex)
        {
            AddToOutput($"❌ Error converting HvpNode: {ex.Message}", LogSeverity.ERROR);
            return null;
        }
    }

    /// <summary>
    /// Create a TreeViewItem from an HvpNode with proper hierarchical structure
    /// </summary>
    private System.Windows.Controls.TreeViewItem CreateTreeViewItemFromHvpNode(object hvpNode, bool isRoot = false)
    {
        var treeItem = new System.Windows.Controls.TreeViewItem();
        var nodeType = hvpNode.GetType();
        
        // Get node properties
        var nameProperty = nodeType.GetProperty("Name");
        var scoreProperty = nodeType.GetProperty("Score");
        var childrenProperty = nodeType.GetProperty("Children");
        
        // Set the header with name and score
        string nodeName;
        if (isRoot)
        {
            // For root node: try to use HvpNode name first, fallback to ReportNameWithoutVerifPlan
            var hvpNodeName = nameProperty?.GetValue(hvpNode)?.ToString();
            AddToOutput($"🐛 ROOT NODE DEBUG: HvpNode name property value = '{hvpNodeName}'", LogSeverity.DEBUG);
            AddToOutput($"🐛 ROOT NODE DEBUG: ReportNameWithoutVerifPlan = '{_currentProject?.ReportNameWithoutVerifPlan}'", LogSeverity.DEBUG);
            
            if (!string.IsNullOrEmpty(hvpNodeName) && hvpNodeName != "Unknown")
            {
                // Use the HvpNode's actual name if it exists
                nodeName = hvpNodeName;
                AddToOutput($"✓ ROOT NODE: Using HvpNode name = '{nodeName}'", LogSeverity.DEBUG);
            }
            else if (_currentProject != null && !string.IsNullOrEmpty(_currentProject.ReportNameWithoutVerifPlan))
            {
                // Fallback to the stored report name for the root node
                nodeName = _currentProject.ReportNameWithoutVerifPlan;
                AddToOutput($"✓ ROOT NODE: Using ReportNameWithoutVerifPlan = '{nodeName}'", LogSeverity.DEBUG);
            }
            else
            {
                // Final fallback
                nodeName = "Coverage Report";
                AddToOutput($"✓ ROOT NODE: Using final fallback = '{nodeName}'", LogSeverity.DEBUG);
            }
        }
        else
        {
            // Use the actual node name for child nodes
            nodeName = nameProperty?.GetValue(hvpNode)?.ToString() ?? "Unknown";
        }
        
        var score = scoreProperty?.GetValue(hvpNode);
        
        if (score is double doubleScore && doubleScore > 0)
        {
            treeItem.Header = $"{nodeName} ({doubleScore:F1}%)";
            if (isRoot) AddToOutput($"✓ ROOT NODE FINAL HEADER: '{treeItem.Header}' (with score)", LogSeverity.DEBUG);
        }
        else
        {
            treeItem.Header = nodeName;
            if (isRoot) AddToOutput($"✓ ROOT NODE FINAL HEADER: '{treeItem.Header}' (no score)", LogSeverity.DEBUG);
        }
        
        // Add children recursively
        if (childrenProperty != null)
        {
            var children = childrenProperty.GetValue(hvpNode);
            if (children is System.Collections.IEnumerable enumerable)
            {
                foreach (var child in enumerable)
                {
                    if (child != null)
                    {
                        var childItem = CreateTreeViewItemFromHvpNode(child, isRoot: false);
                        treeItem.Items.Add(childItem);
                    }
                }
            }
        }
        
        return treeItem;
    }
}
