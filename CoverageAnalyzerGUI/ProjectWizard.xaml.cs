using CoverageAnalyzerGUI.Models;
using CoverageAnalyzerGUI.Utilities;
using Microsoft.Win32;
using Renci.SshNet;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace CoverageAnalyzerGUI;

/// <summary>
/// Interaction logic for ProjectWizard.xaml
/// </summary>
public partial class ProjectWizard : Window
{
    private ProjectSettings _projectSettings;
    private List<DatabaseRelease> _availableReleases = new();
    private List<DatabaseReport> _availableReports = new();
    private List<string> _availableChangelists = new();
    private bool _isDatabaseConnected = false;
    private MainWindow? _mainWindow;

    public ProjectSettings? CompletedProject { get; private set; }

    public ProjectWizard(MainWindow? mainWindow = null)
    {
        _mainWindow = mainWindow;
        InitializeComponent();
        _projectSettings = new ProjectSettings();
        
        // Initialize SSH host from the default value in the TextBox
        _projectSettings.SshHost = SshHostTextBox?.Text ?? string.Empty;
        
        UpdateUIState();
        
        // Auto-connect to database after window is loaded
        this.Loaded += ProjectWizard_Loaded;
    }

    private async void ProjectWizard_Loaded(object sender, RoutedEventArgs e)
    {
        // Auto-connect to database on startup
        await AutoConnectToDatabase();
    }

    private async Task AutoConnectToDatabase()
    {
        try
        {
            LogToOutput("=== DATABASE CONNECTION ATTEMPT ===");
            
            // Initialize database connection with timeout
            LogToOutput("Initializing database connection...");
            await Task.Run(() => 
            {
                var timeout = Task.Delay(10000); // 10 second timeout
                var initTask = Task.Run(() => DcPgConn.InitDb());
                var completedTask = Task.WaitAny(initTask, timeout);
                
                if (completedTask == 1) // timeout
                {
                    throw new TimeoutException("Database connection timed out after 10 seconds");
                }
            });
            
            LogToOutput("✅ Database connection initialized successfully");

            // Test connection by getting releases
            LogToOutput("Testing connection by retrieving releases...");
            _availableReleases = await Task.Run(() => GetAllReleases());
            LogToOutput($"Retrieved {_availableReleases.Count} releases from database");

            if (_availableReleases.Count == 0)
            {
                // Database connected but no releases found
                _isDatabaseConnected = true;
                LogToOutput("⚠️ Database connected but no releases found");
            }
            else
            {
                // Update UI with success status
                _isDatabaseConnected = true;
                LogToOutput($"Connected to database - {_availableReleases.Count} releases available");
            }

            // Populate releases dropdown
            ReleaseComboBox.ItemsSource = _availableReleases;
            ReleaseComboBox.DisplayMemberPath = "Name";

            UpdateUIState();
        }
        catch (TimeoutException ex)
        {
            LogToOutput($"❌ Database connection timeout: {ex.Message}");
            HandleDatabaseError("Database connection timed out. Please check your network connection and database server status.");
        }
        catch (Exception ex)
        {
            LogToOutput($"❌ Database connection failed: {ex.Message}");
            LogToOutput($"❌ Exception type: {ex.GetType().FullName}");
            if (ex.InnerException != null)
            {
                LogToOutput($"❌ Inner exception: {ex.InnerException.Message}");
            }
            HandleDatabaseError($"Database connection failed: {ex.Message}");
        }
    }

    private void HandleDatabaseError(string errorMessage)
    {
        // Update UI with error status
        _isDatabaseConnected = false;

        // Clear releases list and update UI
        _availableReleases = new List<DatabaseRelease>();
        ReleaseComboBox.ItemsSource = _availableReleases;

        // Add sample data for testing if in debug mode
        #if DEBUG
        LogToOutput("🔧 DEBUG MODE: Adding sample data for testing...");
        AddSampleDataForTesting();
        #endif

        UpdateUIState();
        LogToOutput("=== DATABASE CONNECTION FAILED ===");
    }

    #if DEBUG
    private void AddSampleDataForTesting()
    {
        try
        {
            var sampleReleases = new List<DatabaseRelease>
            {
                new DatabaseRelease { Id = 1, Name = "Sample Release 1.0", ProjectId = 1, CreatedAt = DateTime.Now.AddDays(-30) },
                new DatabaseRelease { Id = 2, Name = "Sample Release 1.1", ProjectId = 1, CreatedAt = DateTime.Now.AddDays(-15) },
                new DatabaseRelease { Id = 3, Name = "Sample Release 2.0", ProjectId = 1, CreatedAt = DateTime.Now.AddDays(-5) }
            };
            
            _availableReleases = sampleReleases;
            ReleaseComboBox.ItemsSource = _availableReleases;
            
            LogToOutput("Using sample data (DEBUG MODE)");
            
            LogToOutput("✓ Added sample releases for testing");
            UpdateUIState();
        }
        catch (Exception ex)
        {
            LogToOutput($"❌ Failed to add sample data: {ex.Message}");
        }
    }
    #endif

    private void BrowseFolderButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog()
        {
            Title = "Select folder for the new project",
            Multiselect = false
        };

        if (dialog.ShowDialog() == true)
        {
            var selectedPath = dialog.FolderName;
            
            // Store the base folder and set initial project folder with default name
            // The actual project name will be generated when all selections are made
            var projectFolder = Path.Combine(selectedPath, _projectSettings.ProjectName);
            
            ProjectFolderTextBox.Text = projectFolder;
            _projectSettings.ProjectFolderPath = projectFolder;
            
            // Log the path structure for clarity
            LogToOutput($"=== PROJECT PATH STRUCTURE ===");
            LogToOutput($"Selected base folder: {selectedPath}");
            LogToOutput($"Initial project name: {_projectSettings.ProjectName}");
            LogToOutput($"Project folder: {_projectSettings.ProjectFolderPath}");
            LogToOutput($"Data files will be saved to: {_projectSettings.LocalDataPath}");
            LogToOutput($"=== END PATH STRUCTURE ===");
            
            UpdateUIState();
        }
    }

    private void GenerateProjectName()
    {
        // Generate project name using the pattern: {projectName}_{releaseName}_{coverageType}_{changelist}
        if (_projectSettings.SelectedReport != null && 
            _projectSettings.SelectedRelease != null && 
            !string.IsNullOrEmpty(_projectSettings.SelectedChangelist))
        {
            var projectName = _projectSettings.SelectedReport.ProjectName ?? "Unknown";
            var releaseName = _projectSettings.SelectedRelease.Name ?? "Unknown";
            var coverageType = GetDisplayCoverageType(); // functional or code
            var changelist = _projectSettings.SelectedChangelist;
            
            // Clean names to be filesystem-safe
            projectName = CleanForFilename(projectName);
            releaseName = CleanForFilename(releaseName);
            coverageType = CleanForFilename(coverageType);
            changelist = CleanForFilename(changelist);
            
            _projectSettings.ProjectName = $"{projectName}_{releaseName}_{coverageType}_{changelist}";
            LogToOutput($"Auto-generated project name: {_projectSettings.ProjectName}");
        }
        else
        {
            _projectSettings.ProjectName = "CoverageProject";
            LogToOutput("Using default project name (not all fields selected yet)");
        }
        
        // Update project folder path if base folder is already selected
        if (!string.IsNullOrEmpty(ProjectFolderTextBox.Text))
        {
            // Extract the base folder from the current path
            var currentPath = ProjectFolderTextBox.Text;
            var parentFolder = Path.GetDirectoryName(currentPath);
            if (!string.IsNullOrEmpty(parentFolder))
            {
                var newProjectFolder = Path.Combine(parentFolder, _projectSettings.ProjectName);
                ProjectFolderTextBox.Text = newProjectFolder;
                _projectSettings.ProjectFolderPath = newProjectFolder;
                LogToOutput($"Updated project folder path: {newProjectFolder}");
            }
        }
    }

    private string CleanForFilename(string input)
    {
        if (string.IsNullOrEmpty(input)) return "Unknown";
        
        // Remove invalid filename characters
        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = string.Join("_", input.Split(invalid, StringSplitOptions.RemoveEmptyEntries));
        
        // Replace spaces with underscores and limit length
        cleaned = cleaned.Replace(" ", "_").Replace("-", "_");
        if (cleaned.Length > 50) cleaned = cleaned.Substring(0, 50);
        
        return cleaned;
    }

    private List<DatabaseRelease> GetAllReleases()
    {
        try
        {
            var releases = DcPgConn.GetAllReleases();
            return releases.Select((r, index) => new DatabaseRelease 
            { 
                Id = GetReleaseId(r) ?? (index + 1),
                Name = GetReleaseName(r) ?? $"Release {index + 1}",
                ProjectId = 0, // No longer needed since we're not filtering by project
                CreatedAt = DateTime.Now.AddDays(-index)
            }).ToList();
        }
        catch (Exception ex)
        {
            LogToOutput($"Database GetAllReleases failed: {ex.Message}");
            // Return empty list when database is not available
            return new List<DatabaseRelease>();
        }
    }

    private string? GetReleaseName(object? releaseObj)
    {
        if (releaseObj == null) return null;
        
        // Try to access the release name property using reflection
        try
        {
            var nameProperty = releaseObj.GetType().GetProperty("releaseName") ?? 
                             releaseObj.GetType().GetProperty("ReleaseName") ??
                             releaseObj.GetType().GetProperty("Name");
            return nameProperty?.GetValue(releaseObj)?.ToString();
        }
        catch
        {
            return releaseObj.ToString();
        }
    }

    private int? GetReleaseId(object? releaseObj)
    {
        if (releaseObj == null) return null;
        
        // Try to access the ReleaseId property using reflection
        try
        {
            var idProperty = releaseObj.GetType().GetProperty("ReleaseId") ?? 
                           releaseObj.GetType().GetProperty("releaseId") ??
                           releaseObj.GetType().GetProperty("Id");
            var idValue = idProperty?.GetValue(releaseObj);
            return idValue is int id ? id : null;
        }
        catch
        {
            return null;
        }
    }

    private void ReleaseComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ReleaseComboBox.SelectedItem is DatabaseRelease selectedRelease)
        {
            LogToOutput($"=== RELEASE SELECTION DEBUG ===");
            LogToOutput($"Selected release: ID={selectedRelease.Id}, Name='{selectedRelease.Name}', ProjectId={selectedRelease.ProjectId}");
            
            _projectSettings.SelectedRelease = selectedRelease;
            
            // Load reports for the selected release
            LoadReportsForRelease(selectedRelease.Id);
            
            // Regenerate project name with new release
            GenerateProjectName();
            
            LogToOutput($"=== END RELEASE SELECTION DEBUG ===");
            UpdateUIState();
        }
    }

    private void ReportComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ReportComboBox.SelectedItem is DatabaseReport selectedReport)
        {
            LogToOutput($"=== REPORT SELECTION DEBUG ===");
            LogToOutput($"Selected report: ID={selectedReport.Id}, Name='{selectedReport.Name}', ReleaseId={selectedReport.ReleaseId}");
            LogToOutput($"Selected report ProjectName='{selectedReport.ProjectName}'");
            LogToOutput($"NOTE: Name='{selectedReport.Name}' comes from database 'reportName' field");
            LogToOutput($"NOTE: ProjectName='{selectedReport.ProjectName}' comes from database 'projectName' field");
            
            _projectSettings.SelectedReport = selectedReport;
            
            LogToOutput($"🔄 Report selected - triggering changelist loading...");
            LoadChangelistsForCurrentSelection();
            
            // Regenerate project name with new report
            GenerateProjectName();
            
            LogToOutput($"=== END REPORT SELECTION DEBUG ===");
            UpdateUIState();
        }
    }

    private async void CoverageType_Changed(object sender, RoutedEventArgs e)
    {
        // Prevent execution during XAML initialization
        if (FunctionalRadio == null || CodeRadio == null || _projectSettings == null)
            return;

        LogToOutput("=== COVERAGE TYPE CHANGED ===");

        if (FunctionalRadio.IsChecked == true)
        {
            _projectSettings.CoverageType = CoverageType.Functional;
            LogToOutput("Coverage type changed to: Functional");
        }
        else if (CodeRadio.IsChecked == true)
        {
            _projectSettings.CoverageType = CoverageType.Code;
            LogToOutput("Coverage type changed to: Code");
        }

        // Clear changelist selection when coverage type changes
        if (ChangelistComboBox != null)
        {
            ChangelistComboBox.SelectedIndex = -1;
        }
        _availableChangelists.Clear();
        _projectSettings.SelectedChangelist = string.Empty;
        _projectSettings.ReportPath = string.Empty;
        // ReportPathTextBox removed from UI

        // Reload reports if a release is already selected (since reports depend on coverage type now)
        if (_projectSettings.SelectedRelease != null)
        {
            LogToOutput($"Reloading reports for release {_projectSettings.SelectedRelease.Id} with new coverage type");
            
            // Clear current reports first
            _availableReports.Clear();
            if (ReportComboBox != null)
            {
                ReportComboBox.ItemsSource = null;
                ReportComboBox.SelectedIndex = -1;
            }
            
            // Load reports asynchronously to prevent UI hanging
            try
            {
                _availableReports = await GetReportsFromDatabaseAsync(_projectSettings.SelectedRelease.Id);
                
                // Update the UI on the UI thread
                Dispatcher.Invoke(() =>
                {
                    if (ReportComboBox != null)
                    {
                        ReportComboBox.ItemsSource = _availableReports;
                        ReportComboBox.SelectedIndex = -1;
                    }
                });
            }
            catch (Exception ex)
            {
                LogToOutput($"❌ Error reloading reports after coverage type change: {ex.Message}");
                MessageBox.Show($"Error reloading reports: {ex.Message}", "Error", 
                              MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        LoadChangelistsForCurrentSelection();
        
        // Regenerate project name with new coverage type
        GenerateProjectName();
        
        UpdateUIState();
        
        LogToOutput("=== COVERAGE TYPE CHANGE COMPLETED ===");
    }

    private void ReportType_Changed(object sender, RoutedEventArgs e)
    {
        // Prevent execution during XAML initialization
        if (IndividualRadio == null || AccumulateRadio == null || _projectSettings == null)
            return;

        if (IndividualRadio.IsChecked == true)
        {
            _projectSettings.ReportType = ReportType.Individual;
        }
        else if (AccumulateRadio.IsChecked == true)
        {
            _projectSettings.ReportType = ReportType.Accumulate;
        }

        // Clear changelist selection when report type changes
        if (ChangelistComboBox != null)
        {
            ChangelistComboBox.SelectedIndex = -1;
        }
        _availableChangelists.Clear();
        _projectSettings.SelectedChangelist = string.Empty;
        _projectSettings.ReportPath = string.Empty;
        // ReportPathTextBox removed from UI

        LoadChangelistsForCurrentSelection();
        UpdateUIState();
    }

    private async void LoadReportsForRelease(int releaseId)
    {
        try
        {
            LogToOutput($"=== LOADING REPORTS FOR RELEASE ===");
            LogToOutput($"Release ID: {releaseId}");
            LogToOutput($"Current project settings: CoverageType={_projectSettings.CoverageType}, ReportType={_projectSettings.ReportType}");
            LogToOutput($"Database connection status: {_isDatabaseConnected}");
            
            // First, let's test what's available in the database
            await DiagnoseDatabaseMethods();
            
            // Call GetAllReportsForRelease using the async method
            _availableReports = await GetReportsFromDatabaseAsync(releaseId);
            
            LogToOutput($"Retrieved {_availableReports.Count} reports: [{string.Join(", ", _availableReports.Select(r => $"ID={r.Id}, Name='{r.Name}'"))}]");
            
            if (_availableReports.Count == 0)
            {
                LogToOutput("⚠️ No reports found. Attempting alternative approaches...");
                await TryAlternativeReportRetrieval(releaseId);
            }
            
            LogToOutput($"=== END REPORTS DEBUG ===");
            
            // Populate the report ComboBox
            ReportComboBox.ItemsSource = _availableReports;
            ReportComboBox.SelectedIndex = -1;
            
            // Update UI to show status
            if (_availableReports.Count == 0)
            {
                LogToOutput("📋 No reports available for this release and coverage type combination");
                MessageBox.Show($"No reports found for Release ID {releaseId} with coverage type '{_projectSettings.GetCoverageTypeString()}'. Check the debug output for more details.", 
                              "No Reports Found", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            LogToOutput($"❌ ERROR in LoadReportsForRelease: {ex.Message}");
            LogToOutput($"❌ ERROR Stack Trace: {ex.StackTrace}");
            MessageBox.Show($"Error loading reports: {ex.Message}", "Database Error", 
                          MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async Task DiagnoseDatabaseMethods()
    {
        await Task.Run(() =>
        {
            try
            {
                LogToOutput("🔍 === DATABASE METHODS DIAGNOSIS ===");
                var dcPgConnType = typeof(DcPgConn);
                var allMethods = dcPgConnType.GetMethods(BindingFlags.Public | BindingFlags.Static);
                
                LogToOutput($"📊 Total methods in DcPgConn: {allMethods.Length}");
                
                // Show all methods that might be related to reports
                var reportRelatedMethods = allMethods.Where(m => 
                    m.Name.ToLower().Contains("report") || 
                    m.Name.ToLower().Contains("coverage") ||
                    m.Name.ToLower().Contains("data") ||
                    m.Name.ToLower().Contains("get")).ToArray();
                
                LogToOutput($"📋 Report/Coverage/Data related methods ({reportRelatedMethods.Length}):");
                foreach (var method in reportRelatedMethods)
                {
                    var parameters = string.Join(", ", method.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"));
                    LogToOutput($"  • {method.Name}({parameters}) -> {method.ReturnType.Name}");
                }
            }
            catch (Exception ex)
            {
                LogToOutput($"❌ Error in database diagnosis: {ex.Message}");
            }
        });
    }

    private async Task TryAlternativeReportRetrieval(int releaseId)
    {
        await Task.Run(() =>
        {
            try
            {
                LogToOutput("🔄 Trying alternative report retrieval methods...");
                
                var dcPgConnType = typeof(DcPgConn);
                var allMethods = dcPgConnType.GetMethods(BindingFlags.Public | BindingFlags.Static);
                
                // Try to call any method that might return data
                foreach (var method in allMethods)
                {
                    if (method.ReturnType != typeof(void) && 
                        !method.ReturnType.IsValueType &&
                        method.GetParameters().Length <= 2)
                    {
                        try
                        {
                            LogToOutput($"🧪 Testing method: {method.Name}");
                            object? result = null;
                            
                            var paramTypes = method.GetParameters();
                            if (paramTypes.Length == 0)
                            {
                                result = method.Invoke(null, new object[0]);
                            }
                            else if (paramTypes.Length == 1 && paramTypes[0].ParameterType == typeof(int))
                            {
                                result = method.Invoke(null, new object[] { releaseId });
                            }
                            else if (paramTypes.Length == 2 && paramTypes[0].ParameterType == typeof(int) && paramTypes[1].ParameterType == typeof(string))
                            {
                                result = method.Invoke(null, new object[] { releaseId, _projectSettings.GetCoverageTypeString() });
                            }
                            
                            if (result != null)
                            {
                                LogToOutput($"✅ Method {method.Name} returned: {result.GetType().Name}");
                                if (result is System.Collections.IEnumerable enumerable)
                                {
                                    int count = 0;
                                    foreach (var item in enumerable)
                                    {
                                        count++;
                                        if (count <= 3) // Show first 3 items
                                        {
                                            LogToOutput($"  📄 Item {count}: {item?.GetType().Name} - {item}");
                                        }
                                    }
                                    LogToOutput($"  📊 Total items in result: {count}");
                                }
                            }
                        }
                        catch (Exception methodEx)
                        {
                            LogToOutput($"❌ Method {method.Name} failed: {methodEx.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogToOutput($"❌ Error in alternative retrieval: {ex.Message}");
            }
        });
    }

    private async void LoadChangelistsForCurrentSelection()
    {
        if (_projectSettings.SelectedReport == null)
        {
            LogToOutput("No report selected");
            return;
        }

        try
        {
            var coverageType = _projectSettings.GetCoverageTypeString(); // func_cov/code_cov
            var reportType = _projectSettings.GetReportTypeString(); // individual/accumulate
            
            LogToOutput($"Loading changelists for {_projectSettings.SelectedReport.Name} ({reportType})...");
            
            // Use actual database call to get changelists based on selected report
            // API signature: GetChangelistsForReport(int reportId, string reportType, int? limit)
            // where reportType is "individual" or "accumulate" based on user selection
            _availableChangelists = await Task.Run(() => GetChangelistsFromDatabase(
                _projectSettings.SelectedReport.Id,  // This should be the reportId from the database (like 15960)
                reportType));  // This should be report type like "individual" or "accumulate"

            LogToOutput($"Found {_availableChangelists.Count} changelists for {_projectSettings.SelectedReport.Name}");

            // Update the UI on the UI thread
            Dispatcher.Invoke(() =>
            {
                if (ChangelistComboBox != null)
                {
                    ChangelistComboBox.ItemsSource = _availableChangelists;
                    ChangelistComboBox.SelectedIndex = -1;
                }
                else
                {
                    LogToOutput("Error: ChangelistComboBox not found");
                }
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error loading changelists: {ex.Message}", "Database Error", 
                          MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async Task<List<DatabaseReport>> GetReportsFromDatabaseAsync(int releaseId)
    {

        
        try
        {
            // Get the coverage type string for the database call
            var covType = _projectSettings.GetCoverageTypeString(); // func_cov or code_cov

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            
            var dbReports = await Task.Run(async () =>
            {
                var timeoutTask = Task.Delay(30000); // 30 second timeout
                var dbTask = Task.Run(() => DcPgConn.GetAllReportsForRelease(releaseId, covType));
                
                var completedTask = await Task.WhenAny(dbTask, timeoutTask);
                
                if (completedTask == timeoutTask)
                {
                    throw new TimeoutException("Database call timed out after 30 seconds");
                }
                
                return await dbTask;
            });
            
            stopwatch.Stop();
            if (dbReports == null)
            {
                return new List<DatabaseReport>();
            }
            
            
            // Convert the database results to our DatabaseReport objects
            var reports = new List<DatabaseReport>();
            int itemCount = 0;
            
            foreach (var dbReport in dbReports)
            {
                itemCount++;
                
                var report = new DatabaseReport
                {
                    Id = dbReport.reportId,
                    Name = dbReport.reportName ?? "",
                    ProjectName = dbReport.projectName ?? "",
                    ReleaseId = dbReport.releaseId,
                    CreatedAt = DateTime.Now
                };
                
                
                if (report.Id > 0 && !string.IsNullOrEmpty(report.Name))
                {
                    reports.Add(report);
                }
                else
                {
                }
            }
            

            
            return reports;
        }
        catch (TimeoutException)
        {
            MessageBox.Show($"Database operation timed out. The database may be slow or unresponsive.", "Timeout Error", 
                          MessageBoxButton.OK, MessageBoxImage.Warning);
            return new List<DatabaseReport>();
        }
        catch (Exception ex)
        {
            if (ex.InnerException != null)
            {
            }
            
            // Return empty list when database call fails
            return new List<DatabaseReport>();
        }
        finally
        {
            LogToOutput($"=== GetReportsFromDatabase END ===");
        }
    }

    private int? GetReleaseIdFromReport(object reportItem)
    {
        if (reportItem == null) return null;
        
        try
        {
            var itemType = reportItem.GetType();
            
            // Try multiple possible property names for release ID
            var releaseIdProp = itemType.GetProperty("ReleaseId") ?? 
                              itemType.GetProperty("releaseId") ?? 
                              itemType.GetProperty("Release_Id") ?? 
                              itemType.GetProperty("release_id") ??
                              itemType.GetProperty("RelId") ??
                              itemType.GetProperty("rel_id");
                              
            if (releaseIdProp != null && releaseIdProp.CanRead)
            {
                var releaseIdValue = releaseIdProp.GetValue(reportItem);
                if (releaseIdValue != null && int.TryParse(releaseIdValue.ToString(), out int relId))
                {
                    return relId;
                }
            }
            
            return null;
        }
        catch
        {
            return null;
        }
    }

    private bool TryGetReportsMethod(int releaseId, string covType, out List<DatabaseReport> reports)
    {
        reports = new List<DatabaseReport>();
        
        try
        {
            LogToOutput("=== REPORTS DATABASE METHOD REFLECTION DEBUG START ===");
            LogToOutput($"🔍 Input Parameters: releaseId={releaseId}, covType='{covType}'");
            
            // Get the DcPgConn type directly
            var dcPgConnType = typeof(DcPgConn);
            LogToOutput($"DcPgConn type: {dcPgConnType.FullName}");
            LogToOutput($"DcPgConn assembly: {dcPgConnType.Assembly.FullName}");
            
            // Log ALL available methods in DcPgConn for debugging
            var allMethods = dcPgConnType.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic);
            LogToOutput($"📋 ALL METHODS in DcPgConn ({allMethods.Length} total):");
            foreach (var m in allMethods.Where(m => !m.Name.StartsWith("get_") && !m.Name.StartsWith("set_")))
            {
                var parameters = string.Join(", ", m.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"));
                LogToOutput($"  • {m.Name}({parameters}) -> {m.ReturnType.Name}");
            }
            
            // Look for GetAllReportsForRelease method with different possible signatures
            MethodInfo? method = null;
            
            // First try the expected signature: GetAllReportsForRelease(int releaseId, string covType)
            method = dcPgConnType.GetMethod("GetAllReportsForRelease", new[] { typeof(int), typeof(string) });
            LogToOutput($"🎯 Trying GetAllReportsForRelease(int, string): {(method != null ? "FOUND" : "NOT FOUND")}");
            
            if (method == null)
            {
                // Try alternative method names with various case variations
                var methodNames = new[] { 
                    "GetAllReportsForRelease", "GetReportsForRelease", "GetAllReports", "GetReports",
                    "getAllReportsForRelease", "getReportsForRelease", "getAllReports", "getReports",
                    "GetReportsByRelease", "GetReleaseReports", "GetCoverageReports", "GetProjectReports",
                    "GetAllReportsForReleaseId", "GetReportsForReleaseId", "GetReportsBy", "GetReportList"
                };
                
                foreach (var methodName in methodNames)
                {
                    LogToOutput($"🔎 Searching for method: {methodName}");
                    
                    // Try with (int, string) signature
                    method = dcPgConnType.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static | BindingFlags.IgnoreCase, null, new[] { typeof(int), typeof(string) }, null);
                    if (method != null)
                    {
                        LogToOutput($"✅ Found method: {methodName}(int, string)");
                        break;
                    }
                    
                    // Try with (int) signature
                    method = dcPgConnType.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static | BindingFlags.IgnoreCase, null, new[] { typeof(int) }, null);
                    if (method != null)
                    {
                        LogToOutput($"✅ Found method: {methodName}(int) - will call without covType");
                        break;
                    }
                    
                    // Try with no parameters (get all reports)
                    method = dcPgConnType.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static | BindingFlags.IgnoreCase, null, new Type[0], null);
                    if (method != null)
                    {
                        LogToOutput($"✅ Found method: {methodName}() - will call without parameters");
                        break;
                    }
                    
                    LogToOutput($"❌ Method {methodName} not found with expected signatures");
                }
            }
            
            if (method == null)
            {
                LogToOutput("❌ ERROR: No suitable reports method found");
                
                // Show available methods for debugging
                var reportMethods = dcPgConnType.GetMethods(BindingFlags.Public | BindingFlags.Static)
                    .Where(m => m.Name.Contains("Report", StringComparison.OrdinalIgnoreCase) || 
                               m.Name.Contains("Release", StringComparison.OrdinalIgnoreCase))
                    .ToArray();
                foreach (var m in reportMethods)
                {
                    var parameters = string.Join(", ", m.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"));
                    LogToOutput($"  • {m.Name}({parameters}) -> {m.ReturnType.Name}");
                }
                
                // Try to use any method that might return reports
                var fallbackMethods = dcPgConnType.GetMethods(BindingFlags.Public | BindingFlags.Static);
                foreach (var fallbackMethod in fallbackMethods)
                {
                    if (fallbackMethod.ReturnType != typeof(void) && 
                        (fallbackMethod.Name.ToLower().Contains("report") || 
                         fallbackMethod.Name.ToLower().Contains("coverage")))
                    {
                        LogToOutput($"🔍 Trying fallback method: {fallbackMethod.Name}");
                        try
                        {
                            var paramCount = fallbackMethod.GetParameters().Length;
                            if (paramCount == 0)
                            {
                                method = fallbackMethod;
                                break;
                            }
                            else if (paramCount == 1 && fallbackMethod.GetParameters()[0].ParameterType == typeof(int))
                            {
                                method = fallbackMethod;
                                break;
                            }
                        }
                        catch (Exception)
                        {
                        }
                    }
                }
                
                if (method == null)
                {
                    return false;
                }
            }
            
            // Prepare arguments based on method signature
            var parameterTypes = method.GetParameters();
            object[] methodArgs;
            
            if (parameterTypes.Length == 2 && parameterTypes[0].ParameterType == typeof(int) && parameterTypes[1].ParameterType == typeof(string))
            {
                // Method signature: (int releaseId, string covType)
                methodArgs = new object[] { releaseId, covType };
            }
            else if (parameterTypes.Length == 1 && parameterTypes[0].ParameterType == typeof(int))
            {
                // Method signature: (int releaseId) - call without covType
                methodArgs = new object[] { releaseId };
            }
            else if (parameterTypes.Length == 0)
            {
                // Method signature: () - get all reports
                methodArgs = new object[0];
            }
            else
            {
                return false;
            }
            
            // Call the method
            LogToOutput($"🚀 Invoking method...");
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var result = method.Invoke(null, methodArgs);
            stopwatch.Stop();
            
            LogToOutput($"⏱️ Method execution time: {stopwatch.ElapsedMilliseconds}ms");
            
            if (result == null)
            {
                return false;
            }
            
            LogToOutput($"✅ Method returned: {result.GetType().FullName}");
            LogToOutput($"📊 Raw result: {result}");
            
            // Convert result to DatabaseReport list using reflection
            if (result is System.Collections.IEnumerable enumerable)
            {
                LogToOutput($"📋 Processing enumerable result...");
                int itemCount = 0;
                foreach (var item in enumerable)
                {
                    itemCount++;
                    LogToOutput($"📄 Processing item #{itemCount}:");
                    
                    if (item != null)
                    {
                        
                        var report = new DatabaseReport();
                        
                        // Extract properties using reflection
                        var itemType = item.GetType();
                        
                        // Debug: Log all available properties
                        var allProperties = itemType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
                        foreach (var prop in allProperties)
                        {
                            try
                            {
                                var value = prop.GetValue(item);
                                var valueStr = value?.ToString() ?? "<null>";
                            }
                            catch (Exception)
                            {
                            }
                        }
                        
                        // Set ReleaseId to the releaseId parameter we passed to the method
                        report.ReleaseId = releaseId;
                        
                        // Try multiple possible property names for reportId
                        var reportIdProp = itemType.GetProperty("ReportId") ?? 
                                         itemType.GetProperty("reportId") ?? 
                                         itemType.GetProperty("Id") ?? 
                                         itemType.GetProperty("id") ??
                                         itemType.GetProperty("ReportID") ??
                                         itemType.GetProperty("report_id");
                                         
                        if (reportIdProp != null && reportIdProp.CanRead)
                        {
                            var reportIdValue = reportIdProp.GetValue(item);
                            LogToOutput($"  🔢 Found ID property '{reportIdProp.Name}' with value: '{reportIdValue}'");
                            if (reportIdValue != null && int.TryParse(reportIdValue.ToString(), out int repId))
                            {
                                report.Id = repId;
                            }
                            else
                            {
                            }
                        }
                        else
                        {
                        }
                        
                        // Try multiple possible property names for reportName
                        var reportNameProp = itemType.GetProperty("ReportName") ?? 
                                           itemType.GetProperty("reportName") ?? 
                                           itemType.GetProperty("Name") ?? 
                                           itemType.GetProperty("name") ??
                                           itemType.GetProperty("ReportNAME") ??
                                           itemType.GetProperty("report_name") ??
                                           itemType.GetProperty("Title") ??
                                           itemType.GetProperty("title");
                                           
                        if (reportNameProp != null && reportNameProp.CanRead)
                        {
                            var reportNameValue = reportNameProp.GetValue(item);
                            LogToOutput($"  📝 Found Name property '{reportNameProp.Name}' with value: '{reportNameValue}'");
                            if (reportNameValue != null)
                            {
                                report.Name = reportNameValue.ToString() ?? "";
                            }
                            else
                            {
                            }
                        }
                        else
                        {
                        }
                        
                        // Try to get project name if available
                        var projectNameProp = itemType.GetProperty("ProjectName") ?? 
                                            itemType.GetProperty("projectName") ?? 
                                            itemType.GetProperty("Project") ??
                                            itemType.GetProperty("project");
                        if (projectNameProp != null && projectNameProp.CanRead)
                        {
                            var projectValue = projectNameProp.GetValue(item);
                            if (projectValue != null)
                            {
                                report.ProjectName = projectValue.ToString() ?? "";
                            }
                        }
                        
                        // Set default created date
                        report.CreatedAt = DateTime.Now;
                        
                        
                        if (report.Id > 0 && !string.IsNullOrEmpty(report.Name))
                        {
                            // If we called a method with no parameters, filter by releaseId
                            if (parameterTypes.Length == 0)
                            {
                                // Try to get the release ID from the report object
                                var releaseIdFromReport = GetReleaseIdFromReport(item);
                                if (releaseIdFromReport.HasValue && releaseIdFromReport.Value == releaseId)
                                {
                                    reports.Add(report);
                                }
                                else
                                {
                                }
                            }
                            else
                            {
                                reports.Add(report);
                            }
                        }
                        else
                        {
                        }
                    }
                    else
                    {
                    }
                }
                
            }
            else
            {
            }
            
            foreach (var report in reports)
            {
                LogToOutput($"  📋 Report: ID={report.Id}, Name='{report.Name}', ReleaseId={report.ReleaseId}, ProjectName='{report.ProjectName}'");
            }
            
            return reports.Count > 0;
        }
        catch (Exception ex)
        {
            if (ex.InnerException != null)
            {
            }
            return false;
        }
    }



    private List<string> GetChangelistsFromDatabase(int reportId, string reportType)
    {
        
        try
        {
            
            // Use reflection to find the appropriate method
            if (TryGetChangelistsMethod(reportId, reportType, out var changelists))
            {
                return changelists;
            }
            
            return new List<string>();
        }
        catch (Exception)
        {            
            // Return empty list when database is not available
            return new List<string>();
        }
    }

    private bool TryGetChangelistsMethod(int reportId, string reportType, out List<string> changelists)
    {
        changelists = new List<string>();
        
        try
        {
            // Get the DcPgConn type directly
            var dcPgConnType = typeof(DcPgConn);
            
            // Look for GetChangelistsForReport method with 3 parameters (including limit)
            MethodInfo? method = null;
            
            // Try the correct signature: GetChangelistsForReport(int reportId, string reportType, int? limit)
            method = dcPgConnType.GetMethod("GetChangelistsForReport", new[] { typeof(int), typeof(string), typeof(int?) });
            
            if (method == null)
            {
                // Try without the optional parameter: GetChangelistsForReport(int reportId, string reportType)
                method = dcPgConnType.GetMethod("GetChangelistsForReport", new[] { typeof(int), typeof(string) });
            }
            
            if (method == null)
            {
                // Try to find any method with 'changelist' in name
                var methods = dcPgConnType.GetMethods(BindingFlags.Public | BindingFlags.Static)
                    .Where(m => m.Name.ToLower().Contains("changelist")).ToArray();
                
                foreach (var m in methods)
                {
                    var parameters = string.Join(", ", m.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"));
                    LogToOutput($"  - {m.Name}({parameters}) : {m.ReturnType.Name}");
                }
                
                // Take the first one available
                method = methods.FirstOrDefault();
            }
            
            if (method == null)
            {
                return false;
            }
            
            
            // Prepare arguments based on method signature
            var parameterTypes = method.GetParameters();
            object[] methodArgs;
            
            if (parameterTypes.Length >= 3)
            {
                // Method has limit parameter: GetChangelistsForReport(int reportId, string reportType, int? limit)
                methodArgs = new object[] { reportId, reportType, 100 }; // Use reasonable limit
            }
            else
            {
                // Method has only required parameters: GetChangelistsForReport(int reportId, string reportType)
                methodArgs = new object[] { reportId, reportType };
            }
            
            // Call the method
            var methodResult = method.Invoke(null, methodArgs);
            
            if (methodResult == null)
            {
                return false;
            }
            
            
            // Convert result to string list
            if (methodResult is IEnumerable<string> stringEnumerable)
            {
                changelists = stringEnumerable.Where(s => !string.IsNullOrEmpty(s)).ToList();
                return true;
            }
            else if (methodResult is System.Collections.IEnumerable enumerable)
            {
                changelists = enumerable.Cast<object>()
                    .Select(obj => obj?.ToString() ?? "")
                    .Where(s => !string.IsNullOrEmpty(s))
                    .ToList();
                return true;
            }
            else
            {
                return false;
            }
        }
        catch (Exception ex)
        {
            LogToOutput($"❌ Exception in TryGetChangelistsMethod: {ex.Message}");
            LogToOutput($"❌ Stack trace: {ex.StackTrace}");
            return false;
        }
    }

    private string? TryGetReportPathFromDatabase(string projectName, string releaseName, string reportName, string coverageType, string reportType, string changelist)
    {
        try
        {
            
            // First try the new GetReleaseReportInfo method if it exists
            var dcPgConnType = typeof(DcPgConn);
            var getReleaseReportInfoMethod = dcPgConnType.GetMethod("GetReleaseReportInfo");
            
            if (getReleaseReportInfoMethod != null)
            {
                LogToOutput("Found GetReleaseReportInfo method, trying to call it...");
                var parameters = getReleaseReportInfoMethod.GetParameters();
                LogToOutput($"GetReleaseReportInfo parameters: {string.Join(", ", parameters.Select(p => $"{p.ParameterType.Name} {p.Name}"))}");
                
                // Try to call GetReleaseReportInfo with appropriate parameters
                // This method likely returns path information based on the release and report details
                try
                {
                    object[]? args = null;
                    
                    // Try different parameter combinations based on what the method expects
                    if (parameters.Length == 3 && parameters.All(p => p.ParameterType == typeof(string)))
                    {
                        args = new object[] { releaseName, reportName, reportType };
                    }
                    else if (parameters.Length == 4 && parameters.All(p => p.ParameterType == typeof(string)))
                    {
                        args = new object[] { releaseName, reportName, reportType, changelist };
                    }
                    else if (parameters.Length == 5 && parameters.All(p => p.ParameterType == typeof(string)))
                    {
                        args = new object[] { projectName, releaseName, reportName, reportType, changelist };
                    }
                    
                    if (args != null)
                    {
                        LogToOutput($"Calling GetReleaseReportInfo with {args.Length} parameters");
                        var result = getReleaseReportInfoMethod.Invoke(null, args);
                        
                        if (result != null)
                        {
                            var pathResult = result.ToString();
                            LogToOutput($"GetReleaseReportInfo returned: '{pathResult}'");
                            if (!string.IsNullOrEmpty(pathResult))
                            {
                                return pathResult;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogToOutput($"Error calling GetReleaseReportInfo: {ex.Message}");
                }
            }
            else
            {
                LogToOutput("GetReleaseReportInfo method not found, trying GetReportPath...");
            }
            
            // Use the correct GetReportPath method signature:
            // GetReportPath(string projectName, string releaseName, string covType, string reportName, string reportType, string changelist, string fileName = "")
            var fileName = "";  // Empty fileName as default
 
            
            // Correct parameter order: GetReportPath(projectName, releaseName, covType, reportName, reportType, changelist, fileName)
            var reportPath = DcPgConn.GetReportPath(projectName, releaseName, coverageType, reportName, reportType, changelist, fileName);
            LogToOutput($"GetReportPath returned: '{reportPath}'");
            LogToOutput($"=== END GetReportPath CALL ===");
            
            return reportPath;
        }
        catch (Exception ex)
        {
            LogToOutput($"Error in TryGetReportPathFromDatabase: {ex.Message}");
            // Database method call failed
            return null;
        }
    }

    private string GenerateDisplayPath(string projectName, string releaseName, string coverageType, string changelist)
    {
        // Generate a human-readable path using names for display purposes
        if (string.IsNullOrEmpty(projectName))
        {
            return $"/remote/reports/{releaseName}/{coverageType}/{changelist}";
        }
        return $"/remote/reports/{projectName}/{releaseName}/{coverageType}/{changelist}";
    }

    private string GetDisplayCoverageType()
    {
        // Return friendly names for display in paths
        return _projectSettings.CoverageType switch
        {
            CoverageType.Functional => "functional",
            CoverageType.Code => "code",
            _ => "functional"
        };
    }

    private void ChangelistComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ChangelistComboBox.SelectedItem is string selectedChangelist && 
            _projectSettings.SelectedRelease != null &&
            _projectSettings.SelectedReport != null)
        {
            _projectSettings.SelectedChangelist = selectedChangelist;

            // Generate report path using all available information from GetAllReportsForRelease
            var coverageType = _projectSettings.GetCoverageTypeString(); // Use new internal format: func_cov/code_cov
            var reportType = _projectSettings.GetReportTypeString(); // individual/accumulate
            
            LogToOutput($"Generating report path for {selectedChangelist}...");
            
            // Regenerate project name with new changelist
            GenerateProjectName();
            
            // Try to get the actual report path from database using all available information
            try
            {
                // Try the new GetReleaseReportInfo method if available, or use existing GetReportPath
                var dbPath = TryGetReportPathFromDatabase(
                    _projectSettings.SelectedReport.ProjectName,
                    _projectSettings.SelectedRelease.Name,
                    _projectSettings.SelectedReport.Name,
                    coverageType,
                    reportType,
                    selectedChangelist);
                
                _projectSettings.ReportPath = dbPath ?? GenerateDisplayPath(
                    _projectSettings.SelectedReport.ProjectName,
                    _projectSettings.SelectedRelease.Name,
                    GetDisplayCoverageType(),
                    selectedChangelist);
            }
            catch (Exception ex)
            {
                LogToOutput($"Error getting database path: {ex.Message}");
                // Fallback to generated path using all available information
                _projectSettings.ReportPath = GenerateDisplayPath(
                    _projectSettings.SelectedReport.ProjectName,
                    _projectSettings.SelectedRelease.Name,
                    GetDisplayCoverageType(),
                    selectedChangelist);
            }

            LogToOutput($"Generated path: '{_projectSettings.ReportPath}'");
            LogToOutput($"=== END PATH GENERATION DEBUG ===");

            // ReportPathTextBox removed from UI - path is stored internally
            UpdateUIState();
        }
    }

    private void SshConfig_Changed(object sender, TextChangedEventArgs e)
    {
        if (_projectSettings == null) return;
        
        _projectSettings.SshHost = SshHostTextBox?.Text ?? string.Empty;
        UpdateUIState();
    }

    private async void CreateProjectButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_projectSettings.IsValid())
        {
            MessageBox.Show("Please complete all required fields.", "Validation Error", 
                          MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            // Show progress panel
            ProgressPanel.Visibility = Visibility.Visible;
            CreateProjectButton.IsEnabled = false;
            CancelButton.IsEnabled = false;

            // Save project settings
            _projectSettings.Save();

            // Copy files via SSH
            await CopyFilesViaSsh();

            CompletedProject = _projectSettings;
            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error creating project: {ex.Message}", "Project Creation Error", 
                          MessageBoxButton.OK, MessageBoxImage.Error);
            
            ProgressPanel.Visibility = Visibility.Collapsed;
            CreateProjectButton.IsEnabled = true;
            CancelButton.IsEnabled = true;
        }
    }

    private async Task CopyFilesViaSsh()
    {
        if (string.IsNullOrEmpty(_projectSettings.SshHost) || 
            string.IsNullOrEmpty(_projectSettings.ReportPath))
        {
            throw new InvalidOperationException("SSH host or report path is incomplete");
        }

        var progress = new Progress<(int filesProcessed, int totalFiles, string currentFile)>(report =>
        {
            Dispatcher.Invoke(() =>
            {
                if (report.totalFiles > 0)
                {
                    CopyProgressBar.Value = (double)report.filesProcessed / report.totalFiles * 100;
                    ProgressStatusText.Text = $"Copying {report.currentFile} ({report.filesProcessed}/{report.totalFiles})";
                }
                else
                {
                    ProgressStatusText.Text = "Connecting to SSH server...";
                }
            });
        });

        await Task.Run(() =>
        {
            try
            {
                LogToOutput($"=== SSH FILE COPY OPERATION ===");
                LogToOutput($"SSH Host: {_projectSettings.SshHost}");
                LogToOutput($"Remote Path: {_projectSettings.ReportPath}");
                LogToOutput($"Local Destination: {_projectSettings.LocalDataPath}");
                LogToOutput($"NOTE: SSH credentials are never saved - using SSH config files and interactive dialog only");
                
                ((IProgress<(int, int, string)>)progress).Report((0, 0, "Reading SSH configuration..."));
                
                // Get SSH configuration for the host
                var sshConfig = SshConfigParser.GetHostConfig(_projectSettings.SshHost);
                if (sshConfig != null)
                {
                    LogToOutput($"SSH Config found - User: {sshConfig.User}, Identity files: {sshConfig.IdentityFiles.Count}");
                }
                else
                {
                    LogToOutput("No SSH config found, using defaults");
                    sshConfig = new SshHostConfig
                    {
                        HostName = _projectSettings.SshHost,
                        User = Environment.UserName,
                        IdentityFiles = SshConfigParser.GetIdentityFiles(_projectSettings.SshHost)
                    };
                }
                
                ((IProgress<(int, int, string)>)progress).Report((0, 0, "Connecting to SSH server..."));
                
                // Try SSH key authentication first
                SshClient? sshClient = null;
                SftpClient? sftpClient = null;
                string? username = null;
                string? password = null;
                bool usedSshKeys = false;
                
                // Try SSH keys first
                if (sshConfig.IdentityFiles.Count > 0)
                {
                    LogToOutput($"Attempting SSH key authentication with {sshConfig.IdentityFiles.Count} identity files...");
                    
                    foreach (var keyPath in sshConfig.IdentityFiles)
                    {
                        try
                        {
                            LogToOutput($"Trying SSH key: {keyPath}");
                            if (!File.Exists(keyPath))
                            {
                                LogToOutput($"Key file not found: {keyPath}");
                                continue;
                            }
                            
                            var keyFile = new PrivateKeyFile(keyPath);
                            var keyFiles = new[] { keyFile };
                            
                            // Use username from SSH config or default
                            username = sshConfig.User ?? Environment.UserName;
                            
                            sshClient = new SshClient(_projectSettings.SshHost, username, keyFiles);
                            sshClient.Connect();
                            
                            if (sshClient.IsConnected)
                            {
                                LogToOutput($"SSH key authentication successful with {keyPath}");
                                usedSshKeys = true;
                                break;
                            }
                        }
                        catch (Exception keyEx)
                        {
                            LogToOutput($"SSH key authentication failed with {keyPath}: {keyEx.Message}");
                            sshClient?.Dispose();
                            sshClient = null;
                        }
                    }
                }
                else
                {
                    LogToOutput("No SSH keys found in configuration");
                }
                
                // Fallback to password authentication if keys failed
                if (sshClient == null || !sshClient.IsConnected)
                {
                    LogToOutput("SSH key authentication failed, prompting for credentials...");
                    
                    // Show credentials dialog on UI thread
                    bool credentialsProvided = false;
                    Dispatcher.Invoke(() =>
                    {
                        var (success, dialogUsername, dialogPassword) = SshCredentialsDialog.GetCredentials(
                            this, _projectSettings.SshHost, sshConfig?.User);
                        
                        if (success)
                        {
                            username = dialogUsername;
                            password = dialogPassword;
                            credentialsProvided = true;
                        }
                    });
                    
                    if (!credentialsProvided || string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
                    {
                        throw new OperationCanceledException("SSH authentication was cancelled by user or credentials not provided");
                    }
                    
                    LogToOutput($"Using password authentication for user: {username}");
                    sshClient = new SshClient(_projectSettings.SshHost, username, password);
                    sshClient.Connect();
                    LogToOutput("SSH password authentication successful");
                }
                
                LogToOutput($"SSH connection established using {(usedSshKeys ? "SSH keys" : "password")} for user: {username}");
                
                ((IProgress<(int, int, string)>)progress).Report((0, 0, "Listing remote files..."));
                
                // List all *.* files in the remote path
                // Use -P to avoid following symlinks that could cause loops, but resolve the path first
                var resolveCommand = $"readlink -f '{_projectSettings.ReportPath}'";
                var resolveResult = sshClient.RunCommand(resolveCommand);
                
                string searchPath;
                if (resolveResult.ExitStatus == 0 && !string.IsNullOrWhiteSpace(resolveResult.Result))
                {
                    searchPath = resolveResult.Result.Trim();
                    LogToOutput($"Resolved symlink: {_projectSettings.ReportPath} -> {searchPath}");
                }
                else
                {
                    searchPath = _projectSettings.ReportPath;
                    LogToOutput($"Using original path (not a symlink): {searchPath}");
                }
                
                LogToOutput($"DEBUG: Searching for .* files in: {searchPath}");
                var listCommand = $"find '{searchPath}' -name '*.*' -type f";
                LogToOutput($"Executing command: {listCommand}");
                
                var result = sshClient.RunCommand(listCommand);
                if (result.ExitStatus != 0)
                {
                    throw new InvalidOperationException($"Failed to list files: {result.Error}");
                }
                
                var txtFiles = result.Result.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                LogToOutput($"Found {txtFiles.Length} .* files: [{string.Join(", ", txtFiles.Take(5))}]");
                
                if (txtFiles.Length == 0)
                {
                    LogToOutput("No .* files found in remote path");
                    LogToOutput($"DEBUG: Let's check if the directory exists and what files are actually there...");
                    
                    // Check if directory exists and list all files for debugging
                    var debugListCmd = $"ls -la '{searchPath}'";
                    var debugResult = sshClient.RunCommand(debugListCmd);
                    LogToOutput($"Directory listing result: {debugResult.Result}");
                    
                    ((IProgress<(int, int, string)>)progress).Report((0, 0, "No .* files found"));
                    return;
                }
                
                // Create SFTP connection using the same authentication method
                if (usedSshKeys)
                {
                    // Find the key that worked
                    foreach (var keyPath in sshConfig.IdentityFiles)
                    {
                        try
                        {
                            if (File.Exists(keyPath))
                            {
                                var keyFile = new PrivateKeyFile(keyPath);
                                var keyFiles = new[] { keyFile };
                                sftpClient = new SftpClient(_projectSettings.SshHost, username!, keyFiles);
                                sftpClient.Connect();
                                if (sftpClient.IsConnected)
                                {
                                    break;
                                }
                            }
                        }
                        catch
                        {
                            sftpClient?.Dispose();
                            sftpClient = null;
                        }
                    }
                }
                else
                {
                    sftpClient = new SftpClient(_projectSettings.SshHost, username!, password!);
                    sftpClient.Connect();
                }
                
                if (sftpClient == null || !sftpClient.IsConnected)
                {
                    throw new InvalidOperationException("Failed to establish SFTP connection");
                }
                
                LogToOutput("SFTP connection established");
                
                // Copy each file
                for (int i = 0; i < txtFiles.Length; i++)
                {
                    var remoteFile = txtFiles[i].Trim();
                    if (string.IsNullOrEmpty(remoteFile)) continue;
                    
                    var fileName = Path.GetFileName(remoteFile);
                    var localFilePath = Path.Combine(_projectSettings.LocalDataPath, fileName);
                    
                    LogToOutput($"Copying: {remoteFile} -> {localFilePath}");
                    ((IProgress<(int, int, string)>)progress).Report((i, txtFiles.Length, fileName));
                    
                    try
                    {
                        using var localFileStream = File.Create(localFilePath);
                        sftpClient.DownloadFile(remoteFile, localFileStream);
                        LogToOutput($"Successfully copied: {fileName}");
                    }
                    catch (Exception fileEx)
                    {
                        LogToOutput($"Error copying {fileName}: {fileEx.Message}");
                        // Continue with other files
                    }
                }
                
                // Clean up connections
                sftpClient?.Disconnect();
                sftpClient?.Dispose();
                sshClient?.Disconnect();
                sshClient?.Dispose();
                
                ((IProgress<(int, int, string)>)progress).Report((txtFiles.Length, txtFiles.Length, "Copy complete"));
                LogToOutput($"=== SSH COPY OPERATION COMPLETED ===");
            }
            catch (Exception ex)
            {
                LogToOutput($"SSH Copy Error: {ex.Message}");
                LogToOutput($"SSH Copy Stack Trace: {ex.StackTrace}");
                
                // SSH operation failed - let the error propagate
                throw;
            }
        });
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void UpdateUIState()
    {
        // Enable/disable panels based on completion status
        // Step 2: Coverage Type is always enabled (moved to step 2)
        CoverageTypePanel.IsEnabled = true;

        // Step 3: Release Selection is always enabled (database connection is required)
        ReleaseSelectionPanel.IsEnabled = true;

        // Step 4: Report Selection enabled when release is selected (coverage type is always set)
        ReportSelectionPanel.IsEnabled = _projectSettings.SelectedRelease != null;

        // Step 5: Changelist selection enabled when report is selected
        ChangelistSelectionPanel.IsEnabled = _projectSettings.SelectedReport != null;

        // Step 6: SSH config enabled when changelist is selected
        SshConfigPanel.IsEnabled = !string.IsNullOrEmpty(_projectSettings.SelectedChangelist);

        // Enable Create Project button only when all required fields are completed
        // Note: ProjectName is now auto-generated, so we just check if it exists
        var hasProjectName = !string.IsNullOrEmpty(_projectSettings.ProjectName);
        var hasProjectFolder = !string.IsNullOrEmpty(_projectSettings.ProjectFolderPath);
        var hasRelease = _projectSettings.SelectedRelease != null;
        var hasReport = _projectSettings.SelectedReport != null;
        var hasChangelist = !string.IsNullOrEmpty(_projectSettings.SelectedChangelist);
        var hasSshHost = !string.IsNullOrEmpty(_projectSettings.SshHost);
        
        // Debug validation
        LogToOutput($"Validation - ProjectName: {hasProjectName} ('{_projectSettings.ProjectName}'), ProjectFolder: {hasProjectFolder}, Release: {hasRelease}, Report: {hasReport}, Changelist: {hasChangelist}, SshHost: {hasSshHost}");
        
        CreateProjectButton.IsEnabled = hasProjectName && hasProjectFolder && hasRelease && hasReport && hasChangelist && hasSshHost;
        LogToOutput($"Create Project Button Enabled: {CreateProjectButton.IsEnabled}");
    }

    protected override void OnClosed(EventArgs e)
    {
        // Clean up database connection
        try
        {
            DcPgConn.CloseDb();
        }
        catch { }
        
        base.OnClosed(e);
    }

    private void LogToOutput(string message)
    {
        var logMessage = $"ProjectWizard: {message}";
        Console.WriteLine(logMessage);
        
        // Write to MainWindow's output panel if available
        try
        {
            _mainWindow?.Dispatcher.Invoke(() =>
            {
                _mainWindow?.AddToOutput(logMessage);
            });
        }
        catch
        {
            // MainWindow not available yet, just use console
        }
    }
}