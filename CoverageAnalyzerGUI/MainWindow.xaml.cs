using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using JiraAPI;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Threading.Tasks;
using System.Text.Json.Nodes;
using CoverageAnalyzerGUI.Models;
using Microsoft.Win32;
using HvpHtmlParser;
using Microsoft.Web.WebView2.Wpf;
using AvalonDock;
using System.Windows.Input;

namespace CoverageAnalyzerGUI;

/// <summary>
/// Simple RelayCommand implementation for keyboard shortcuts
/// </summary>
public class RelayCommand : ICommand
{
    private readonly Action<object?>? _execute;
    private readonly Func<object?, bool>? _canExecute;

    public RelayCommand(Action<object?>? execute = null, Func<object?, bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged
    {
        add { CommandManager.RequerySuggested += value; }
        remove { CommandManager.RequerySuggested -= value; }
    }

    public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;

    public void Execute(object? parameter) => _execute?.Invoke(parameter);
}

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
public class HierarchyNode : INotifyPropertyChanged
{
    private bool _isSelected;
    
    public string Name { get; set; }
    public string FullPath { get; set; }
    public string? Link { get; set; } // URL/path to the report file for this node
    public double CoveragePercentage { get; set; }
    public int LinesCovered { get; set; }
    public int TotalLines { get; set; }
    public List<HierarchyNode> Children { get; set; } = new List<HierarchyNode>();
    public bool IsExpanded { get; set; } = false;
    
    public bool IsSelected 
    { 
        get => _isSelected; 
        set 
        { 
            if (_isSelected != value)
            {
                _isSelected = value;
                OnPropertyChanged();
            }
        } 
    }
    
    public string GroupType 
    { 
        get 
        {
            if (Children.Count > 0) return "Dir";
            if (Name.Contains(".cpp") || Name.Contains(".c")) return "File";
            if (Name.Contains("::")) return "Func";
            return "Item";
        } 
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    
    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
    
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
/// ViewModel for displaying HvpNode properties in the DataGrid
/// </summary>
public class HvpNodePropertyViewModel
{
    public string PropertyName { get; set; }
    public string PropertyValue { get; set; }
    public Brush BackgroundColor { get; set; }
    
    public HvpNodePropertyViewModel(string propertyName, string propertyValue, Brush backgroundColor)
    {
        PropertyName = propertyName;
        PropertyValue = propertyValue;
        BackgroundColor = backgroundColor;
    }
}

/// <summary>
/// Color style information for coverage percentage display
/// </summary>
public class CoverageColorStyle
{
    public SolidColorBrush Foreground { get; set; }
    public SolidColorBrush Background { get; set; }
    
    public CoverageColorStyle(SolidColorBrush foreground, SolidColorBrush background)
    {
        Foreground = foreground;
        Background = background;
    }
}

/// <summary>
/// Utility class for creating color mapping based on coverage percentages
/// Uses a 10-color scheme (s0-s10) for different percentage ranges
/// </summary>
public static class CoverageColorMapping
{
    // Define the color styles based on the provided CSS
    private static readonly Dictionary<int, CoverageColorStyle> ColorStyles = new Dictionary<int, CoverageColorStyle>
    {
        { 0, new CoverageColorStyle(new SolidColorBrush(Color.FromRgb(255, 255, 255)), new SolidColorBrush(Color.FromRgb(204, 0, 0))) },    // s0: white text, dark red bg
        { 1, new CoverageColorStyle(new SolidColorBrush(Colors.White), new SolidColorBrush(Color.FromRgb(204, 0, 0))) },                   // s1: white text, dark red bg
        { 2, new CoverageColorStyle(new SolidColorBrush(Color.FromRgb(0, 0, 0)), new SolidColorBrush(Color.FromRgb(255, 0, 0))) },          // s2: black text, red bg
        { 3, new CoverageColorStyle(new SolidColorBrush(Color.FromRgb(0, 0, 0)), new SolidColorBrush(Color.FromRgb(255, 0, 0))) },          // s3: black text, red bg
        { 4, new CoverageColorStyle(new SolidColorBrush(Color.FromRgb(0, 0, 0)), new SolidColorBrush(Color.FromRgb(255, 153, 0))) },        // s4: black text, orange bg
        { 5, new CoverageColorStyle(new SolidColorBrush(Color.FromRgb(0, 0, 0)), new SolidColorBrush(Color.FromRgb(255, 153, 0))) },        // s5: black text, orange bg
        { 6, new CoverageColorStyle(new SolidColorBrush(Color.FromRgb(0, 0, 0)), new SolidColorBrush(Color.FromRgb(255, 255, 0))) },        // s6: black text, yellow bg
        { 7, new CoverageColorStyle(new SolidColorBrush(Color.FromRgb(0, 0, 0)), new SolidColorBrush(Color.FromRgb(255, 255, 0))) },        // s7: black text, yellow bg
        { 8, new CoverageColorStyle(new SolidColorBrush(Color.FromRgb(0, 0, 0)), new SolidColorBrush(Color.FromRgb(0, 255, 0))) },          // s8: black text, bright green bg
        { 9, new CoverageColorStyle(new SolidColorBrush(Color.FromRgb(0, 0, 0)), new SolidColorBrush(Color.FromRgb(0, 255, 0))) },          // s9: black text, bright green bg
        { 10, new CoverageColorStyle(new SolidColorBrush(Color.FromRgb(0, 0, 0)), new SolidColorBrush(Color.FromRgb(0, 187, 0))) }          // s10: black text, dark green bg
    };
    
    /// <summary>
    /// Gets the color style for a given percentage (0-100)
    /// Maps percentages to style classes: 0-10% = s0/s1, 10-20% = s2/s3, etc.
    /// </summary>
    /// <param name="percentage">Percentage value (0-100)</param>
    /// <returns>CoverageColorStyle with appropriate foreground and background colors</returns>
    public static CoverageColorStyle GetColorStyleForPercentage(double percentage)
    {
        // Clamp percentage to 0-100 range
        percentage = Math.Max(0, Math.Min(100, percentage));
        
        // Map percentage to style index (0-10)
        // 0-10% = s0/s1, 10-20% = s2/s3, 20-30% = s4/s5, etc.
        int styleIndex;
        if (percentage < 10)
        {
            styleIndex = percentage < 5 ? 0 : 1;  // 0-5% = s0, 5-10% = s1
        }
        else if (percentage >= 100)
        {
            styleIndex = 10;  // 100% = s10
        }
        else
        {
            // 10-99% maps to s2-s9
            // Every 10% gets 2 style classes, alternate between them
            int rangeIndex = (int)(percentage / 10);  // 1-9 for 10-99%
            styleIndex = rangeIndex * 2;  // s2, s4, s6, s8 for even ranges
            if (percentage % 10 >= 5)  // Use odd style for second half of range
            {
                styleIndex = Math.Min(9, styleIndex + 1);  // s3, s5, s7, s9
            }
            if (styleIndex < 2) styleIndex = 2;  // Ensure minimum s2 for >= 10%
        }
        
        return ColorStyles.TryGetValue(styleIndex, out var style) ? style : ColorStyles[0];
    }
    
    /// <summary>
    /// Legacy method for backward compatibility - returns only background brush
    /// </summary>
    /// <param name="percentage">Percentage value (0-100)</param>
    /// <returns>SolidColorBrush with appropriate background color</returns>
    public static SolidColorBrush GetColorForPercentage(double percentage)
    {
        return GetColorStyleForPercentage(percentage).Background;
    }
}

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private ProjectSettings? _currentProject;
    private HttpClient? _authenticatedHttpClient;
    private JiraApi? _jiraApi;
    private JsonNode? _jiraEpicTicket;
    private JsonNode? _jiraStoryTicket;
    private string _httpUsername = string.Empty;
    private string _httpPassword = string.Empty;
    private bool _statsLoaded = false;
    
    // Event handler tracking for proper cleanup
    private EventHandler<Microsoft.Web.WebView2.Core.CoreWebView2NavigationCompletedEventArgs>? _currentNavigationHandler;
    private EventHandler<Microsoft.Web.WebView2.Core.CoreWebView2DOMContentLoadedEventArgs>? _currentDOMHandler;
    
    // Jira browser event handler tracking
    private EventHandler<Microsoft.Web.WebView2.Core.CoreWebView2NavigationCompletedEventArgs>? _currentJiraNavigationHandler;
    private EventHandler<Microsoft.Web.WebView2.Core.CoreWebView2DOMContentLoadedEventArgs>? _currentJiraDOMHandler;
    
    // Multi-selection support for HVP tree
    private HashSet<TreeViewItem> _selectedTreeViewItems = new HashSet<TreeViewItem>();
    private HashSet<HierarchyNode> _selectedNodes = new HashSet<HierarchyNode>();
    
    // Multi-selection support for Stats tree
    private HashSet<TreeViewItem> _selectedStatsTreeViewItems = new HashSet<TreeViewItem>();
    private HashSet<object> _selectedStatsNodes = new HashSet<object>();
    
    // Project information for display in status bar
    public string ReleaseName { get; private set; } = string.Empty;
    public string CoverageType { get; private set; } = string.Empty;
    public string ReportName { get; private set; } = string.Empty;
    public string ReportType { get; private set; } = string.Empty;
    public string Changelist { get; private set; } = string.Empty;
    
    // Commands for keyboard shortcuts
    public ICommand ToggleOutputCommand { get; private set; }

    public MainWindow()
    {
        InitializeComponent();
        
        // Set DataContext for command bindings
        DataContext = this;
        
        // Initialize commands  
        ToggleOutputCommand = new RelayCommand(_ => {
            // Call the existing ToggleOutput_Click method
            ToggleOutput_Click(this, new RoutedEventArgs());
        });
        
        // Enable mouse wheel scrolling for TreeView and ScrollViewers
        HvpTreeView.PreviewMouseWheel += SolutionExplorer_PreviewMouseWheel;
        
        try
        {
            LogToFile("=== APPLICATION STARTUP ===");
            LogToFile($"MainWindow constructor started at {DateTime.Now}");
            
            AddToOutput("Welcome to Coverage Analyzer GUI");
            AddToOutput("Ready to create or open a project");
            AddToOutput("💡 Tip: Press Ctrl+` to toggle Output panel visibility");
            UpdateWindowTitle();
            
            // Initialize WebView2
            InitializeWebView();
            
            // Ensure window is visible and activated
            this.Show();
            this.Activate();
            this.WindowState = WindowState.Normal;
            this.Topmost = true;
            this.Topmost = false; // Reset topmost after activation
            
            // Ensure Output panel is visible after the layout is loaded
            this.Loaded += async (s, e) => {
                EnsureOutputPanelVisible();
                AddToOutput("✓ Application ready - Output panel should be visible");
                
                // Initialize Jira browser after UI is fully loaded (on UI thread)
                try 
                {
                    AddToOutput("🔄 Initializing Jira browser...", LogSeverity.DEBUG);
                    await InitializeJiraBrowser();
                }
                catch (Exception ex)
                {
                    AddToOutput($"❌ Jira Browser initialization failed: {ex.Message}", LogSeverity.ERROR);
                }
            };
            
            LogToFile("MainWindow initialization completed successfully");
        }
        catch (Exception ex)
        {
            AddToOutput($"Initialization error: {ex.Message}", LogSeverity.ERROR);
            MessageBox.Show($"Initialization error: {ex.Message}", "Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void InitializeWebView()
    {
        try
        {
            // Initialize WebView2 when ready - simplified version
            if (HvpBrowser != null)
            {
                await HvpBrowser.EnsureCoreWebView2Async();
                
                // Configure HTTP authentication for WebView2 if we have credentials
                ConfigureWebViewAuthentication();
                
                // Add Basic Authentication handler for automatic credential provision
                HvpBrowser.CoreWebView2.BasicAuthenticationRequested += CoreWebView2_BasicAuthenticationRequested;
                
                // Add navigation state change event handler
                HvpBrowser.CoreWebView2.HistoryChanged += (sender, args) =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        BackButton.IsEnabled = HvpBrowser.CoreWebView2.CanGoBack;
                        ForwardButton.IsEnabled = HvpBrowser.CoreWebView2.CanGoForward;
                    });
                };
                
                // Update address bar when source changes
                HvpBrowser.CoreWebView2.SourceChanged += (sender, args) =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        HvpAddressBar.Text = HvpBrowser.CoreWebView2.Source;
                    });
                };
                
                // Set initial welcome content instead of loading test HTML
                string welcomeHtml = @"
                <!DOCTYPE html>
                <html>
                <head>
                    <title>HVP Coverage Browser</title>
                    <style>
                        body { 
                            font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; 
                            margin: 0; padding: 40px; 
                            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
                            color: white; text-align: center; 
                        }
                        .container { 
                            max-width: 600px; margin: 0 auto; 
                            background: rgba(255,255,255,0.1); 
                            padding: 30px; border-radius: 15px; 
                            backdrop-filter: blur(10px);
                        }
                        h1 { font-size: 2.5em; margin-bottom: 20px; }
                        p { font-size: 1.2em; line-height: 1.6; margin-bottom: 20px; }
                        .steps { text-align: left; margin: 20px 0; }
                        .step { margin: 10px 0; padding: 10px; background: rgba(255,255,255,0.1); border-radius: 8px; }
                    </style>
                </head>
                <body>
                    <div class='container'>
                        <h1>🌐 HVP Coverage Browser</h1>
                        <p>Welcome to the HVP Coverage Analysis Tool!</p>
                        
                        <div class='steps'>
                            <div class='step'>
                                <strong>1. Create or Open Project</strong><br>
                                Use File → New Project or File → Open Project to get started
                            </div>
                            <div class='step'>
                                <strong>2. Automatic Loading</strong><br>
                                HVP data and reports will load automatically in this browser
                            </div>
                            <div class='step'>
                                <strong>3. Interactive Navigation</strong><br>
                                Click tree nodes to navigate to detailed coverage reports
                            </div>
                        </div>
                        
                        <p><em>Ready to analyze your HVP coverage data! 🚀</em></p>
                    </div>
                </body>
                </html>";
                
                HvpBrowser.NavigateToString(welcomeHtml);
                LogToFile("WebView2 initialized with welcome content");
            }
        }
        catch (Exception ex)
        {
            LogToFile($"WebView2 initialization error: {ex.Message}");
            AddToOutput($"HTML browser initialization error: {ex.Message}", LogSeverity.ERROR);
        }
    }

    /// <summary>
    /// Get Windows user credentials with domain stripped from username
    /// </summary>
    private (string username, string domain) GetWindowsCredentials()
    {
        try
        {
            string fullUsername = Environment.UserName;
            string domain = Environment.UserDomainName;
            
            // Strip domain from username if it contains domain\username format
            string cleanUsername = fullUsername;
            if (fullUsername.Contains("\\"))
            {
                cleanUsername = fullUsername.Split('\\').Last();
            }
            else if (fullUsername.Contains("@"))
            {
                cleanUsername = fullUsername.Split('@').First();
            }

            return (cleanUsername, domain);
        }
        catch (Exception ex)
        {
            AddToOutput($"Error getting Windows credentials: {ex.Message}", LogSeverity.ERROR);
            return (Environment.UserName, "");
        }
    }
    
    /// <summary>
    /// Attempts to authenticate using Windows credentials automatically
    /// Tests authentication for both HVP and Jira servers if available
    /// </summary>
    private async Task<(bool success, HttpClient? httpClient)> TryWindowsAuthentication(string serverUrl)
    {
        try
        {
            // Create HttpClient with Windows integrated authentication
            // DefaultNetworkCredentials automatically handles domain/username
            var handler = new HttpClientHandler()
            {
                UseDefaultCredentials = true,
                Credentials = System.Net.CredentialCache.DefaultNetworkCredentials
            };
            
            var httpClient = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(10) // Quick timeout for automatic auth test
            };
            
            // Test the authentication by making a simple request to the primary server
            try
            {
                var testResponse = await httpClient.GetAsync(serverUrl);
                
                if (testResponse.IsSuccessStatusCode || testResponse.StatusCode != System.Net.HttpStatusCode.Unauthorized)
                {
                    AddToOutput($"✅ Windows authentication successful for {serverUrl}", LogSeverity.INFO);
                    
                    // Also test Jira server if it's different and available
                    if (_currentProject != null && !string.IsNullOrEmpty(_currentProject.JiraServer))
                    {
                        try
                        {
                            var jiraResponse = await httpClient.GetAsync(_currentProject.JiraServer);
                            if (jiraResponse.IsSuccessStatusCode || jiraResponse.StatusCode != System.Net.HttpStatusCode.Unauthorized)
                            {
                                AddToOutput($"✅ Windows authentication also works for Jira server: {_currentProject.JiraServer}", LogSeverity.INFO);
                            }
                            else
                            {
                                AddToOutput($"⚠️ Windows authentication failed for Jira server: {_currentProject.JiraServer}", LogSeverity.WARNING);
                            }
                        }
                        catch (Exception jiraEx)
                        {
                            AddToOutput($"⚠️ Could not test Jira server authentication: {jiraEx.Message}", LogSeverity.DEBUG);
                        }
                    }
                    
                    httpClient.Timeout = TimeSpan.FromSeconds(30); // Set normal timeout
                    return (true, httpClient);
                }
                else
                {
                    httpClient.Dispose();
                    return (false, null);
                }
            }
            catch (HttpRequestException)
            {
                httpClient.Dispose();
                return (false, null);
            }
        }
        catch (Exception ex)
        {
            AddToOutput($"Error in Windows authentication: {ex.Message}", LogSeverity.ERROR);
            return (false, null);
        }
    }

    /// <summary>
    /// Validates that the authenticated HttpClient works for both HVP and Jira servers
    /// </summary>
    private async Task<bool> ValidateDualServerAuthentication()
    {
        if (_authenticatedHttpClient == null || _currentProject == null)
        {
            return false;
        }

        var results = new List<(string serverName, string url, bool success)>();

        // Test HVP server if available
        if (!string.IsNullOrEmpty(_currentProject.HvpTop))
        {
            try
            {
                var hvpResponse = await _authenticatedHttpClient.GetAsync(_currentProject.HvpTop);
                var hvpSuccess = hvpResponse.IsSuccessStatusCode || hvpResponse.StatusCode != System.Net.HttpStatusCode.Unauthorized;
                results.Add(("HVP", _currentProject.HvpTop, hvpSuccess));
                AddToOutput($"{(hvpSuccess ? "✅" : "❌")} Authentication test for HVP server: {_currentProject.HvpTop}", LogSeverity.DEBUG);
            }
            catch (Exception ex)
            {
                results.Add(("HVP", _currentProject.HvpTop, false));
                AddToOutput($"❌ HVP server test failed: {ex.Message}", LogSeverity.DEBUG);
            }
        }

        // Test Jira server if available
        if (!string.IsNullOrEmpty(_currentProject.JiraServer))
        {
            try
            {
                var jiraResponse = await _authenticatedHttpClient.GetAsync(_currentProject.JiraServer);
                var jiraSuccess = jiraResponse.IsSuccessStatusCode || jiraResponse.StatusCode != System.Net.HttpStatusCode.Unauthorized;
                results.Add(("Jira", _currentProject.JiraServer, jiraSuccess));
                AddToOutput($"{(jiraSuccess ? "✅" : "❌")} Authentication test for Jira server: {_currentProject.JiraServer}", LogSeverity.DEBUG);
            }
            catch (Exception ex)
            {
                results.Add(("Jira", _currentProject.JiraServer, false));
                AddToOutput($"❌ Jira server test failed: {ex.Message}", LogSeverity.DEBUG);
            }
        }

        var successfulTests = results.Where(r => r.success).ToList();
        var failedTests = results.Where(r => !r.success).ToList();

        if (successfulTests.Any())
        {
            AddToOutput($"✅ Authentication successful for: {string.Join(", ", successfulTests.Select(s => s.serverName))}", LogSeverity.INFO);
        }

        if (failedTests.Any())
        {
            AddToOutput($"⚠️ Authentication failed for: {string.Join(", ", failedTests.Select(f => f.serverName))}", LogSeverity.WARNING);
        }

        return results.All(r => r.success);
    }

    /// <summary>
    /// Handle Basic Authentication requests from WebView2 automatically
    /// </summary>
    private void CoreWebView2_BasicAuthenticationRequested(object? sender, Microsoft.Web.WebView2.Core.CoreWebView2BasicAuthenticationRequestedEventArgs e)
    {
        try
        {
            if (_authenticatedHttpClient?.DefaultRequestHeaders.Authorization != null)
            {
                var authHeader = _authenticatedHttpClient.DefaultRequestHeaders.Authorization;
                if (authHeader.Scheme == "Basic" && !string.IsNullOrEmpty(authHeader.Parameter))
                {
                    // Decode the base64 credentials to get username and password
                    var credentialBytes = Convert.FromBase64String(authHeader.Parameter);
                    var credential = Encoding.UTF8.GetString(credentialBytes);
                    var parts = credential.Split(':', 2);
                    
                    if (parts.Length == 2)
                    {
                        var username = parts[0];
                        var password = parts[1];
                        
                        AddToOutput($"🔐 WebView2 auto-authenticating as user: {username}", LogSeverity.INFO);
                        
                        // Provide credentials automatically
                        e.Response.UserName = username;
                        e.Response.Password = password;
                        
                        return;
                    }
                }
            }
            
            AddToOutput("❌ No valid credentials available for WebView2 authentication", LogSeverity.WARNING);
        }
        catch (Exception ex)
        {
            AddToOutput($"Error handling WebView2 authentication: {ex.Message}", LogSeverity.ERROR);
        }
    }

    /// <summary>
    /// Configure WebView2 to use the same HTTP authentication as our HttpClient
    /// </summary>
    private void ConfigureWebViewAuthentication()
    {
        try
        {
            if (HvpBrowser?.CoreWebView2 != null && _authenticatedHttpClient?.DefaultRequestHeaders.Authorization != null)
            {
                var authHeader = _authenticatedHttpClient.DefaultRequestHeaders.Authorization;
                if (authHeader.Scheme == "Basic" && !string.IsNullOrEmpty(authHeader.Parameter))
                {
                    // Decode the base64 credentials to get username and password
                    var credentialBytes = Convert.FromBase64String(authHeader.Parameter);
                    var credential = Encoding.UTF8.GetString(credentialBytes);
                    var parts = credential.Split(':', 2);
                    
                    if (parts.Length == 2)
                    {
                        var username = parts[0];
                        var password = parts[1];
                        
                        AddToOutput($"🔐 Configuring WebView2 auto-authentication for user: {username}", LogSeverity.INFO);
                        
                        // Set up comprehensive authentication handling
                        ConfigureWebView2Authentication(username, password, authHeader.Parameter);
                    }
                    else
                    {
                        AddToOutput("❌ Invalid credential format in Authorization header", LogSeverity.ERROR);
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
        catch (Exception ex)
        {
            AddToOutput($"Error configuring WebView2 authentication: {ex.Message}", LogSeverity.ERROR);
        }
    }

    /// <summary>
    /// Configure comprehensive WebView2 authentication using JavaScript injection for both HVP and Jira servers
    /// </summary>
    private void ConfigureWebView2Authentication(string username, string password, string encodedCredentials)
    {
        try
        {
            var coreWebView2 = HvpBrowser.CoreWebView2;
            
            // Get server URLs for authentication
            var hvpServerUrl = !string.IsNullOrEmpty(_currentProject?.HttpServerUrl) ? _currentProject.HttpServerUrl : "";
            var jiraServerUrl = !string.IsNullOrEmpty(_currentProject?.JiraServer) ? _currentProject.JiraServer : "";
            
            AddToOutput($"🔐 Configuring WebView2 authentication for HVP: {hvpServerUrl}, Jira: {jiraServerUrl}", LogSeverity.DEBUG);
            
            // Use JavaScript injection to handle authentication automatically
            coreWebView2.DOMContentLoaded += async (sender, args) =>
            {
                try
                {
                    // Inject comprehensive authentication handling for both HVP and Jira
                    var script = $@"
                        (function() {{
                            // Store credentials for automatic authentication on both HVP and Jira servers
                            window._webview2Auth = {{
                                header: 'Basic {encodedCredentials}',
                                username: '{username}',
                                password: '{password}',
                                enabled: true,
                                hvpServer: '{hvpServerUrl}',
                                jiraServer: '{jiraServerUrl}'
                            }};
                            
                            console.log('WebView2: Auth credentials stored for HVP and Jira servers');
                            
                            // Override XMLHttpRequest to add auth headers automatically
                            const originalOpen = XMLHttpRequest.prototype.open;
                            XMLHttpRequest.prototype.open = function(method, url, async, user, password) {{
                                this._method = method;
                                this._url = url;
                                return originalOpen.call(this, method, url, async, user, password);
                            }};
                            
                            // Helper function to check if URL needs authentication (HVP or Jira server)
                            const needsAuth = function(url) {{
                                if (!url || (!url.startsWith('http://') && !url.startsWith('https://'))) {{
                                    return false;
                                }}
                                const auth = window._webview2Auth;
                                if (!auth || !auth.enabled) return false;
                                
                                // Check if URL matches HVP or Jira server
                                return (auth.hvpServer && url.includes(auth.hvpServer.replace('https://', '').replace('http://', ''))) ||
                                       (auth.jiraServer && url.includes(auth.jiraServer.replace('https://', '').replace('http://', '')));
                            }};
                            
                            const originalSend = XMLHttpRequest.prototype.send;
                            XMLHttpRequest.prototype.send = function(data) {{
                                if (needsAuth(this._url)) {{
                                    this.setRequestHeader('Authorization', window._webview2Auth.header);
                                    console.log('WebView2: Auto-auth header added to XMLHttpRequest:', this._url);
                                }}
                                return originalSend.call(this, data);
                            }};
                            
                            // Override fetch to add auth headers automatically for both servers
                            const originalFetch = window.fetch;
                            window.fetch = function(input, init = {{}}) {{
                                const url = typeof input === 'string' ? input : input.url;
                                if (needsAuth(url)) {{
                                    init.headers = init.headers || {{}};
                                    init.headers['Authorization'] = window._webview2Auth.header;
                                    console.log('WebView2: Auto-auth header added to fetch:', url);
                                }}
                                return originalFetch.call(this, input, init);
                            }};
                            
                            // Handle authentication dialogs automatically for both HVP and Jira
                            const handleAuthDialog = function() {{
                                // Skip if not on an authenticated server
                                if (!needsAuth(window.location.href)) {{
                                    return;
                                }}
                                
                                // Look for authentication forms and auto-fill them (works for both HVP and Jira)
                                const usernameFields = document.querySelectorAll(
                                    'input[type=""text""], input[type=""email""], input[name*=""user""], input[id*=""user""], ' +
                                    'input[name*=""login""], input[id*=""login""], input[placeholder*=""user""], input[placeholder*=""email""]'
                                );
                                const passwordFields = document.querySelectorAll(
                                    'input[type=""password""], input[name*=""pass""], input[id*=""pass""]'
                                );
                                
                                if (usernameFields.length > 0 && passwordFields.length > 0) {{
                                    console.log('WebView2: Auto-filling auth form for', window.location.hostname);
                                    usernameFields[0].value = window._webview2Auth.username;
                                    passwordFields[0].value = window._webview2Auth.password;
                                    
                                    // Try to submit the form (works for both HVP and Jira login forms)
                                    const form = usernameFields[0].closest('form');
                                    if (form) {{
                                        const submitBtn = form.querySelector(
                                            'button[type=""submit""], input[type=""submit""], ' +
                                            'button[class*=""login""], button[id*=""login""], ' +
                                            'input[value*=""Login""], input[value*=""Sign""]'
                                        );
                                        if (submitBtn) {{
                                            console.log('WebView2: Auto-submitting authentication form for', window.location.hostname);
                                            setTimeout(() => submitBtn.click(), 200); // Small delay for form validation
                                        }}
                                    }}
                                }}
                            }};
                            
                            // Check for auth dialogs on page load and mutations
                            setTimeout(handleAuthDialog, 100);
                            
                            if (typeof MutationObserver !== 'undefined') {{
                                const observer = new MutationObserver(function(mutations) {{
                                    mutations.forEach(function(mutation) {{
                                        if (mutation.addedNodes.length > 0) {{
                                            setTimeout(handleAuthDialog, 50);
                                        }}
                                    }});
                                }});
                                
                                observer.observe(document.body, {{
                                    childList: true,
                                    subtree: true
                                }});
                            }}
                            
                            console.log('WebView2: Comprehensive authentication system initialized for both HVP and Jira servers');
                        }})();
                    ";
                    
                    await coreWebView2.ExecuteScriptAsync(script);
                    AddToOutput("✅ WebView2 auto-authentication system activated for both HVP and Jira servers", LogSeverity.INFO);
                }
                catch (Exception ex)
                {
                    AddToOutput($"Error setting up WebView2 auto-authentication: {ex.Message}", LogSeverity.ERROR);
                }
            };
            
            // Handle navigation events for logging
            coreWebView2.NavigationStarting += (sender, args) =>
            {
                try
                {
                    if (args.Uri.StartsWith("http://") || args.Uri.StartsWith("https://"))
                    {

                    }
                }
                catch (Exception ex)
                {
                    AddToOutput($"Error in NavigationStarting: {ex.Message}", LogSeverity.ERROR);
                }
            };
            
            AddToOutput($"✅ WebView2 authentication configured for user: {username} (HVP + Jira servers)", LogSeverity.INFO);
        }
        catch (Exception ex)
        {
            AddToOutput($"Error setting up WebView2 authentication: {ex.Message}", LogSeverity.ERROR);
        }
    }

    private void NavigateToHvpReport(string reportPath)
    {
        try
        {
            if (HvpBrowser?.CoreWebView2 != null && !string.IsNullOrEmpty(reportPath))
            {
                var startTime = DateTime.Now;
                
                // Update progress
                StatusText.Text = $"Loading HVP file... 75%";
                OperationProgress.Value = 75;
                
                // Check if it's a relative path and make it absolute if needed
                if (!Path.IsPathRooted(reportPath))
                {
                    reportPath = Path.GetFullPath(reportPath);
                }

                // Convert to file URI
                string fileUri = $"file:///{reportPath.Replace('\\', '/')}";
                
                // Add navigation completed handler for local files
                HvpBrowser.CoreWebView2.NavigationCompleted += (sender, args) =>
                {
                    var duration = DateTime.Now - startTime;
                    if (args.IsSuccess)
                    {
                        AddToOutput($"✅ HVP file loaded in {duration.TotalSeconds:F1} seconds", LogSeverity.INFO);
                        Dispatcher.Invoke(() => 
                        {
                            StatusText.Text = $"HVP file loaded successfully! 100%";
                            OperationProgress.Value = 100;
                            
                            // Hide progress after showing completion
                            Task.Delay(2000).ContinueWith(_ => 
                            {
                                Dispatcher.Invoke(() => 
                                {
                                    OperationProgress.Visibility = Visibility.Collapsed;
                                    StatusText.Text = "Ready";
                                });
                            });
                        });
                    }
                    else
                    {
                        AddToOutput($"❌ HVP file loading failed after {duration.TotalSeconds:F1} seconds", LogSeverity.ERROR);
                        Dispatcher.Invoke(() => 
                        {
                            OperationProgress.Visibility = Visibility.Collapsed;
                            StatusText.Text = "HVP loading failed";
                        });
                    }
                };
                
                HvpBrowser.CoreWebView2.Navigate(fileUri);
                
                LogToFile($"Navigating WebView to HVP report: {fileUri}");
                AddToOutput($"Loading HVP report: {Path.GetFileName(reportPath)}");
            }
        }
        catch (Exception ex)
        {
            LogToFile($"Error navigating to HVP report: {ex.Message}");
            AddToOutput($"Error loading HVP report: {ex.Message}", LogSeverity.ERROR);
            
            // Hide progress on error
            OperationProgress.Visibility = Visibility.Collapsed;
            StatusText.Text = "Error loading HVP";
        }
    }

    private void LoadTestReportButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // Load the test HTML file
            var testHtmlPath = Path.Combine(Directory.GetCurrentDirectory(), "..", "test_hvp_report.html");
            if (File.Exists(testHtmlPath))
            {
                NavigateToHvpReport(testHtmlPath);
                AddToOutput($"🌐 Loaded test HVP report from: {testHtmlPath}");
            }
            else
            {
                AddToOutput($"❌ Test report not found at: {testHtmlPath}", LogSeverity.ERROR);
                
                // Try alternative path
                var altPath = Path.Combine(Environment.CurrentDirectory, "test_hvp_report.html");
                if (File.Exists(altPath))
                {
                    NavigateToHvpReport(altPath);
                    AddToOutput($"🌐 Loaded test HVP report from alternative path: {altPath}");
                }
                else
                {
                    AddToOutput($"❌ Test report also not found at: {altPath}", LogSeverity.ERROR);
                }
            }
        }
        catch (Exception ex)
        {
            AddToOutput($"Error loading test report: {ex.Message}", LogSeverity.ERROR);
        }
    }

    private void BackButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (HvpBrowser?.CoreWebView2 != null && HvpBrowser.CoreWebView2.CanGoBack)
            {
                HvpBrowser.CoreWebView2.GoBack();
                AddToOutput("🔙 Navigated back in browser");
            }
            else
            {
                AddToOutput("⚠ Cannot go back - no previous page", LogSeverity.WARNING);
            }
        }
        catch (Exception ex)
        {
            AddToOutput($"Error navigating back: {ex.Message}", LogSeverity.ERROR);
        }
    }

    private void ForwardButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (HvpBrowser?.CoreWebView2 != null && HvpBrowser.CoreWebView2.CanGoForward)
            {
                HvpBrowser.CoreWebView2.GoForward();
                AddToOutput("🔜 Navigated forward in browser");
            }
            else
            {
                AddToOutput("⚠ Cannot go forward - no next page", LogSeverity.WARNING);
            }
        }
        catch (Exception ex)
        {
            AddToOutput($"Error navigating forward: {ex.Message}", LogSeverity.ERROR);
        }
    }

    private void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (HvpBrowser?.CoreWebView2 != null)
            {
                HvpBrowser.CoreWebView2.Reload();
                AddToOutput("🔄 Browser refreshed");
            }
            else
            {
                AddToOutput("⚠ Browser not initialized", LogSeverity.WARNING);
            }
        }
        catch (Exception ex)
        {
            AddToOutput($"Error refreshing browser: {ex.Message}", LogSeverity.ERROR);
        }
    }

    private async Task LoadHvpTopInBrowser()
    {
        try
        {
            if (_currentProject == null || string.IsNullOrEmpty(_currentProject.HvpTop))
            {
                AddToOutput("⚠ No HVPTop path configured for browser navigation", LogSeverity.WARNING);
                return;
            }

            // Show progress indicator for HVPTop loading
            OperationProgress.Visibility = Visibility.Visible;
            OperationProgress.IsIndeterminate = false;
            OperationProgress.Value = 10;
            StatusText.Text = "Initializing HVPTop... 10%";

            // Wait for WebView2 to be ready
            if (HvpBrowser?.CoreWebView2 == null)
            {
                await HvpBrowser?.EnsureCoreWebView2Async()!;
                OperationProgress.Value = 30;
                StatusText.Text = "Browser ready... 30%";
            }

            string hvpTopPath = _currentProject.HvpTop;
            
            OperationProgress.Value = 50;
            StatusText.Text = "Loading HVPTop... 50%";
            
            // Handle different URL types
            if (hvpTopPath.StartsWith("http://") || hvpTopPath.StartsWith("https://"))
            {
                // Direct HTTP/HTTPS URL
                var startTime = DateTime.Now;
                bool isCompleted = false;
                
                // Clean up previous handlers to prevent multiple firing
                if (_currentNavigationHandler != null)
                {
                    HvpBrowser.CoreWebView2.NavigationCompleted -= _currentNavigationHandler;
                }
                if (_currentDOMHandler != null)
                {
                    HvpBrowser.CoreWebView2.DOMContentLoaded -= _currentDOMHandler;
                }
                
                // Add DOM content loaded handler for better timing
                _currentDOMHandler = (sender, args) =>
                {
                    if (!isCompleted)
                    {
                        isCompleted = true;
                        var duration = DateTime.Now - startTime;
                        Dispatcher.Invoke(() => 
                        {
                            AddToOutput($"✅ HVPTop content loaded in {duration.TotalSeconds:F1} seconds", LogSeverity.INFO);
                            StatusText.Text = "HVPTop loaded! 100%";
                            OperationProgress.Value = 100;
                            
                            // Hide progress after delay
                            Task.Delay(2000).ContinueWith(_ => 
                            {
                                Dispatcher.Invoke(() => 
                                {
                                    OperationProgress.Visibility = Visibility.Collapsed;
                                    StatusText.Text = "Ready";
                                });
                            });
                        });
                    }
                };
                HvpBrowser.CoreWebView2.DOMContentLoaded += _currentDOMHandler;
                
                // Add navigation completed handler as fallback
                _currentNavigationHandler = (sender, args) =>
                {
                    var duration = DateTime.Now - startTime;
                    
                    if (!args.IsSuccess && !isCompleted)
                    {
                        isCompleted = true;
                        Dispatcher.Invoke(() => 
                        {
                            AddToOutput($"❌ HVPTop navigation failed after {duration.TotalSeconds:F1} seconds", LogSeverity.ERROR);
                            AddToOutput($"💡 Note: Page content may still be loading via redirects or JavaScript", LogSeverity.INFO);
                            StatusText.Text = "HVPTop navigation failed (content may still load)";
                            
                            // Don't hide progress immediately on navigation failure - content might still load
                            OperationProgress.Value = 75;
                        });
                    }
                    else if (args.IsSuccess && duration.TotalSeconds < 0.5)
                    {
                        // Very fast navigation usually means redirect or initial load - wait for DOM
                        Dispatcher.Invoke(() => 
                        {
                            StatusText.Text = "HVPTop navigated, waiting for content... 80%";
                            OperationProgress.Value = 80;
                        });
                    }
                };
                HvpBrowser.CoreWebView2.NavigationCompleted += _currentNavigationHandler;
                
                HvpBrowser.CoreWebView2.Navigate(hvpTopPath);
                AddToOutput($"🌐 Loading HVPTop from URL: {hvpTopPath}");
            }
            else
            {
                // Local file path
                string fullPath = Path.IsPathRooted(hvpTopPath) ? hvpTopPath : Path.GetFullPath(hvpTopPath);
                
                if (File.Exists(fullPath))
                {
                    string fileUri = $"file:///{fullPath.Replace('\\', '/')}";
                    HvpBrowser.CoreWebView2.Navigate(fileUri);
                    AddToOutput($"🌐 Loading HVPTop from file: {fileUri}");
                }
                else
                {
                    AddToOutput($"❌ HVPTop file not found: {fullPath}", LogSeverity.ERROR);
                    
                    // Fallback to test report
                    var testHtmlPath = Path.Combine(Directory.GetCurrentDirectory(), "..", "test_hvp_report.html");
                    if (File.Exists(testHtmlPath))
                    {
                        NavigateToHvpReport(testHtmlPath);
                        AddToOutput($"🌐 Fallback: Loaded test report instead: {testHtmlPath}");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            AddToOutput($"Error loading HVPTop in browser: {ex.Message}", LogSeverity.ERROR);
            
            // Hide progress on error
            OperationProgress.Visibility = Visibility.Collapsed;
            StatusText.Text = "Error loading HVPTop";
        }
    }

    private void LoadCoverageData_Click(object sender, RoutedEventArgs e)
    {
        StatusText.Text = "Loading coverage data...";
        AddToOutput("Load Coverage Data command executed.");
        
        try
        {
            // Open file dialog to select coverage data file
            var openFileDialog = new OpenFileDialog
            {
                Title = "Select Coverage Data File",
                Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*",
                DefaultExt = "txt"
            };
            
            if (openFileDialog.ShowDialog() != true)
            {
                StatusText.Text = "Coverage data loading cancelled";
                AddToOutput("Coverage data loading cancelled by user");
                return;
            }
            
            string coverageFilePath = openFileDialog.FileName;
            AddToOutput($"Loading coverage data from: {coverageFilePath}");
            
            // Check if file exists
            if (!File.Exists(coverageFilePath))
            {
                string error = $"ERROR: Coverage file not found at {coverageFilePath}";
                AddToOutput(error);
                StatusText.Text = "Error: Coverage file not found";
                return;
            }
            
            // Read and parse the coverage file
            LogToFile("Reading coverage file...");
            
            var hierarchyData = ParseHierarchyFile(coverageFilePath);
            LogToFile($"Parsed {hierarchyData.Count} hierarchy entries");
            
            // Build hierarchy tree from parsed data
            var rootHierarchy = BuildHierarchyFromParserData(hierarchyData);
            LogToFile($"Root hierarchy has {rootHierarchy.Children.Count} children");
            
            // Update UI on main thread
            Dispatcher.Invoke(() =>
            {
                // Clear existing items and set data source
                HvpTreeView.Items.Clear();
                rootHierarchy.IsExpanded = true;
                
                // Use data binding instead of manual TreeViewItem creation
                HvpTreeView.ItemsSource = new List<HierarchyNode> { rootHierarchy };
                
                LogToFile("TreeView updated with hierarchy data using data binding");
            });
            
            LogToFile("✅ Hierarchy loaded successfully");
            AddToOutput("✓ Hierarchy loaded from coverage file");
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
        public string? Link { get; set; }
        public double CoveragePercentage { get; set; }
        public int LinesCovered { get; set; }
        public int TotalLines { get; set; }

        public HierarchyEntry(string name, string path, double coverage, int linesCovered, int totalLines, string? link = null)
        {
            Name = name;
            Path = path;
            Link = link;
            CoveragePercentage = coverage;
            LinesCovered = linesCovered;
            TotalLines = totalLines;
        }
    }

    /// <summary>
    /// Parses the coverage file and returns hierarchy entries
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
                hierarchyNode.Link = entry.Link;

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
#if DEBUG
        // Only log to file in DEBUG builds to improve performance
        LogToFile($"OUTPUT-{severity}: {message}");
#endif

        // Filter out navigation messages (both INFO and ERROR level)
        if (message.Contains("✅ Navigation completed") || 
            message.Contains("❌ Navigation failed"))
        {
            // Skip these verbose navigation messages entirely
            return;
        }

        // Show all message types (INFO, WARNING, ERROR)
        var showMessage = true;

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
        
        // Display ReportPath for debugging hyperlink issues
        if (_currentProject != null && !string.IsNullOrEmpty(_currentProject.ReportPath))
        {
            AddToOutput($"ReportPath: {_currentProject.ReportPath}");
        }
        else
        {
            AddToOutput("ReportPath: Not configured");
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
            
            // Include Jira information if available
            var jiraInfo = "";
            if (_currentProject != null)
            {
                var jiraParts = new List<string>();
                
                if (!string.IsNullOrEmpty(_currentProject.JiraProject))
                    jiraParts.Add($"Project: {_currentProject.JiraProject}");
                    
                if (!string.IsNullOrEmpty(_currentProject.JiraEpic))
                    jiraParts.Add($"Epic: {_currentProject.JiraEpic}");
                    
                if (!string.IsNullOrEmpty(_currentProject.JiraStory))
                    jiraParts.Add($"Story: {_currentProject.JiraStory}");
                
                if (jiraParts.Count > 0)
                {
                    jiraInfo = $" | 🎫 {string.Join(" | ", jiraParts)}";
                }
            }
            
            ProjectInfoText.Text = $"Release: {ReleaseName} | Coverage: {displayCoverage} | Report: {ReportName} | Type: {displayReportType} | CL: {Changelist}{jiraInfo}";
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
    private async void NewProject_Click(object sender, RoutedEventArgs e)
    {
        var wizard = new ProjectWizard(this)
        {
            Owner = this
        };

        if (wizard.ShowDialog() == true && wizard.CompletedProject != null)
        {
            _currentProject = wizard.CompletedProject;
            _statsLoaded = false; // Reset stats loaded flag for new project
            
            // Ensure HTTP authentication is available for new project
            await EnsureHttpAuthenticationForProject();
            
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

    private async void OpenProject_Click(object sender, RoutedEventArgs e)
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
                _statsLoaded = false; // Reset stats loaded flag for loaded project
                
                // Debug: Show which project file was loaded and the Jira settings
                var projectJsonPath = Path.Combine(dialog.FolderName, "project.json");
                AddToOutput($"📂 Loading project from: {projectJsonPath}", LogSeverity.DEBUG);
                AddToOutput($"📂 Loaded project - JiraServer: '{_currentProject.JiraServer}'", LogSeverity.DEBUG);
                AddToOutput($"📂 Loaded project - JiraProject: '{_currentProject.JiraProject}'", LogSeverity.DEBUG);
                AddToOutput($"📂 Loaded project - JiraEpic: '{_currentProject.JiraEpic}'", LogSeverity.DEBUG);
                AddToOutput($"📂 Loaded project - JiraStory: '{_currentProject.JiraStory}'", LogSeverity.DEBUG);
                
                // Also show if the JSON file exists and has content
                if (File.Exists(projectJsonPath))
                {
                    var jsonContent = File.ReadAllText(projectJsonPath);
                    var hasJiraSettings = jsonContent.Contains("jiraServer") || jsonContent.Contains("jiraProject");
                    AddToOutput($"📂 Project JSON file exists ({jsonContent.Length} chars), Contains Jira settings: {hasJiraSettings}", LogSeverity.DEBUG);
                }
                
                // Ensure HTTP authentication is available for opened project
                await EnsureHttpAuthenticationForProject();
                
                // Debug: Show what ReportPath was loaded from JSON


                
                // Verify ReportPath contains the changelist (it should be correct from JSON)
                if (!string.IsNullOrEmpty(_currentProject.ReportPath) && 
                    !string.IsNullOrEmpty(_currentProject.SelectedChangelist))
                {
                    if (_currentProject.ReportPath.Contains(_currentProject.SelectedChangelist))
                    {

                    }
                    else
                    {
                        AddToOutput($"⚠️ ReportPath doesn't contain changelist {_currentProject.SelectedChangelist}", LogSeverity.WARNING);
                    }
                }
                
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
        
        AddToOutput("Project loaded successfully");
        
        // Check if we need HTTP authentication first
        if (RequiresHttpAuthentication() && _authenticatedHttpClient == null)
        {
            AddToOutput("🔐 Project requires HTTP authentication - setting up credentials...");
            await PromptForHttpCredentials();
            
            // If authentication was cancelled or failed, don't auto-load
            if (_authenticatedHttpClient == null)
            {
                AddToOutput("⚠ Authentication not configured - skipping auto-load");
                AddToOutput("💡 Use 'File > Test HVP TreeView' to manually load with authentication");
                return;
            }
        }
        
        AddToOutput("✓ Project ready. Auto-loading HVP data...");
        
        // Run JiraApi creation and HVP data loading in parallel for better performance
        var jiraTask = Task.Run(() => CreateJiraApiObject());
        var hvpTask = AutoLoadHvpData();
        
        // Wait for both operations to complete
        await Task.WhenAll(jiraTask, hvpTask);
        
        AddToOutput("✓ Project loading completed (HVP + Jira parallel processing)");
        
        // Ensure Output panel is visible after loading
        EnsureOutputPanelVisible();
        
        // Load Jira server automatically after authentication is available
        // Call on UI thread to avoid thread ownership issues
        _ = LoadJiraServerInBrowser();
    }

    #region Jira Browser Methods

    /// <summary>
    /// Initialize the Jira Browser WebView2 control
    /// </summary>
    private async Task InitializeJiraBrowser()
    {
        try
        {
            if (JiraBrowser != null)
            {
                AddToOutput("🔄 Starting Jira browser initialization...", LogSeverity.DEBUG);
                
                // Initialize WebView2 on UI thread
                await JiraBrowser.EnsureCoreWebView2Async();
                AddToOutput("✓ Jira CoreWebView2 initialized", LogSeverity.DEBUG);
                
                // Configure authentication for the Jira browser
                JiraBrowser.CoreWebView2.BasicAuthenticationRequested += CoreWebView2_BasicAuthenticationRequested;
                
                // Update Jira navigation buttons and address bar when history changes
                JiraBrowser.CoreWebView2.HistoryChanged += (sender, args) =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        JiraBackButton.IsEnabled = JiraBrowser.CoreWebView2.CanGoBack;
                        JiraForwardButton.IsEnabled = JiraBrowser.CoreWebView2.CanGoForward;
                    });
                };
                
                // Update address bar when source changes
                JiraBrowser.CoreWebView2.SourceChanged += (sender, args) =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        JiraAddressBar.Text = JiraBrowser.CoreWebView2.Source;
                    });
                };

                    // Set beautiful welcome content for Jira browser (matching HVP style)
                    string jiraWelcomeHtml = @"
<!DOCTYPE html>
<html>
<head>
    <title>Jira Browser</title>
    <style>
        body { 
            font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; 
            margin: 0; padding: 40px; 
            background: linear-gradient(135deg, #0052CC 0%, #2684FF 100%);
            color: white; text-align: center; 
        }
        .container { 
            max-width: 600px; margin: 0 auto; 
            background: rgba(255,255,255,0.1); 
            padding: 30px; border-radius: 15px; 
            backdrop-filter: blur(10px);
        }
        h1 { font-size: 2.5em; margin-bottom: 20px; }
        p { font-size: 1.2em; line-height: 1.6; margin-bottom: 20px; }
        .steps { text-align: left; margin: 20px 0; }
        .step { margin: 10px 0; padding: 10px; background: rgba(255,255,255,0.1); border-radius: 8px; }
    </style>
</head>
<body>
    <div class='container'>
        <h1>🎫 Jira Browser</h1>
        <p>Welcome to the integrated Jira workspace!</p>
        
        <div class='steps'>
            <div class='step'>
                <strong>1. Project Setup</strong><br>
                Create or open a project with Jira server configuration
            </div>
            <div class='step'>
                <strong>2. Automatic Loading</strong><br>
                Jira server loads automatically when you open a project
            </div>
            <div class='step'>
                <strong>3. Seamless Integration</strong><br>
                Create and track Jira issues directly from coverage data
            </div>
        </div>
        
        <p><em>Ready to manage your project workflow! 🚀</em></p>
    </div>
</body>
</html>";
                
                AddToOutput("🌐 Navigating to Jira welcome page...", LogSeverity.DEBUG);
                JiraBrowser.NavigateToString(jiraWelcomeHtml);
                
                AddToOutput("✨ Jira browser initialized with beautiful welcome message", LogSeverity.INFO);
            }
            else
            {
                AddToOutput("⚠️ JiraBrowser control is null!", LogSeverity.ERROR);
            }
        }
        catch (Exception ex)
        {
            AddToOutput($"❌ Jira browser initialization error: {ex.Message}", LogSeverity.ERROR);
            AddToOutput($"Stack trace: {ex.StackTrace}", LogSeverity.DEBUG);
        }
    }

    /// <summary>
    /// Load the Jira Server in the Jira browser
    /// </summary>
    private async Task LoadJiraServerInBrowser()
    {
        try
        {
            if (_currentProject == null || string.IsNullOrEmpty(_currentProject.JiraServer))
            {
                AddToOutput("ℹ️ No Jira server configured in current project", LogSeverity.INFO);
                return;
            }

            // Check if Jira browser is initialized (must be done on UI thread)
            bool jiraBrowserReady = false;
            await Dispatcher.InvokeAsync(() => {
                jiraBrowserReady = JiraBrowser?.CoreWebView2 != null;
            });
            
            if (!jiraBrowserReady)
            {
                AddToOutput("⚠️ Jira browser not ready yet. It should initialize automatically.", LogSeverity.WARNING);
                return;
            }

            if (_authenticatedHttpClient == null)
            {
                AddToOutput("ℹ️ Waiting for authentication to complete before loading Jira server...", LogSeverity.INFO);
                // Wait for authentication to be available with better feedback
                int attempts = 0;
                while (_authenticatedHttpClient == null && attempts < 60) // Extended to 60 seconds
                {
                    await Task.Delay(1000);
                    attempts++;
                    
                    // Provide periodic feedback
                    if (attempts % 10 == 0)
                    {
                        AddToOutput($"⏳ Still waiting for authentication... ({attempts}/60 seconds)", LogSeverity.DEBUG);
                    }
                }
                
                if (_authenticatedHttpClient == null)
                {
                    AddToOutput("⚠️ Authentication timeout after 60 seconds. Jira server not loaded.", LogSeverity.WARNING);
                    AddToOutput("💡 Jira server will auto-load when you open a project and authenticate.", LogSeverity.INFO);
                    return;
                }
                else
                {
                    AddToOutput("✅ Authentication completed! Loading Jira server...", LogSeverity.INFO);
                }
            }

            string jiraServerUrl = _currentProject.JiraServer;
            AddToOutput($"🌐 Loading Jira server: {jiraServerUrl}", LogSeverity.INFO);

            await Dispatcher.InvokeAsync(() =>
            {
                // Clean up existing event handlers
                if (_currentJiraNavigationHandler != null && JiraBrowser?.CoreWebView2 != null)
                {
                    JiraBrowser.CoreWebView2.NavigationCompleted -= _currentJiraNavigationHandler;
                }
                if (_currentJiraDOMHandler != null && JiraBrowser?.CoreWebView2 != null)
                {
                    JiraBrowser.CoreWebView2.DOMContentLoaded -= _currentJiraDOMHandler;
                }

                // Create new navigation handlers for Jira
                _currentJiraNavigationHandler = (sender, args) =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        if (args.IsSuccess)
                        {
                            AddToOutput($"✅ Jira server loaded successfully", LogSeverity.INFO);
                        }
                        else
                        {
                            AddToOutput($"❌ Failed to load Jira server: {args.WebErrorStatus}", LogSeverity.ERROR);
                        }
                    });
                };

                _currentJiraDOMHandler = (sender, args) =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        AddToOutput("✓ Jira server DOM loaded", LogSeverity.DEBUG);
                    });
                };

                // Attach event handlers and navigate
                if (JiraBrowser?.CoreWebView2 != null)
                {
                    JiraBrowser.CoreWebView2.NavigationCompleted += _currentJiraNavigationHandler;
                    JiraBrowser.CoreWebView2.DOMContentLoaded += _currentJiraDOMHandler;
                    
                    // Navigate to Jira server
                    JiraBrowser.CoreWebView2.Navigate(jiraServerUrl);
                }
            });
            
            // Log navigation outside of Dispatcher to avoid nested calls
            AddToOutput($"➡️ Navigating to Jira server: {jiraServerUrl}");
        }
        catch (Exception ex)
        {
            AddToOutput($"Error loading Jira server: {ex.Message}", LogSeverity.ERROR);
        }
    }

    private void JiraBackButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (JiraBrowser?.CoreWebView2 != null && JiraBrowser.CoreWebView2.CanGoBack)
            {
                JiraBrowser.CoreWebView2.GoBack();
                AddToOutput("Jira browser navigated back");
            }
            else
            {
                AddToOutput("Cannot navigate back in Jira browser");
            }
        }
        catch (Exception ex)
        {
            AddToOutput($"Error navigating back in Jira browser: {ex.Message}");
        }
    }

    private void JiraForwardButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (JiraBrowser?.CoreWebView2 != null && JiraBrowser.CoreWebView2.CanGoForward)
            {
                JiraBrowser.CoreWebView2.GoForward();
                AddToOutput("Jira browser navigated forward");
            }
            else
            {
                AddToOutput("Cannot navigate forward in Jira browser");
            }
        }
        catch (Exception ex)
        {
            AddToOutput($"Error navigating forward in Jira browser: {ex.Message}");
        }
    }

    private void JiraRefreshButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (JiraBrowser?.CoreWebView2 != null)
            {
                JiraBrowser.CoreWebView2.Reload();
                AddToOutput("Jira browser refreshed");
            }
            else
            {
                AddToOutput("Jira browser not initialized");
            }
        }
        catch (Exception ex)
        {
            AddToOutput($"Error refreshing Jira browser: {ex.Message}");
        }
    }

    #endregion

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
    private async Task PromptForHttpCredentials()
    {
        if (_authenticatedHttpClient != null)
        {
            AddToOutput("HTTP credentials already configured for this session");
            return;
        }

        AddToOutput($"  User should provide Credentials");

        
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


            
            // Try Windows authentication first
            AddToOutput($"✓ Attempting Windows authentication for server: {serverUrl}");
            
            var (success, httpClient) = await TryWindowsAuthentication(serverUrl);
            
            if (success && httpClient != null)
            {
                AddToOutput("✓ Windows authentication successful", LogSeverity.INFO);
                
                // Store the authenticated HTTP client
                _authenticatedHttpClient?.Dispose();
                _authenticatedHttpClient = httpClient;
                
                // Extract Windows credentials for JiraApi
                var (username, domain) = GetWindowsCredentials();
                _httpUsername = username;
                _httpPassword = ""; // Windows auth doesn't use explicit password
                
                AddToOutput($"✓ HTTP authentication configured for server: {serverUrl}");
                AddToOutput($"✓ Windows credentials set - Username: {username}", LogSeverity.DEBUG);
                
                // Configure WebView2 to use the same authentication
                ConfigureWebViewAuthentication();
            }
            else
            {
                AddToOutput("⚠ Windows authentication failed, showing login dialog", LogSeverity.INFO);
                
                // Fallback to manual authentication dialog
                // Just use the simple Windows username as default (no password extraction needed)
                string defaultUsername = Environment.UserName ?? "";

                
                var (dialogSuccess, dialogHttpClient, rememberCredentials, username, password) = HttpAuthDialog.GetHttpAuthentication(this, serverUrl, defaultUsername);
                


                
                if (dialogSuccess && dialogHttpClient != null)
                {
                    // Store the authenticated HTTP client and credentials
                    _authenticatedHttpClient?.Dispose();
                    _authenticatedHttpClient = dialogHttpClient;
                    _httpUsername = username;
                    _httpPassword = password;
                    
                    AddToOutput($"✓ HTTP authentication configured for server: {serverUrl}");
                    
                    // Configure WebView2 to use the same authentication
                    ConfigureWebViewAuthentication();
                    
                    if (rememberCredentials)
                    {
                        AddToOutput("✓ Credentials will be remembered for this session");
                    }
                }
                else
                {
                    AddToOutput("❌ Authentication cancelled or failed", LogSeverity.ERROR);
                    return;
                }
            }
        }
        catch (Exception ex)
        {
            var errorMsg = $"Error configuring HTTP authentication: {ex.Message}";
            AddToOutput(errorMsg, LogSeverity.ERROR);
        }
        finally
        {

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
            HvpTreeView.Items.Clear();
            rootHierarchy.IsExpanded = true;
            
            // Use data binding instead of manual TreeViewItem creation
            HvpTreeView.ItemsSource = new List<HierarchyNode> { rootHierarchy };
            
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
    /// Ensures the Output panel is visible and expanded in AvalonDock
    /// </summary>
    private void EnsureOutputPanelVisible()
    {
        try
        {
            var dockingManager = DockingManager;
            if (dockingManager?.Layout == null)
            {
                AddToOutput("⚠️ DockingManager not found - cannot ensure Output panel visibility", LogSeverity.WARNING);
                return;
            }

            // Find the Output anchorable
            var outputAnchorable = FindAnchorableByContentId(dockingManager.Layout.RootPanel, "Output");
            
            if (outputAnchorable != null)
            {
                bool wasRestored = false;
                
                // Make sure it's visible and not hidden
                if (outputAnchorable.IsHidden)
                {
                    outputAnchorable.Show();
                    wasRestored = true;
                    AddToOutput("✓ Output panel restored from hidden state", LogSeverity.DEBUG);
                }
                
                // Make sure it's not auto-hidden (minimized)
                if (outputAnchorable.IsAutoHidden)
                {
                    outputAnchorable.ToggleAutoHide();
                    wasRestored = true;
                    AddToOutput("✓ Output panel expanded from auto-hidden state", LogSeverity.DEBUG);
                }
                
                // Ensure it's active/focused and bring it to front
                outputAnchorable.IsActive = true;
                outputAnchorable.IsSelected = true;
                
                // Force focus to the Output panel
                Dispatcher.BeginInvoke(() => {
                    try
                    {
                        if (OutputTextBox != null)
                        {
                            OutputTextBox.Focus();
                            OutputTextBox.ScrollToEnd();
                        }
                    }
                    catch { /* Ignore focus errors */ }
                }, System.Windows.Threading.DispatcherPriority.Background);
                
                if (wasRestored)
                {
                    AddToOutput("✓ Output panel is now visible, active, and focused", LogSeverity.DEBUG);
                }
            }
            else
            {
                AddToOutput("⚠️ Output panel not found in layout", LogSeverity.WARNING);
            }
        }
        catch (Exception ex)
        {
            AddToOutput($"Error ensuring Output panel visibility: {ex.Message}", LogSeverity.ERROR);
        }
    }

    /// <summary>
    /// Public method to manually restore the output panel - can be called from anywhere
    /// </summary>
    public void RestoreOutputPanel()
    {
        EnsureOutputPanelVisible();
    }

    /// <summary>
    /// Recursively finds an anchorable by ContentId
    /// </summary>
    private Xceed.Wpf.AvalonDock.Layout.LayoutAnchorable? FindAnchorableByContentId(Xceed.Wpf.AvalonDock.Layout.ILayoutElement element, string contentId)
    {
        if (element is Xceed.Wpf.AvalonDock.Layout.LayoutAnchorable anchorable && anchorable.ContentId == contentId)
        {
            return anchorable;
        }
        
        if (element is Xceed.Wpf.AvalonDock.Layout.ILayoutContainer container)
        {
            foreach (var child in container.Children)
            {
                var result = FindAnchorableByContentId(child, contentId);
                if (result != null) return result;
            }
        }
        
        return null;
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
        
        // Configure WebView2 to use the same authentication
        ConfigureWebViewAuthentication();
    }

    /// <summary>
    /// Gets the authenticated HTTP client for file access
    /// </summary>
    public HttpClient? GetHttpClient() => _authenticatedHttpClient;

    /// <summary>
    /// Creates JiraApi object after successful HTTP authentication and sets up project tickets
    /// </summary>
    private async void CreateJiraApiObject()
    {
        try
        {
            // Debug: Show current project Jira settings
            AddToOutput($"🔍 Debug - JiraServer: '{_currentProject?.JiraServer ?? "NULL"}'", LogSeverity.DEBUG);
            AddToOutput($"🔍 Debug - JiraProject: '{_currentProject?.JiraProject ?? "NULL"}'", LogSeverity.DEBUG);
            AddToOutput($"🔍 Debug - JiraEpic: '{_currentProject?.JiraEpic ?? "NULL"}'", LogSeverity.DEBUG);
            AddToOutput($"🔍 Debug - JiraStory: '{_currentProject?.JiraStory ?? "NULL"}'", LogSeverity.DEBUG);
            
            // Early exit if project doesn't have Jira configuration
            if (_currentProject == null || string.IsNullOrEmpty(_currentProject.JiraServer) || string.IsNullOrEmpty(_currentProject.JiraProject))
            {
                AddToOutput("ℹ️ Skipping JiraApi creation - No Jira configuration found in project", LogSeverity.INFO);
                return;
            }

            // Wait for HTTP authentication to be ready (with timeout)
            // Now using HttpClient-based authentication instead of username/password
            var timeout = TimeSpan.FromSeconds(15); // Increased timeout
            var startTime = DateTime.Now;
            var attempts = 0;
            const int maxAttempts = 30; // 30 attempts * 500ms = 15 seconds
            
            while (_authenticatedHttpClient == null && attempts < maxAttempts)
            {
                attempts++;
                AddToOutput($"⏳ Waiting for HTTP authentication... (attempt {attempts}/{maxAttempts})", LogSeverity.DEBUG);
                
                try
                {
                    await Task.Delay(500, CancellationToken.None); // Use CancellationToken.None to prevent cancellation
                }
                catch (TaskCanceledException)
                {
                    AddToOutput("⚠️ Authentication wait was cancelled", LogSeverity.WARNING);
                    return;
                }
            }
            
            if (_authenticatedHttpClient == null)
            {
                AddToOutput("⚠️ Timeout waiting for HTTP authentication - skipping JiraApi creation", LogSeverity.WARNING);
                return;
            }
            
            var authenticationType = string.IsNullOrEmpty(_httpPassword) ? "Windows Authentication" : "Basic Authentication";
            AddToOutput($"✓ HTTP authentication ready after {attempts} attempts - Type: {authenticationType}", LogSeverity.DEBUG);
            AddToOutput($"✓ Authentication - Username: '{_httpUsername ?? "Windows Integrated"}', HttpClient: {(_authenticatedHttpClient != null ? "Ready" : "Not Available")}", LogSeverity.DEBUG);

            AddToOutput($"🎫 Creating JiraApi for server: {_currentProject.JiraServer}", LogSeverity.INFO);
            
            // Validate that the HttpClient can authenticate with both servers
            AddToOutput("🔍 Validating authentication for all servers...", LogSeverity.DEBUG);
            var dualServerAuthValid = await ValidateDualServerAuthentication();
            if (!dualServerAuthValid)
            {
                AddToOutput("⚠️ Authentication validation failed for one or more servers", LogSeverity.WARNING);
            }
            
            if (_currentProject != null && !string.IsNullOrEmpty(_currentProject.JiraServer) && 
                _authenticatedHttpClient != null &&
                !string.IsNullOrEmpty(_currentProject.JiraProject))
            {
                // Dispose existing JiraApi if any
                _jiraApi?.Dispose();
                _jiraEpicTicket = null;
                _jiraStoryTicket = null;
                
                // Using JiraAPI v1.0.2 with HttpClient parameter
                // Constructor: JiraApi(string serverUrl = null, string user = null, string password = null, bool mockingModeEnable = false, HttpClient httpClient = null)
                // When HttpClient is provided, user and password are not needed as authentication is handled by HttpClient
                _jiraApi = new JiraApi(
                    serverUrl: _currentProject.JiraServer,
                    user: null!,
                    password: null!,
                    mockingModeEnable: true,
                    httpClient: _authenticatedHttpClient!);
                
                var authType = string.IsNullOrEmpty(_httpPassword) ? "Windows Authentication" : "Basic Authentication";
                AddToOutput($"✓ JiraApi created with authenticated HttpClient (v1.0.2)", LogSeverity.DEBUG);
                AddToOutput($"🔐 Authentication configured - Type: {authType}", LogSeverity.INFO);

                AddToOutput($"✓ JiraApi initialized for server: {_currentProject.JiraServer}", LogSeverity.INFO);                // Setup the Jira project with proper null checking and timeout handling
                if (_jiraApi != null && _currentProject != null)
                {
                    AddToOutput($"🔗 Attempting to connect to Jira server: {_currentProject.JiraServer}", LogSeverity.DEBUG);
                    AddToOutput($"📋 Setting up Jira project: {_currentProject.JiraProject}");
                    CancellationTokenSource? timeoutCts = null;
                    try
                    {
                        // Use CancellationTokenSource for explicit timeout control
                        timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(30)); // 30 second timeout
                        
                        // Run SetupProject with explicit timeout
                        await Task.Run(() => {
                            try 
                            {
                                _jiraApi.SetupProject(_currentProject.JiraProject);
                            }
                            catch (Exception ex)
                            {
                                AddToOutput($"🔍 SetupProject internal error: {ex.GetType().Name}: {ex.Message}", LogSeverity.DEBUG);
                                throw; // Re-throw to be caught by outer try-catch
                            }
                        }, timeoutCts.Token);
                        
                        AddToOutput($"✓ Jira project setup completed for: {_currentProject.JiraProject}", LogSeverity.INFO);
                    }
                    catch (OperationCanceledException) when (timeoutCts?.Token.IsCancellationRequested == true)
                    {
                        AddToOutput($"⚠️ Jira project setup timed out after 30 seconds - server may be slow or unreachable", LogSeverity.WARNING);
                        AddToOutput($"💡 Try checking if {_currentProject.JiraServer} is accessible in your browser", LogSeverity.INFO);
                        return; // Exit early if project setup fails
                    }
                    catch (HttpRequestException httpEx) when (httpEx.Message.Contains("404"))
                    {
                        AddToOutput($"⚠️ Jira project '{_currentProject.JiraProject}' not found on server {_currentProject.JiraServer} (404)", LogSeverity.WARNING);
                        AddToOutput($"💡 Please verify the project key is correct and you have access to it", LogSeverity.INFO);
                        return; // Exit early if project setup fails
                    }
                    catch (TaskCanceledException)
                    {
                        AddToOutput($"⚠️ Jira project setup was cancelled or timed out - server may be slow or unreachable", LogSeverity.WARNING);
                        return; // Exit early if project setup fails
                    }
                    catch (Exception ex)
                    {
                        AddToOutput($"⚠️ Error during Jira project setup: {ex.GetType().Name}: {ex.Message}", LogSeverity.WARNING);
                        if (ex.InnerException != null)
                        {
                            AddToOutput($"⚠️ Inner exception: {ex.InnerException.Message}", LogSeverity.WARNING);
                        }
                        AddToOutput($"💡 Check if you can access {_currentProject.JiraServer} directly in a browser", LogSeverity.INFO);
                        return; // Exit early if project setup fails
                    }
                }
                else
                {
                    AddToOutput("⚠️ JiraApi or project became null during initialization", LogSeverity.WARNING);
                    return;
                }
                
                // Create or get Epic ticket
                if (!string.IsNullOrEmpty(_currentProject.JiraEpic) && _jiraApi != null && _currentProject != null)
                {
                    AddToOutput($"Creating/getting Epic ticket: {_currentProject.JiraEpic}");
                    try
                    {
                        _jiraEpicTicket = await _jiraApi.GetOrCreateEpic(_currentProject.JiraProject, _currentProject.JiraEpic);
                        if (_jiraEpicTicket != null)
                        {
                            AddToOutput($"✓ Epic ticket ready: {_currentProject.JiraEpic}", LogSeverity.INFO);
                        }
                        else
                        {
                            AddToOutput($"⚠ Failed to create/get Epic ticket: {_currentProject.JiraEpic}", LogSeverity.WARNING);
                        }
                    }
                    catch (HttpRequestException httpEx) when (httpEx.Message.Contains("404"))
                    {
                        AddToOutput($"⚠️ Epic '{_currentProject.JiraEpic}' not found - it may need to be created manually", LogSeverity.WARNING);
                    }
                    catch (TaskCanceledException)
                    {
                        AddToOutput($"⚠️ Epic creation timed out - server may be slow", LogSeverity.WARNING);
                    }
                    catch (Exception ex)
                    {
                        AddToOutput($"⚠️ Error creating/getting Epic ticket: {ex.GetType().Name}: {ex.Message}", LogSeverity.WARNING);
                    }
                }
                
                // Create or get Story ticket  
                if (!string.IsNullOrEmpty(_currentProject?.JiraStory) && _jiraApi != null && _currentProject != null)
                {
                    AddToOutput($"Creating/getting Story ticket: {_currentProject.JiraStory}");
                    try
                    {
                        // First create/get Epic to use as epicLink
                        string epicTicketKey = _jiraEpicTicket != null ? _jiraApi.GetIssueKey(_jiraEpicTicket) : _currentProject.JiraEpic;
                        _jiraStoryTicket = await _jiraApi.GetOrCreateStory(_currentProject.JiraProject, _currentProject.JiraStory, _currentProject.JiraStory, epicTicketKey);
                        if (_jiraStoryTicket != null)
                        {
                            AddToOutput($"✓ Story ticket ready: {_currentProject.JiraStory}", LogSeverity.INFO);
                        }
                        else
                        {
                            AddToOutput($"⚠ Failed to create/get Story ticket: {_currentProject.JiraStory}", LogSeverity.WARNING);
                        }
                    }
                    catch (HttpRequestException httpEx) when (httpEx.Message.Contains("404"))
                    {
                        AddToOutput($"⚠️ Story '{_currentProject.JiraStory}' not found - it may need to be created manually", LogSeverity.WARNING);
                    }
                    catch (TaskCanceledException)
                    {
                        AddToOutput($"⚠️ Story creation timed out - server may be slow", LogSeverity.WARNING);
                    }
                    catch (Exception ex)
                    {
                        AddToOutput($"⚠️ Error creating/getting Story ticket: {ex.GetType().Name}: {ex.Message}", LogSeverity.WARNING);
                    }
                }
                
                // Provide comprehensive status summary
                var successCount = 0;
                if (_jiraApi != null) successCount++;
                if (_jiraEpicTicket != null) successCount++;
                if (_jiraStoryTicket != null) successCount++;
                
                if (successCount == 3)
                {
                    AddToOutput("🎉 JiraApi setup completed successfully - all components ready!", LogSeverity.INFO);
                }
                else if (successCount > 0)
                {
                    AddToOutput($"⚠️ JiraApi partially configured ({successCount}/3 components ready)", LogSeverity.WARNING);
                }
                else
                {
                    AddToOutput("❌ JiraApi setup failed - no components configured", LogSeverity.ERROR);
                }
            }
            else
            {
                // Provide specific diagnostics about what's missing
                var missing = new List<string>();
                if (_currentProject == null) missing.Add("project");
                if (string.IsNullOrEmpty(_currentProject?.JiraServer)) missing.Add("JiraServer");
                if (_authenticatedHttpClient == null) missing.Add("authenticated HttpClient");
                if (string.IsNullOrEmpty(_currentProject?.JiraProject)) missing.Add("JiraProject");
                
                // Note: Using authenticated HttpClient instead of user/password with JiraAPI v1.0.2
                var authType = string.IsNullOrEmpty(_httpPassword) ? "Windows auth" : "Basic auth";
                AddToOutput($"⚠️ Cannot create JiraApi - Missing: {string.Join(", ", missing)} (Auth type: {authType})", LogSeverity.WARNING);
            }
        }
        catch (Exception ex)
        {
            AddToOutput($"Error creating JiraApi or setting up tickets: {ex.Message}", LogSeverity.ERROR);
            // Clean up on error
            _jiraApi?.Dispose();
            _jiraApi = null;
            _jiraEpicTicket = null;
            _jiraStoryTicket = null;
        }
    }

    /// <summary>
    /// Gets the current JiraApi instance
    /// </summary>
    public JiraApi? GetJiraApi() => _jiraApi;

    /// <summary>
    /// Gets the current Jira Epic ticket
    /// </summary>
    public JsonNode? GetJiraEpicTicket() => _jiraEpicTicket;

    /// <summary>
    /// Gets the current Jira Story ticket
    /// </summary>
    public JsonNode? GetJiraStoryTicket() => _jiraStoryTicket;

    /// <summary>
    /// Clears the stored HTTP authentication
    /// </summary>
    public void ClearHttpAuthentication()
    {
        _authenticatedHttpClient?.Dispose();
        _authenticatedHttpClient = null;
        _jiraApi?.Dispose();
        _jiraApi = null;
        _jiraEpicTicket = null;
        _jiraStoryTicket = null;
        _httpUsername = string.Empty;
        _httpPassword = string.Empty;
        AddToOutput("HTTP authentication and JiraApi cleared");
    }

    /// <summary>
    /// Ensures HTTP authentication is available for the current project if needed
    /// </summary>
    private async Task EnsureHttpAuthenticationForProject()
    {
        // Check if the project requires HTTP authentication
        if (!RequiresHttpAuthentication())
        {
            return; // No authentication needed
        }

        // If we already have a valid HTTP client, check if it's still working
        if (_authenticatedHttpClient != null)
        {
            try
            {
                // Try to make a simple request to test if authentication is still valid
                string? testUrl = !string.IsNullOrEmpty(_currentProject?.HttpServerUrl) 
                    ? _currentProject.HttpServerUrl 
                    : _currentProject?.HvpTop;

                if (!string.IsNullOrEmpty(testUrl))
                {
                    var response = await _authenticatedHttpClient.GetAsync(testUrl);
                    if (response.IsSuccessStatusCode)
                    {
                        AddToOutput("✓ Existing HTTP authentication is still valid");
                        return; // Authentication is still good
                    }
                }
            }
            catch (Exception ex)
            {
                AddToOutput($"⚠️ HTTP authentication test failed: {ex.Message}");
            }
        }

        // Authentication failed or doesn't exist, prompt for re-authentication
        AddToOutput("🔐 HTTP authentication required for this project");
        await PromptForHttpAuthentication();
    }

    /// <summary>
    /// Prompts the user for HTTP authentication
    /// </summary>
    private async Task PromptForHttpAuthentication()
    {
        try
        {
            string serverUrl = !string.IsNullOrEmpty(_currentProject?.HttpServerUrl) 
                ? _currentProject.HttpServerUrl 
                : _currentProject?.HvpTop ?? "";

            if (string.IsNullOrEmpty(serverUrl))
            {
                AddToOutput("❌ No server URL configured for authentication");
                return;
            }

            AddToOutput($"🔐 Attempting authentication for: {serverUrl}");

            // Try Windows authentication first
            var (windowsSuccess, windowsHttpClient) = await TryWindowsAuthentication(serverUrl);
            if (windowsSuccess && windowsHttpClient != null)
            {
                SetHttpAuthentication(windowsHttpClient);
                AddToOutput("✓ Windows authentication successful");
                return;
            }

            // If Windows auth failed, prompt for credentials
            string defaultUsername = Environment.UserName ?? "";
            var (success, httpClient, rememberCredentials, username, password) = HttpAuthDialog.GetHttpAuthentication(
                this, serverUrl, defaultUsername);

            if (success && httpClient != null)
            {
                // Store credentials and set HTTP authentication
                _httpUsername = username;
                _httpPassword = password;
                SetHttpAuthentication(httpClient);
                
                // Create JiraApi object for manual authentication (LoadProjectData handles it for project loading)
                CreateJiraApiObject();
                AddToOutput("✓ HTTP authentication successful");
            }
            else
            {
                AddToOutput("❌ HTTP authentication cancelled or failed");
            }
        }
        catch (Exception ex)
        {
            AddToOutput($"❌ Error during HTTP authentication: {ex.Message}", LogSeverity.ERROR);
        }
    }

    /// <summary>
    /// Handle manual HTTP authentication menu click
    /// </summary>
    private async void HttpAuthentication_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // Clear existing authentication first
            ClearHttpAuthentication();
            
            // Prompt for new authentication
            await PromptForHttpAuthentication();
        }
        catch (Exception ex)
        {
            AddToOutput($"Error during manual HTTP authentication: {ex.Message}", LogSeverity.ERROR);
        }
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
        // Clean up HTTP client and JiraApi
        _authenticatedHttpClient?.Dispose();
        _jiraApi?.Dispose();
        
        // Clean up Jira browser event handlers
        if (_currentJiraNavigationHandler != null && JiraBrowser?.CoreWebView2 != null)
        {
            JiraBrowser.CoreWebView2.NavigationCompleted -= _currentJiraNavigationHandler;
        }
        if (_currentJiraDOMHandler != null && JiraBrowser?.CoreWebView2 != null)
        {
            JiraBrowser.CoreWebView2.DOMContentLoaded -= _currentJiraDOMHandler;
        }
        
        base.OnClosed(e);
    }

    private void Exit_Click(object sender, RoutedEventArgs e) => Application.Current.Shutdown();
    private void Undo_Click(object sender, RoutedEventArgs e) => AddToOutput("Undo clicked");
    private void Redo_Click(object sender, RoutedEventArgs e) => AddToOutput("Redo clicked");
    private void Cut_Click(object sender, RoutedEventArgs e) => AddToOutput("Cut clicked");
    private void Copy_Click(object sender, RoutedEventArgs e) => AddToOutput("Copy clicked");
    private void Paste_Click(object sender, RoutedEventArgs e) => AddToOutput("Paste clicked");
    private void ToggleSolutionExplorer_Click(object sender, RoutedEventArgs e)
    {
        var panel = FindName("SolutionExplorerPanel") as Border;
        if (panel != null)
        {
            panel.Visibility = panel.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
            AddToOutput($"Solution Explorer {(panel.Visibility == Visibility.Visible ? "shown" : "hidden")}");
        }
    }
    
    private void ToggleOutput_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var dockingManager = DockingManager;
            if (dockingManager?.Layout == null)
            {
                AddToOutput("⚠️ DockingManager not found", LogSeverity.WARNING);
                return;
            }

            var outputAnchorable = FindAnchorableByContentId(dockingManager.Layout.RootPanel, "Output");
            if (outputAnchorable != null)
            {
                if (outputAnchorable.IsHidden || outputAnchorable.IsAutoHidden)
                {
                    // Always show the Output panel when it's hidden or minimized
                    if (outputAnchorable.IsHidden)
                    {
                        outputAnchorable.Show();
                    }
                    if (outputAnchorable.IsAutoHidden)
                    {
                        outputAnchorable.ToggleAutoHide();
                    }
                    outputAnchorable.IsActive = true;
                    AddToOutput("✓ Output window restored and activated");
                }
                else
                {
                    // Only hide if it's currently fully visible
                    outputAnchorable.Hide();
                    AddToOutput("✓ Output window hidden");
                }
            }
            else
            {
                AddToOutput("⚠️ Output panel not found", LogSeverity.WARNING);
            }
        }
        catch (Exception ex)
        {
            AddToOutput($"Error toggling output: {ex.Message}", LogSeverity.ERROR);
        }
    }
    
    private void ToggleErrorList_Click(object sender, RoutedEventArgs e) => AddToOutput("Toggle Error List clicked");
    
    private void ResetLayout_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // Reset AvalonDock layout to default configuration
            ResetDockingLayout();
            
            AddToOutput("🔄 Layout has been reset to default configuration", LogSeverity.INFO);
            MessageBox.Show("Layout has been successfully reset to default configuration.", "Layout Reset", 
                          MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            AddToOutput($"❌ Error resetting layout: {ex.Message}", LogSeverity.ERROR);
            MessageBox.Show($"Failed to reset layout: {ex.Message}", "Layout Reset Error", 
                          MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// Reset the AvalonDock layout to default configuration
    /// </summary>
    private void ResetDockingLayout()
    {
        try
        {
            // Get the DockingManager
            var dockingManager = DockingManager;
            if (dockingManager?.Layout == null)
            {
                AddToOutput("⚠️ DockingManager or Layout not found", LogSeverity.WARNING);
                return;
            }

            // Simple and reliable reset approach
            ResetLayoutBasic(dockingManager);

            AddToOutput("✅ AvalonDock layout reset completed", LogSeverity.INFO);
        }
        catch (Exception ex)
        {
            AddToOutput($"❌ Error in ResetDockingLayout: {ex.Message}", LogSeverity.ERROR);
            throw;
        }
    }

    /// <summary>
    /// Basic layout reset that focuses on essential operations
    /// </summary>
    private void ResetLayoutBasic(Xceed.Wpf.AvalonDock.DockingManager dockingManager)
    {
        try
        {
            // Simple approach: Reset any floating windows and ensure visibility
            ResetFloatingWindows(dockingManager);
            
            // Show any hidden panels
            RestoreDefaultVisibility(dockingManager);

            AddToOutput("✅ Basic layout reset operations completed", LogSeverity.INFO);
        }
        catch (Exception ex)
        {
            AddToOutput($"⚠️ Error in basic layout reset: {ex.Message}", LogSeverity.WARNING);
        }
    }

    /// <summary>
    /// Reset floating windows back to docked state
    /// </summary>
    private void ResetFloatingWindows(Xceed.Wpf.AvalonDock.DockingManager dockingManager)
    {
        try
        {
            // Get all floating windows and dock them
            var floatingWindows = dockingManager.Layout.FloatingWindows.ToList();
            
            foreach (var floatingWindow in floatingWindows)
            {
                try
                {
                    // Find anchorables in this floating window and dock them
                    if (floatingWindow is Xceed.Wpf.AvalonDock.Layout.LayoutAnchorableFloatingWindow anchorableWindow)
                    {
                        var anchorables = new List<Xceed.Wpf.AvalonDock.Layout.LayoutAnchorable>();
                        CollectAnchorables(anchorableWindow, anchorables);
                        
                        foreach (var anchorable in anchorables)
                        {
                            if (anchorable.IsFloating)
                            {
                                anchorable.Dock();
                                AddToOutput($"✅ Docked floating panel: {anchorable.ContentId}", LogSeverity.INFO);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    AddToOutput($"⚠️ Error docking floating window: {ex.Message}", LogSeverity.WARNING);
                }
            }
        }
        catch (Exception ex)
        {
            AddToOutput($"⚠️ Error resetting floating windows: {ex.Message}", LogSeverity.WARNING);
        }
    }

    /// <summary>
    /// Collect all anchorable elements from a layout element
    /// </summary>
    private void CollectAnchorables(Xceed.Wpf.AvalonDock.Layout.ILayoutElement element, List<Xceed.Wpf.AvalonDock.Layout.LayoutAnchorable> anchorables)
    {
        if (element is Xceed.Wpf.AvalonDock.Layout.LayoutAnchorable anchorable)
        {
            anchorables.Add(anchorable);
        }
        else if (element is Xceed.Wpf.AvalonDock.Layout.ILayoutContainer container)
        {
            foreach (var child in container.Children)
            {
                CollectAnchorables(child, anchorables);
            }
        }
    }

    /// <summary>
    /// Restore default visibility for key layout elements
    /// </summary>
    private void RestoreDefaultVisibility(Xceed.Wpf.AvalonDock.DockingManager dockingManager)
    {
        try
        {
            // Show any hidden anchorables
            var hiddenAnchorables = dockingManager.Layout.Hidden.ToList();
            
            foreach (var hiddenAnchorable in hiddenAnchorables)
            {
                try
                {
                    if (hiddenAnchorable.IsHidden)
                    {
                        hiddenAnchorable.Show();
                        AddToOutput($"✅ Restored hidden panel: {hiddenAnchorable.ContentId}", LogSeverity.INFO);
                    }
                }
                catch (Exception ex)
                {
                    AddToOutput($"⚠️ Error showing hidden panel {hiddenAnchorable.ContentId}: {ex.Message}", LogSeverity.WARNING);
                }
            }

            // Clean up layout
            try
            {
                dockingManager.Layout.CollectGarbage();
            }
            catch (Exception ex)
            {
                AddToOutput($"⚠️ Warning during layout cleanup: {ex.Message}", LogSeverity.WARNING);
            }
        }
        catch (Exception ex)
        {
            AddToOutput($"⚠️ Error restoring default visibility: {ex.Message}", LogSeverity.WARNING);
        }
    }
    
    private void SetLightTheme_Click(object sender, RoutedEventArgs e) => AddToOutput("Light theme selected");
    private void SetDarkTheme_Click(object sender, RoutedEventArgs e) => AddToOutput("Dark theme selected");
    private void RunCoverageAnalysis_Click(object sender, RoutedEventArgs e) => AddToOutput("Run Coverage Analysis clicked");
    private void CreateJira_Click(object sender, RoutedEventArgs e) => AddToOutput("Create Jira clicked");
    private void AddToWaiver_Click(object sender, RoutedEventArgs e) => AddToOutput("Add to Waiver clicked");
    private void Options_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // Load current global settings
            var appSettings = AppSettings.Load();
            var settingsDialog = new SettingsDialog(appSettings.JiraServer, appSettings.JiraProject)
            {
                Owner = this
            };
            
            if (settingsDialog.ShowDialog() == true)
            {
                // Update global settings
                appSettings.JiraServer = settingsDialog.JiraServer;
                appSettings.JiraProject = settingsDialog.JiraProject;
                appSettings.Save();
                AddToOutput($"Global Jira settings updated - Server: {appSettings.JiraServer}, Project: {appSettings.JiraProject}");
                
                // Update current project if loaded
                if (_currentProject != null)
                {
                    _currentProject.JiraServer = settingsDialog.JiraServer;
                    _currentProject.JiraProject = settingsDialog.JiraProject;
                    _currentProject.Save();
                    AddToOutput($"Current project Jira settings updated");
                    
                    // Update project info display
                    UpdateProjectStatusBar();
                }
                else
                {
                    AddToOutput("Settings will be applied to new projects.");
                }
            }
        }
        catch (Exception ex)
        {
            AddToOutput($"Error opening settings dialog: {ex.Message}");
        }
    }
    private void About_Click(object sender, RoutedEventArgs e) => MessageBox.Show("Coverage Analyzer GUI\nVersion 1.0", "About");

    // Context Menu Event Handlers
    private void ContextMenu_CreateJira_Click(object sender, RoutedEventArgs e) => AddToOutput("Context Menu: Create Jira clicked");
    private void ContextMenu_ShowJira_Click(object sender, RoutedEventArgs e) => AddToOutput("Context Menu: Show Jira clicked");
    private void ContextMenu_AddToWaiver_Click(object sender, RoutedEventArgs e) => AddToOutput("Context Menu: Add to Waiver clicked");
    
    private void ContextMenu_ClearSelection_Click(object sender, RoutedEventArgs e)
    {
        ClearMultiSelection();
        AddToOutput("Context Menu: Clear Selection - all checkboxes cleared");
    }

    // Dockable Panel Management
    private void HideSolutionExplorerPanel_Click(object sender, RoutedEventArgs e)
    {
        var panel = FindName("SolutionExplorerPanel") as Border;
        if (panel != null)
        {
            panel.Visibility = panel.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
            AddToOutput($"Solution Explorer panel {(panel.Visibility == Visibility.Visible ? "shown" : "hidden")}");
        }
    }

    private void HideOutputPanel_Click(object sender, RoutedEventArgs e)
    {
        var panel = FindName("OutputPanel") as Border;
        if (panel != null)
        {
            panel.Visibility = panel.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
            AddToOutput($"Output panel {(panel.Visibility == Visibility.Visible ? "shown" : "hidden")}");
        }
    }

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
            
            StatusText.Text = "Loading HVP data...";
            
            // Capture authentication credentials for background thread
            string? authCredentials = null;
            if (_authenticatedHttpClient != null && 
                _authenticatedHttpClient.DefaultRequestHeaders.Authorization != null)
            {
                authCredentials = _authenticatedHttpClient.DefaultRequestHeaders.Authorization.Parameter;

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
                
                // Suppress console debug output from HvpHtmlParser library
                var originalOut = Console.Out;
                var originalError = Console.Error;
                try
                {
                    // Redirect console output to suppress debug messages
                    using (var nullWriter = new StringWriter())
                    {
                        Console.SetOut(nullWriter);
                        Console.SetError(nullWriter);
                        
                        return await backgroundReader.ParseFile(_currentProject.HvpTop);
                    }
                }
                finally
                {
                    // Restore original console output
                    Console.SetOut(originalOut);
                    Console.SetError(originalError);
                }
            });
            
            var duration = DateTime.Now - startTime;
            AddToOutput($"🎉 ParseFile completed successfully in {duration.TotalSeconds:F1} seconds!", LogSeverity.INFO);
            
            StatusText.Text = "Processing results...";
            
            if (result != null)
            {
                
                // Convert to tree items and display
                try 
                {
                    var treeItems = ConvertHvpNodeToTreeItems(result);
                    
                    if (treeItems?.Count > 0)
                    {
                        // Ensure UI update happens on UI thread
                        if (Dispatcher.CheckAccess())
                        {
                            HvpTreeView.Items.Clear();
                            HvpTreeView.ItemsSource = treeItems;
                            AddToOutput($"✓ Auto-loaded {treeItems.Count} items in TreeView");
                        }
                        else
                        {
                            Dispatcher.Invoke(() => {
                                HvpTreeView.Items.Clear();
                                HvpTreeView.ItemsSource = treeItems;
                                AddToOutput($"✓ Auto-loaded {treeItems.Count} items in TreeView");
                            });
                        }
                        
                        StatusText.Text = "HVP data loaded successfully";
                        
                        // Navigate WebView2 to HVPTop page after successful load
                        await LoadHvpTopInBrowser();
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
        catch (HttpRequestException ex)
        {
            AddToOutput($"❌ Auto-load network error: {ex.Message}", LogSeverity.ERROR);
            StatusText.Text = "Network error - use manual load";
        }
        catch (UnauthorizedAccessException ex)
        {
            AddToOutput($"❌ Auto-load authentication error: {ex.Message}", LogSeverity.ERROR);
            StatusText.Text = "Authentication required - use manual load";
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
            // Preparing for background ParseFile operation
            
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
                    await PromptForHttpCredentials();
                    
                    if (_authenticatedHttpClient != null)
                    {
                        // HTTP authentication configured
                    }
                    else
                    {
                        AddToOutput("  No credentials provided - continuing without authentication.");
                        AddToOutput("⚠ WARNING: ParseFile will likely fail for protected resources", LogSeverity.WARNING);
                    }
                }
            }
            
            // About to call HtmlReader.ParseFile
            
            // Minimal diagnostics - avoid any potential hanging operations
            // Target URL prepared
            
            StatusText.Text = "Connecting to server... (may take up to 2 minutes)";
            
            try
            {
                // Calling ParseFile
                
                // Capture authentication credentials for background thread (simplified approach)
                string? authCredentials = null;
                if (_authenticatedHttpClient != null && 
                    _authenticatedHttpClient.DefaultRequestHeaders.Authorization != null)
                {
                    authCredentials = _authenticatedHttpClient.DefaultRequestHeaders.Authorization.Parameter;
                    // Authentication credentials captured
                }
                else
                {
                    // No authentication available
                }
                
                // Use ConfigureAwait(false) to avoid UI context issues
                var startTime = DateTime.Now;
                // Starting ParseFile operation
                
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
                    
                    // Suppress console debug output from HvpHtmlParser library
                    var originalOut = Console.Out;
                    var originalError = Console.Error;
                    try
                    {
                        // Redirect console output to suppress debug messages
                        using (var nullWriter = new StringWriter())
                        {
                            Console.SetOut(nullWriter);
                            Console.SetError(nullWriter);
                            
                            return await backgroundReader.ParseFile(_currentProject.HvpTop);
                        }
                    }
                    finally
                    {
                        // Restore original console output
                        Console.SetOut(originalOut);
                        Console.SetError(originalError);
                    }
                }); // Remove ConfigureAwait(false) to stay on UI thread
                
                // Background thread completed
                
                var duration = DateTime.Now - startTime;
                // ParseFile completed successfully
                
                // Reset status immediately after successful parse
                StatusText.Text = "Processing results...";
                
                // ParseFile result obtained
                
                if (result != null)
                {
                    // ParseFile returned result
                    
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
                                HvpTreeView.Items.Clear();
                                HvpTreeView.ItemsSource = treeItems;
                                // TreeView populated successfully
                            }
                            else
                            {
                                Dispatcher.Invoke(() => {
                                    // Clear existing items before setting ItemsSource
                                    HvpTreeView.Items.Clear();
                                    HvpTreeView.ItemsSource = treeItems;
                                    // TreeView populated successfully
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

            }
            else
            {

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
            return new List<System.Windows.Controls.TreeViewItem> { CreateTreeViewItemFromHvpNode(hvpNode, isRoot: true, depth: 0) };
        }
        catch (Exception ex)
        {
            AddToOutput($"❌ Error converting HvpNode: {ex.Message}", LogSeverity.ERROR);
            return null;
        }
    }

    /// <summary>
    /// Create a TreeViewItem from an HvpNode with proper table row structure
    /// </summary>
    private System.Windows.Controls.TreeViewItem CreateTreeViewItemFromHvpNode(object hvpNode, bool isRoot = false, int depth = 0)
    {
        var treeItem = new System.Windows.Controls.TreeViewItem();
        var nodeType = hvpNode.GetType();
        
        // Get all HvpNode properties
        var nameProperty = nodeType.GetProperty("Name");
        var scoreProperty = nodeType.GetProperty("Score");
        var groupScoreProperty = nodeType.GetProperty("GroupScore");
        var groupFractionProperty = nodeType.GetProperty("GroupFraction");
        var testCountProperty = nodeType.GetProperty("TestCount");
        var passCountProperty = nodeType.GetProperty("PassCount");
        var failCountProperty = nodeType.GetProperty("FailCount");
        var warnCountProperty = nodeType.GetProperty("WarnCount");
        var assertCountProperty = nodeType.GetProperty("AssertCount");
        var assertProperty = nodeType.GetProperty("AssertScore");
        var unknownCountProperty = nodeType.GetProperty("UnknownCount");
        var childrenProperty = nodeType.GetProperty("Children");
        var linkProperty = nodeType.GetProperty("Link");
        var urlProperty = nodeType.GetProperty("Url");
        var reportPathProperty = nodeType.GetProperty("ReportPath");
        var hyperlinkProperty = nodeType.GetProperty("Hyperlink");
        var htmlPathProperty = nodeType.GetProperty("HtmlPath");
        var reportFileProperty = nodeType.GetProperty("ReportFile");
        var pathProperty = nodeType.GetProperty("Path");
        var filePathProperty = nodeType.GetProperty("FilePath");
        
        // Debug: Log available properties for root node
        if (isRoot)
        {
            AddToOutput($"🔍 Debug - Root node type: {nodeType.Name}", LogSeverity.INFO);
            AddToOutput($"🔍 Debug - Available properties: {string.Join(", ", nodeType.GetProperties().Select(p => p.Name))}", LogSeverity.INFO);
        }
        
        // Get node values
        string nodeName;
        if (isRoot)
        {
            // For root node: try to use HvpNode name first, fallback to ReportNameWithoutVerifPlan
            var hvpNodeName = nameProperty?.GetValue(hvpNode)?.ToString();
            
            if (!string.IsNullOrEmpty(hvpNodeName) && hvpNodeName != "Unknown")
            {
                nodeName = hvpNodeName;
            }
            else if (_currentProject != null && !string.IsNullOrEmpty(_currentProject.ReportNameWithoutVerifPlan))
            {
                nodeName = _currentProject.ReportNameWithoutVerifPlan;
            }
            else
            {
                nodeName = "Coverage Report";
            }
        }
        else
        {
            nodeName = nameProperty?.GetValue(hvpNode)?.ToString() ?? "Unknown";
        }
        
        // Get all property values
        var score = scoreProperty?.GetValue(hvpNode);
        var groupScore = groupScoreProperty?.GetValue(hvpNode);
        var groupFraction = groupFractionProperty?.GetValue(hvpNode)?.ToString() ?? "";
        var testCount = testCountProperty?.GetValue(hvpNode);
        var passCount = passCountProperty?.GetValue(hvpNode);
        var failCount = failCountProperty?.GetValue(hvpNode);
        var warnCount = warnCountProperty?.GetValue(hvpNode);
        var assertCount = assertCountProperty?.GetValue(hvpNode);
        var assertValue = assertProperty?.GetValue(hvpNode);
        var unknownCount = unknownCountProperty?.GetValue(hvpNode);
        
        // Score debugging removed for performance
        
        // Create table row structure as Header - with separator column and reduced widths
        var tableRow = new Grid();
        tableRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(200) }); // Name - reduced from 234
        tableRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1) });   // Separator
        tableRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(65) });  // Score - reduced from 80
        tableRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(65) });  // Group - reduced from 100
        tableRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(65) });  // Assert - reduced from 80
        tableRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(65) });  // Test - reduced from 90
        
        // Name column with checkbox and depth-based indentation
        var namePanel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        
        // Add indentation
        if (depth > 0)
        {
            var spacer = new Border { Width = depth * 16 }; // 16 pixels per level
            namePanel.Children.Add(spacer);
        }
        
        // Add checkbox
        var checkbox = new CheckBox 
        { 
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(2, 0, 5, 0)
        };
        checkbox.Checked += TreeViewItem_Checked;
        checkbox.Unchecked += TreeViewItem_Unchecked;
        namePanel.Children.Add(checkbox);
        
        // Add name text
        var nameText = new TextBlock 
        { 
            Text = nodeName, 
            VerticalAlignment = VerticalAlignment.Center,
            Padding = new Thickness(0, 1, 2, 1),
            Foreground = new SolidColorBrush(Colors.Black)
        };
        namePanel.Children.Add(nameText);
        
        Grid.SetColumn(namePanel, 0);
        tableRow.Children.Add(namePanel);
        
        // Separator line
        var separator = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(77, 128, 128, 128)), // Semi-transparent gray
            Width = 1,
            VerticalAlignment = VerticalAlignment.Stretch,
            Margin = new Thickness(0, 2, 0, 2)
        };
        Grid.SetColumn(separator, 1);
        tableRow.Children.Add(separator);
        
        // Score column with color coding
        var scoreText = new TextBlock 
        { 
            Text = score is double scoreVal ? $"{scoreVal:F1}%" : "",
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Left,
            Padding = new Thickness(2, 1, 2, 1),
            FontFamily = new FontFamily("Consolas")
        };
        if (score is double scoreDouble)
        {
            var colorStyle = CoverageColorMapping.GetColorStyleForPercentage(scoreDouble);
            scoreText.Foreground = colorStyle.Foreground;
            scoreText.Background = colorStyle.Background;
        }
        else
        {
            scoreText.Foreground = new SolidColorBrush(Colors.Black);
        }
        Grid.SetColumn(scoreText, 2);
        tableRow.Children.Add(scoreText);
        
        // GroupScore column with color coding
        var groupScoreText = new TextBlock 
        { 
            Text = groupScore is double groupScoreVal ? $"{groupScoreVal:F1}%" : "",
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Left,
            Padding = new Thickness(2, 1, 2, 1),
            FontFamily = new FontFamily("Consolas")
        };
        if (groupScore is double groupScoreDouble)
        {
            var colorStyle = CoverageColorMapping.GetColorStyleForPercentage(groupScoreDouble);
            groupScoreText.Foreground = colorStyle.Foreground;
            groupScoreText.Background = colorStyle.Background;
        }
        else
        {
            groupScoreText.Foreground = new SolidColorBrush(Colors.Black);
        }
        Grid.SetColumn(groupScoreText, 3);
        tableRow.Children.Add(groupScoreText);
        
        // Assert Score column (using ASSERT property from HVPNode)
        var assertScoreText = new TextBlock 
        { 
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Left,
            Padding = new Thickness(2, 1, 2, 1),
            FontFamily = new FontFamily("Consolas")
        };
        
        // Use ASSERT property value if available, fallback to assertCount
        if (assertValue != null)
        {
            double assertPercentage = 0.0;
            bool isValidPercentage = false;
            
            // Debug for root node
            if (isRoot)
            {
                AddToOutput($"🔍 Root ASSERT value: {assertValue} (Type: {assertValue.GetType().Name})", LogSeverity.ERROR);
            }
            
            if (assertValue is string assertString && !string.IsNullOrEmpty(assertString))
            {
                // Try to parse string as double, handle both decimal separators
                var normalizedString = assertString.Replace(',', '.');
                if (double.TryParse(normalizedString, out double parsedValue))
                {
                    assertPercentage = parsedValue;
                    isValidPercentage = true;
                }
            }
            
            if (isValidPercentage)
            {
                assertScoreText.Text = $"{assertPercentage:F1}%";
                // Apply color coding for percentage values
                var colorStyle = CoverageColorMapping.GetColorStyleForPercentage(assertPercentage);
                assertScoreText.Foreground = colorStyle.Foreground;
                assertScoreText.Background = colorStyle.Background;
            }
            else
            {
                assertScoreText.Text = assertValue.ToString() ?? "";
                assertScoreText.Foreground = new SolidColorBrush(Colors.Black);
            }
        }
        else
        {
            // Debug for root node when no ASSERT data found
            if (isRoot)
            {
                AddToOutput($"❌ Root node: No ASSERT or AssertCount found", LogSeverity.WARNING);
            }
            assertScoreText.Text = "";
            assertScoreText.Foreground = new SolidColorBrush(Colors.Black);
        }
        
        Grid.SetColumn(assertScoreText, 4);
        tableRow.Children.Add(assertScoreText);
        
        // Tests Score column (calculated as passCount*100/testCount with color coding)
        var testsScoreText = new TextBlock 
        { 
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Left,
            Padding = new Thickness(2, 1, 2, 1),
            FontFamily = new FontFamily("Consolas")
        };
        
        // Calculate test score percentage: (passCount * 100) / testCount
        if (testCount is int testCountVal && testCountVal > 0 && passCount is int passCountVal)
        {
            double testScorePercentage = (passCountVal * 100.0) / testCountVal;
            testsScoreText.Text = $"{testScorePercentage:F1}%";
            
            // Apply color coding based on percentage
            var colorStyle = CoverageColorMapping.GetColorStyleForPercentage(testScorePercentage);
            testsScoreText.Foreground = colorStyle.Foreground;
            testsScoreText.Background = colorStyle.Background;
        }
        else
        {
            testsScoreText.Text = "";
            testsScoreText.Foreground = new SolidColorBrush(Colors.Black);
        }
        
        Grid.SetColumn(testsScoreText, 5);
        tableRow.Children.Add(testsScoreText);
        
        // Set the table row as the header
        treeItem.Header = tableRow;
        
        // Store HvpNode reference for selection events
        treeItem.Tag = hvpNode;
        
        // Create HierarchyNode for DataContext to work with new selection handler
        var hierarchyNode = new HierarchyNode(nodeName, nodeName);
        hierarchyNode.CoveragePercentage = score is double scoreValue ? scoreValue : 0.0;
        hierarchyNode.LinesCovered = passCount is int passValue ? passValue : 0;
        hierarchyNode.TotalLines = testCount is int testValue ? testValue : 0;
        
        // Extract link information from HVP node
        string? nodeLink = null;
        
        // Special handling for root node - link to HvpTop main page
        if (isRoot)
        {
            nodeLink = _currentProject?.HvpTop;
        }
        // Try different possible link properties in order of preference
        else if (hyperlinkProperty != null)
        {
            nodeLink = hyperlinkProperty.GetValue(hvpNode)?.ToString();
        }
        else if (htmlPathProperty != null)
        {
            nodeLink = htmlPathProperty.GetValue(hvpNode)?.ToString();
        }
        else if (reportFileProperty != null)
        {
            nodeLink = reportFileProperty.GetValue(hvpNode)?.ToString();
        }
        else if (reportPathProperty != null)
        {
            nodeLink = reportPathProperty.GetValue(hvpNode)?.ToString();
        }
        else if (linkProperty != null)
        {
            nodeLink = linkProperty.GetValue(hvpNode)?.ToString();
        }
        else if (urlProperty != null)
        {
            nodeLink = urlProperty.GetValue(hvpNode)?.ToString();
        }
        else if (pathProperty != null)
        {
            nodeLink = pathProperty.GetValue(hvpNode)?.ToString();
        }
        else if (filePathProperty != null)
        {
            nodeLink = filePathProperty.GetValue(hvpNode)?.ToString();
        }
        
        // Log all link-related properties for debugging
        if (string.IsNullOrEmpty(nodeLink) && !isRoot)
        {

            
            var linkProperties = new[] {
                ("Hyperlink", hyperlinkProperty),
                ("HtmlPath", htmlPathProperty), 
                ("ReportFile", reportFileProperty),
                ("ReportPath", reportPathProperty),
                ("Link", linkProperty),
                ("Url", urlProperty),
                ("Path", pathProperty),
                ("FilePath", filePathProperty)
            };
            
            foreach (var (propName, prop) in linkProperties)
            {
                if (prop != null)
                {
                    try
                    {
                        var value = prop.GetValue(hvpNode)?.ToString();

                    }
                    catch (Exception)
                    {
                        // Ignore property access errors
                    }
                }
                else
                {

                }
            }
        }
        
        hierarchyNode.Link = nodeLink;
        treeItem.DataContext = hierarchyNode;
        
        // Keep existing selection handler for backward compatibility
        treeItem.Selected += (sender, e) => {
            try
            {
                if (!string.IsNullOrEmpty(nodeLink))
                {
                    AddToOutput($"📋 Selected node: {nodeName} - Loading report: {nodeLink}");
                    
                    // Check if it's a relative path that needs ReportPath prefix
                    string fullPath = nodeLink;
                    if (_currentProject?.ReportPath != null && !Uri.IsWellFormedUriString(nodeLink, UriKind.Absolute))
                    {
                        if (!Path.IsPathRooted(nodeLink))
                        {
                            string basePath = Path.GetDirectoryName(_currentProject.ReportPath) ?? "";
                            fullPath = Path.Combine(basePath, nodeLink);
                        }
                    }
                    
                    if (File.Exists(fullPath))
                    {
                        NavigateToHvpReport(fullPath);
                    }
                    else if (Uri.IsWellFormedUriString(fullPath, UriKind.Absolute))
                    {
                        NavigateToWebUrl(fullPath);
                    }
                    else
                    {
                        AddToOutput($"⚠️ Report file not found: {fullPath}", LogSeverity.WARNING);
                    }
                }
                else
                {
                    AddToOutput($"📋 Selected node: {nodeName} - No report path available", LogSeverity.WARNING);
                }
            }
            catch (Exception ex)
            {
                AddToOutput($"Error handling tree selection: {ex.Message}", LogSeverity.ERROR);
            }
        };
        
        if (isRoot)
        {

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
                        var childItem = CreateTreeViewItemFromHvpNode(child, isRoot: false, depth: depth + 1);
                        treeItem.Items.Add(childItem);
                    }
                }
            }
        }
        
        return treeItem;
    }

    /// <summary>
    /// Synchronize header scroll with tree scroll for table alignment
    /// </summary>
    private void TreeScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        try
        {
            if (sender is ScrollViewer treeScrollViewer)
            {
                // Find the header scroll viewer and synchronize horizontal scroll
                var headerScrollViewer = FindName("HeaderScrollViewer") as ScrollViewer;
                if (headerScrollViewer != null)
                {
                    headerScrollViewer.ScrollToHorizontalOffset(treeScrollViewer.HorizontalOffset);
                }
            }
        }
        catch (Exception ex)
        {
            AddToOutput($"Error synchronizing scroll: {ex.Message}", LogSeverity.ERROR);
        }
    }

    /// <summary>
    /// Handle mouse wheel events for TreeView to enable smooth scrolling
    /// </summary>
    private void SolutionExplorer_PreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
    {
        try
        {
            // Find the TreeScrollViewer and forward the mouse wheel event to it
            var treeScrollViewer = FindName("TreeScrollViewer") as ScrollViewer;
            if (treeScrollViewer != null)
            {
                // Calculate scroll amount (increased for faster scrolling)
                double scrollAmount = -e.Delta / 3.0; // Faster scroll: ~8 lines per wheel click
                
                // Scroll vertically
                treeScrollViewer.ScrollToVerticalOffset(treeScrollViewer.VerticalOffset + scrollAmount);
                
                // Mark event as handled so it doesn't bubble up
                e.Handled = true;
            }
        }
        catch (Exception ex)
        {
            AddToOutput($"Error handling mouse wheel: {ex.Message}", LogSeverity.ERROR);
        }
    }

    /// <summary>
    /// Handle TreeView selection changes to navigate to the associated report
    /// </summary>
    private void HvpTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        try
        {
            if (e.NewValue is TreeViewItem treeViewItem && treeViewItem.DataContext is HierarchyNode node)
            {
                // Use the node from DataContext if available
                HandleNodeSelection(node);
            }
            else if (e.NewValue is HierarchyNode directNode)
            {
                // Direct HierarchyNode selection
                HandleNodeSelection(directNode);
            }
            // Handle legacy TreeViewItem selection for default items
            else if (e.NewValue is TreeViewItem legacyItem)
            {
                AddToOutput($"Selected: {legacyItem.Header}", LogSeverity.INFO);
            }
        }
        catch (Exception ex)
        {
            AddToOutput($"Error handling tree selection: {ex.Message}", LogSeverity.ERROR);
        }
    }

    /// <summary>
    /// Handle Stats TreeView selection changes
    /// </summary>
    private void StatsTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        try
        {
            if (e.NewValue is TreeViewItem treeViewItem)
            {
                AddToOutput($"📊 Stats selected: {treeViewItem.Header}", LogSeverity.INFO);
                
                // Check if the TreeViewItem has a Tag containing the original node data
                if (treeViewItem.Tag != null)
                {
                    HandleStatsNodeSelection(treeViewItem.Tag);
                }
            }
        }
        catch (Exception ex)
        {
            AddToOutput($"Error handling stats tree selection: {ex.Message}", LogSeverity.ERROR);
        }
    }
    
    /// <summary>
    /// Handle the selection of a Stats node and navigate to its hyperlink if available
    /// </summary>
    private void HandleStatsNodeSelection(object statsNode)
    {
        try
        {
            var nodeType = statsNode.GetType();
            var nameProperty = nodeType.GetProperty("Name");
            var linkProperty = nodeType.GetProperty("Link") ?? nodeType.GetProperty("Hyperlink") ?? nodeType.GetProperty("Href");
            
            var nodeName = nameProperty?.GetValue(statsNode)?.ToString() ?? "Unknown";
            AddToOutput($"📋 Selected stats node: {nodeName}", LogSeverity.INFO);
            
            // Show progress indicator for stats navigation
            OperationProgress.Visibility = Visibility.Visible;
            OperationProgress.IsIndeterminate = false;
            OperationProgress.Value = 10;
            StatusText.Text = "Loading stats report... 10%";
            
            if (linkProperty != null)
            {
                var linkValue = linkProperty.GetValue(statsNode)?.ToString();
                
                if (!string.IsNullOrEmpty(linkValue))
                {
                    string reportUrl = linkValue;
                    




                    
                    // Check if the link is relative and needs to be combined with ReportPath
                    if (_currentProject?.ReportPath != null && !Uri.IsWellFormedUriString(reportUrl, UriKind.Absolute))
                    {

                        
                        // For web URLs, we need to use URI combination, not Path.Combine
                        if (_currentProject.ReportPath.StartsWith("http://") || _currentProject.ReportPath.StartsWith("https://"))
                        {
                            try
                            {
                                // Ensure base path ends with slash to append rather than replace
                                string basePath = _currentProject.ReportPath;
                                if (!basePath.EndsWith("/"))
                                {
                                    basePath += "/";
                                }

                                var baseUri = new Uri(basePath);
                                var combinedUri = new Uri(baseUri, reportUrl);
                                reportUrl = combinedUri.ToString();

                            }
                            catch (Exception ex)
                            {
                                AddToOutput($"Error combining URIs: {ex.Message}", LogSeverity.ERROR);
                            }
                        }
                        else if (!Path.IsPathRooted(reportUrl))
                        {
                            // For local paths, use Path.Combine
                            string basePath = Path.GetDirectoryName(_currentProject.ReportPath) ?? "";
                            reportUrl = Path.Combine(basePath, reportUrl);

                        }
                    }
                    

                    
                    // Navigate to the URL or file
                    if (File.Exists(reportUrl))
                    {
                        AddToOutput($"🌐 Loading local stats report: {Path.GetFileName(reportUrl)}", LogSeverity.INFO);
                        StatusText.Text = $"Loading {Path.GetFileName(reportUrl)}... 50%";
                        OperationProgress.Value = 50;
                        NavigateToHvpReport(reportUrl);
                    }
                    else if (Uri.IsWellFormedUriString(reportUrl, UriKind.Absolute))
                    {
                        AddToOutput($"🌐 Loading web stats report: {reportUrl}", LogSeverity.INFO);
                        StatusText.Text = "Loading web stats report... 50%";
                        OperationProgress.Value = 50;
                        NavigateToWebUrl(reportUrl);
                    }
                    else
                    {
                        AddToOutput($"⚠️ Stats report file not found: {reportUrl}", LogSeverity.WARNING);
                        StatusText.Text = "Stats file not found";
                        OperationProgress.Value = 0;
                    }
                }
                else
                {
                    AddToOutput($"ℹ️ No hyperlink available for stats node: {nodeName}", LogSeverity.INFO);
                    // Hide progress when no navigation occurs
                    StatusText.Text = "No hyperlink available";
                    OperationProgress.Visibility = Visibility.Collapsed;
                }
            }
            else
            {
                AddToOutput($"ℹ️ No hyperlink property found for stats node: {nodeName}", LogSeverity.INFO);
                // Hide progress when no navigation occurs
                StatusText.Text = "No hyperlink property found";
                OperationProgress.Visibility = Visibility.Collapsed;
            }
        }
        catch (Exception ex)
        {
            AddToOutput($"Error handling stats node selection: {ex.Message}", LogSeverity.ERROR);
            
            // Hide progress on error
            StatusText.Text = "Error loading stats";
            OperationProgress.Visibility = Visibility.Collapsed;
            StatusText.Text = "Error loading stats report";
        }
        finally
        {
            // Ensure progress is hidden if no URL was found
            if (OperationProgress.Visibility == Visibility.Visible && OperationProgress.Value < 50)
            {
                Task.Delay(500).ContinueWith(_ => 
                {
                    Dispatcher.Invoke(() => 
                    {
                        OperationProgress.Visibility = Visibility.Collapsed;
                        StatusText.Text = "Ready";
                    });
                });
            }
        }
    }

    /// <summary>
    /// Load statistics from stats.html.gz file using HTMLReader
    /// </summary>
    private async void LoadStats_Click(object sender, RoutedEventArgs e)
    {
        AddToOutput("=== Loading Statistics Data ===\n");
        
        // Show progress indicator
        OperationProgress.Visibility = Visibility.Visible;
        
        // Disable button during operation
        if (sender is Button loadButton)
        {
            loadButton.IsEnabled = false;
        }
        
        StatusText.Text = "Loading statistics...";
        
        // Show progress indicator with percentage
        OperationProgress.Visibility = Visibility.Visible;
        OperationProgress.IsIndeterminate = false;
        OperationProgress.Value = 0;
        
        try
        {
            if (_currentProject == null)
            {
                AddToOutput("⚠ No project loaded. Please create or open a project first.", LogSeverity.WARNING);
                return;
            }
            
            if (string.IsNullOrEmpty(_currentProject.ReportPath))
            {
                AddToOutput("⚠ No ReportPath configured. Please set ReportPath in project settings.", LogSeverity.WARNING);
                return;
            }
            
            // Construct stats.html.gz path
            string statsPath = Path.Combine(_currentProject.ReportPath, "stats.html.gz");
            
            AddToOutput($"📊 Looking for stats file: {statsPath}");
            
            // Check if it's a local file or URL
            if (!statsPath.StartsWith("http://") && !statsPath.StartsWith("https://"))
            {
                // Local file - check if exists
                if (!File.Exists(statsPath))
                {
                    AddToOutput($"❌ Stats file not found: {statsPath}", LogSeverity.ERROR);
                    AddToOutput("💡 Make sure the ReportPath is correct and contains stats.html.gz", LogSeverity.INFO);
                    return;
                }
            }
            
            // Check if authentication is needed for HTTP URLs and use existing HttpClient
            HttpClient? httpClient = null;
            if (statsPath.StartsWith("http://") || statsPath.StartsWith("https://"))
            {
                if (_authenticatedHttpClient != null)
                {
                    httpClient = _authenticatedHttpClient;
                    AddToOutput("🔐 Using existing authenticated HTTP client for stats loading");
                }
                else
                {
                    AddToOutput("⚠️ HTTP URL detected but no authentication configured. Some features may not work.", LogSeverity.WARNING);
                    AddToOutput("💡 Tip: Use File > Load Coverage Data to set up HTTP authentication first.", LogSeverity.INFO);
                }
            }
            
            var startTime = DateTime.Now;
            AddToOutput("⏱️ Starting ParseFile operation for stats...", LogSeverity.INFO);
            
            // Update progress status
            StatusText.Text = "Downloading and parsing stats file...";
            
            // Update progress status
            StatusText.Text = "Downloading and parsing stats file...";
            
            var result = await Task.Run(async () => {
                // Create fresh instances for background thread
                var backgroundReader = new HtmlReader();
                
                // Set up authentication if we have an authenticated HttpClient
                if (httpClient != null)
                {
                    backgroundReader.SetHttpClient(httpClient);
                }
                
                // Suppress console debug output from HvpHtmlParser library
                var originalOut = Console.Out;
                var originalError = Console.Error;
                try
                {
                    // Redirect console output to suppress debug messages
                    using (var nullWriter = new StringWriter())
                    {
                        Console.SetOut(nullWriter);
                        Console.SetError(nullWriter);
                        
                        return await backgroundReader.ParseFile(statsPath);
                    }
                }
                finally
                {
                    // Restore original console output
                    Console.SetOut(originalOut);
                    Console.SetError(originalError);
                }
            });
            
            var duration = DateTime.Now - startTime;
            AddToOutput($"🎉 Stats ParseFile completed successfully in {duration.TotalSeconds:F1} seconds!", LogSeverity.INFO);
            
            // Update progress status
            StatusText.Text = "Building stats tree view... 75%";
            OperationProgress.Value = 75;
            
            if (result != null)
            {
                
                // Convert to tree items and display in Stats TreeView
                try 
                {
                    var statsTreeItems = ConvertStatsNodeToTreeItems(result);
                    
                    if (statsTreeItems?.Count > 0)
                    {
                        // Update StatsTreeView on UI thread - directly add items without extra wrapper
                        if (Dispatcher.CheckAccess())
                        {
                            StatsTreeView.Items.Clear();
                            
                            // Add parsed items directly to TreeView (no "Stats" wrapper)
                            foreach (var item in statsTreeItems)
                            {
                                StatsTreeView.Items.Add(item);
                            }
                            
                            AddToOutput($"✓ Loaded {statsTreeItems.Count} stats items in TreeView");
                        }
                        else
                        {
                            Dispatcher.Invoke(() => {
                                StatsTreeView.Items.Clear();
                                
                                // Add parsed items directly to TreeView (no "Stats" wrapper)
                                foreach (var item in statsTreeItems)
                                {
                                    StatsTreeView.Items.Add(item);
                                }
                                
                                AddToOutput($"✓ Loaded {statsTreeItems.Count} stats items in TreeView");
                            });
                        }
                    }
                    else
                    {
                        AddToOutput("⚠ No stats items found to display", LogSeverity.WARNING);
                    }
                    
                    // Mark stats as loaded if we successfully added items
                    if (StatsTreeView.Items.Count > 0)
                    {
                        _statsLoaded = true;
                        AddToOutput($"✓ Stats loaded successfully - {StatsTreeView.Items.Count} items", LogSeverity.INFO);
                    }
                }
                catch (Exception uiEx)
                {
                    AddToOutput($"❌ UI update error for stats: {uiEx.Message}", LogSeverity.ERROR);
                }
            }
            else
            {
                AddToOutput("❌ ParseFile returned null - no stats data parsed", LogSeverity.ERROR);
            }
        }
        catch (Exception ex)
        {
            AddToOutput($"❌ Error loading stats: {ex.Message}", LogSeverity.ERROR);
        }
        finally
        {
            // Hide progress indicator
            OperationProgress.Visibility = Visibility.Collapsed;
            OperationProgress.IsIndeterminate = true;
            OperationProgress.Value = 0;
            
            // Re-enable button and reset status
            if (sender is Button senderButton)
            {
                senderButton.IsEnabled = true;
            }
            StatusText.Text = "Ready";
        }
    }
    
    /// <summary>
    /// Handle ExplorerTabControl selection change to auto-load stats if needed
    /// </summary>
    private void ExplorerTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.Source == sender && ExplorerTabControl.SelectedItem is TabItem selectedTab)
        {
            // Check if Stats tab is selected (look for "Stat" in header)
            string tabHeader = selectedTab.Header?.ToString() ?? "";
            if (tabHeader.Contains("Stat", StringComparison.OrdinalIgnoreCase))
            {
                OnStatsTabSelected();
            }
        }
    }

    /// <summary>
    /// Handle tab selection change to auto-load stats if needed
    /// </summary>
    public void OnStatsTabSelected()
    {
        // Auto-load stats if not already loaded and we have a project
        if (!_statsLoaded && _currentProject != null && !string.IsNullOrEmpty(_currentProject.ReportPath))
        {
            AddToOutput("📊 Auto-loading stats on tab switch...", LogSeverity.INFO);
            LoadStats_Click(this, new RoutedEventArgs());
        }
    }

    /// <summary>
    /// Convert StatsNode to TreeView items, preserving original node names
    /// </summary>
    private List<TreeViewItem>? ConvertStatsNodeToTreeItems(object statsNode, int maxDepth = 5, int currentDepth = 0)
    {
        if (statsNode == null || currentDepth >= maxDepth)
            return null;
            
        var result = new List<TreeViewItem>();
        
        try
        {
            var nodeType = statsNode.GetType();
            
            // Get common properties
            var nameProperty = nodeType.GetProperty("Name");
            var childrenProperty = nodeType.GetProperty("Children");
            
            var nodeName = nameProperty?.GetValue(statsNode)?.ToString() ?? "Unknown";
            
            // Processing stats node (logging removed for performance)
            
            // Create TreeViewItem with original node name and store node data in Tag
            var treeItem = new TreeViewItem
            {
                Header = nodeName,
                IsExpanded = currentDepth < 2, // Expand first 2 levels
                Tag = statsNode // Store original node data for hyperlink access
            };
            
            // Process children if they exist
            if (childrenProperty != null)
            {
                var children = childrenProperty.GetValue(statsNode);
                if (children is System.Collections.IEnumerable enumerable)
                {
                    int childCount = 0;
                    foreach (var child in enumerable)
                    {
                        var childItems = ConvertStatsNodeToTreeItems(child, maxDepth, currentDepth + 1);
                        if (childItems != null)
                        {
                            foreach (var childItem in childItems)
                            {
                                treeItem.Items.Add(childItem);
                                childCount++;
                            }
                        }
                    }
                    
                    if (childCount > 0)
                    {

                    }
                }
            }
            
            result.Add(treeItem);
        }
        catch (Exception ex)
        {
            AddToOutput($"Error processing stats node: {ex.Message}", LogSeverity.ERROR);
            
            // Add error item
            var errorItem = new TreeViewItem
            {
                Header = $"Error: {ex.Message}"
            };
            result.Add(errorItem);
        }
        
        return result;
    }

    /// <summary>
    /// Handle the selection of a HierarchyNode and navigate to its report
    /// </summary>
    private void HandleNodeSelection(HierarchyNode node)
    {
        try
        {
            AddToOutput($"📋 Selected node: {node.Name}", LogSeverity.INFO);
            
            // Show progress indicator for navigation
            OperationProgress.Visibility = Visibility.Visible;
            OperationProgress.IsIndeterminate = false;
            OperationProgress.Value = 10;
            StatusText.Text = "Loading HVP report... 10%";

            if (!string.IsNullOrEmpty(node.Link))
            {
                string reportUrl = node.Link;
                




                
                // Check if the link is relative and needs to be combined with ReportPath
                if (_currentProject?.ReportPath != null && !Uri.IsWellFormedUriString(reportUrl, UriKind.Absolute))
                {

                    
                    // For web URLs, we need to use URI combination, not Path.Combine
                    if (_currentProject.ReportPath.StartsWith("http://") || _currentProject.ReportPath.StartsWith("https://"))
                    {
                        try
                        {
                            // Ensure base path ends with slash to append rather than replace
                            string basePath = _currentProject.ReportPath;
                            if (!basePath.EndsWith("/"))
                            {
                                basePath += "/";
                            }

                            var baseUri = new Uri(basePath);
                            var combinedUri = new Uri(baseUri, reportUrl);
                            reportUrl = combinedUri.ToString();

                        }
                        catch (Exception ex)
                        {
                            AddToOutput($"Error combining URIs: {ex.Message}", LogSeverity.ERROR);
                        }
                    }
                    else if (!Path.IsPathRooted(reportUrl))
                    {
                        // For local paths, use Path.Combine
                        string basePath = Path.GetDirectoryName(_currentProject.ReportPath) ?? "";
                        reportUrl = Path.Combine(basePath, reportUrl);

                    }
                }

                // Ensure we have a valid path
                if (File.Exists(reportUrl))
                {
                    AddToOutput($"🌐 Loading report: {Path.GetFileName(reportUrl)}", LogSeverity.INFO);
                    StatusText.Text = $"Loading {Path.GetFileName(reportUrl)}... 50%";
                    OperationProgress.Value = 50;
                    NavigateToHvpReport(reportUrl);
                }
                else if (Uri.IsWellFormedUriString(reportUrl, UriKind.Absolute))
                {
                    // It's a web URL, navigate directly
                    AddToOutput($"🌐 Loading web report: {reportUrl}", LogSeverity.INFO);
                    StatusText.Text = "Loading web report... 50%";
                    OperationProgress.Value = 50;
                    NavigateToWebUrl(reportUrl);
                }
                else
                {
                    AddToOutput($"⚠️ Report file not found: {reportUrl}", LogSeverity.WARNING);
                    // Update progress to show file not found
                    StatusText.Text = "HVP file not found";
                    OperationProgress.Value = 0;
                }
            }
            else
            {
                AddToOutput($"ℹ️ No report link available for: {node.Name}", LogSeverity.INFO);
            }
        }
        catch (Exception ex)
        {
            AddToOutput($"Error handling node selection: {ex.Message}", LogSeverity.ERROR);
        }
        finally
        {
            // For errors or when no navigation occurs, hide progress immediately
            if (string.IsNullOrEmpty(node.Link))
            {
                OperationProgress.Visibility = Visibility.Collapsed;
                StatusText.Text = "Ready";
            }
            // Otherwise, let the navigation completion handlers manage progress cleanup
        }
    }

    /// <summary>
    /// Navigate to a web URL in the browser
    /// </summary>
    private void NavigateToWebUrl(string url)
    {
        try
        {
            if (HvpBrowser?.CoreWebView2 != null && !string.IsNullOrEmpty(url))
            {
                var startTime = DateTime.Now;
                AddToOutput($"⏱️ Navigating to: {url}");
                
                // Clean up previous handlers to prevent multiple firing
                if (_currentNavigationHandler != null)
                {
                    HvpBrowser.CoreWebView2.NavigationCompleted -= _currentNavigationHandler;
                }
                if (_currentDOMHandler != null)
                {
                    HvpBrowser.CoreWebView2.DOMContentLoaded -= _currentDOMHandler;
                }
                
                // Add navigation completed handler for timing and progress
                _currentNavigationHandler = (sender, args) =>
                {
                    var duration = DateTime.Now - startTime;
                    if (args.IsSuccess)
                    {
                        AddToOutput($"✅ Navigation completed in {duration.TotalSeconds:F1} seconds", LogSeverity.INFO);
                        Dispatcher.Invoke(() => 
                        {
                            StatusText.Text = $"HVP report loaded successfully! 100%";
                            OperationProgress.Value = 100;
                            
                            // Hide progress after showing completion
                            Task.Delay(2000).ContinueWith(_ => 
                            {
                                Dispatcher.Invoke(() => 
                                {
                                    OperationProgress.Visibility = Visibility.Collapsed;
                                    StatusText.Text = "Ready";
                                });
                            });
                        });
                    }
                    else
                    {
                        AddToOutput($"❌ Navigation failed after {duration.TotalSeconds:F1} seconds", LogSeverity.ERROR);
                        Dispatcher.Invoke(() => 
                        {
                            OperationProgress.Visibility = Visibility.Collapsed;
                            StatusText.Text = "Navigation failed";
                        });
                    }
                };
                HvpBrowser.CoreWebView2.NavigationCompleted += _currentNavigationHandler;
                
                HvpBrowser.CoreWebView2.Navigate(url);
            }
        }
        catch (Exception ex)
        {
            AddToOutput($"Error navigating to URL: {ex.Message}", LogSeverity.ERROR);
        }
    }

    /// <summary>
    /// Handle TreeViewItem checkbox checked event with parent-child cascading
    /// </summary>
    private void TreeViewItem_Checked(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is CheckBox checkBox && 
                checkBox.TemplatedParent is TreeViewItem treeViewItem)
            {
                _selectedTreeViewItems.Add(treeViewItem);
                
                // Try to get the associated HierarchyNode
                HierarchyNode? node = null;
                if (treeViewItem.DataContext is HierarchyNode dataNode)
                {
                    node = dataNode;
                }
                else if (treeViewItem.Header is Grid grid && grid.DataContext is HierarchyNode gridNode)
                {
                    node = gridNode;
                }

                if (node != null)
                {
                    _selectedNodes.Add(node);
                    AddToOutput($"✅ Selected: {node.Name} (Total: {_selectedNodes.Count})", LogSeverity.INFO);
                    
                    // Check all child nodes
                    CheckAllChildNodes(treeViewItem, true);
                }
                
                UpdateMultiSelectionStatus();
            }
        }
        catch (Exception ex)
        {
            AddToOutput($"Error handling checkbox selection: {ex.Message}", LogSeverity.ERROR);
        }
    }

    /// <summary>
    /// Handle TreeViewItem checkbox unchecked event with parent-child cascading
    /// </summary>
    private void TreeViewItem_Unchecked(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is CheckBox checkBox && 
                checkBox.TemplatedParent is TreeViewItem treeViewItem)
            {
                _selectedTreeViewItems.Remove(treeViewItem);
                
                // Try to get the associated HierarchyNode
                HierarchyNode? node = null;
                if (treeViewItem.DataContext is HierarchyNode dataNode)
                {
                    node = dataNode;
                }
                else if (treeViewItem.Header is Grid grid && grid.DataContext is HierarchyNode gridNode)
                {
                    node = gridNode;
                }

                if (node != null)
                {
                    _selectedNodes.Remove(node);
                    AddToOutput($"❌ Deselected: {node.Name} (Total: {_selectedNodes.Count})", LogSeverity.INFO);
                    
                    // Uncheck all child nodes
                    CheckAllChildNodes(treeViewItem, false);
                }
                
                UpdateMultiSelectionStatusCombined();
            }
        }
        catch (Exception ex)
        {
            AddToOutput($"Error handling checkbox deselection: {ex.Message}", LogSeverity.ERROR);
        }
    }

    /// <summary>
    /// Update the status bar with multi-selection information
    /// </summary>
    private void UpdateMultiSelectionStatus()
    {
        if (_selectedNodes.Count > 0)
        {
            StatusText.Text = $"Multi-Selection: {_selectedNodes.Count} nodes selected";
        }
        else
        {
            StatusText.Text = "Ready";
        }
    }

    /// <summary>
    /// Check or uncheck all child nodes recursively
    /// </summary>
    private void CheckAllChildNodes(TreeViewItem parentItem, bool isChecked)
    {
        try
        {
            foreach (TreeViewItem childItem in parentItem.Items.OfType<TreeViewItem>())
            {
                // Find the checkbox in the child item
                var checkbox = FindVisualChild<CheckBox>(childItem);
                if (checkbox != null && checkbox.IsChecked != isChecked)
                {
                    // Temporarily remove event handlers to prevent recursive calls
                    checkbox.Checked -= TreeViewItem_Checked;
                    checkbox.Unchecked -= TreeViewItem_Unchecked;
                    
                    checkbox.IsChecked = isChecked;
                    
                    // Update selection tracking
                    if (isChecked)
                    {
                        _selectedTreeViewItems.Add(childItem);
                        if (childItem.DataContext is HierarchyNode node)
                        {
                            _selectedNodes.Add(node);
                        }
                        else if (childItem.Header is Grid grid && grid.DataContext is HierarchyNode gridNode)
                        {
                            _selectedNodes.Add(gridNode);
                        }
                    }
                    else
                    {
                        _selectedTreeViewItems.Remove(childItem);
                        if (childItem.DataContext is HierarchyNode node)
                        {
                            _selectedNodes.Remove(node);
                        }
                        else if (childItem.Header is Grid grid && grid.DataContext is HierarchyNode gridNode)
                        {
                            _selectedNodes.Remove(gridNode);
                        }
                    }
                    
                    // Re-attach event handlers
                    checkbox.Checked += TreeViewItem_Checked;
                    checkbox.Unchecked += TreeViewItem_Unchecked;
                    
                    // Recursively check children
                    CheckAllChildNodes(childItem, isChecked);
                }
            }
        }
        catch (Exception ex)
        {
            AddToOutput($"Error checking child nodes: {ex.Message}", LogSeverity.ERROR);
        }
    }

    /// <summary>
    /// Helper method to find visual child of specific type
    /// </summary>
    private T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T result)
            {
                return result;
            }
            
            var childOfChild = FindVisualChild<T>(child);
            if (childOfChild != null)
            {
                return childOfChild;
            }
        }
        return null;
    }

    /// <summary>
    /// Check or uncheck all Stats child nodes recursively
    /// </summary>
    private void CheckAllStatsChildNodes(TreeViewItem parentItem, bool isChecked)
    {
        try
        {
            foreach (TreeViewItem childItem in parentItem.Items.OfType<TreeViewItem>())
            {
                // Find the checkbox in the child item (Stats uses specific name)
                var checkbox = FindVisualChild<CheckBox>(childItem);
                if (checkbox != null && checkbox.IsChecked != isChecked)
                {
                    // Temporarily remove event handlers to prevent recursive calls
                    checkbox.Checked -= StatsTreeViewItem_Checked;
                    checkbox.Unchecked -= StatsTreeViewItem_Unchecked;
                    
                    checkbox.IsChecked = isChecked;
                    
                    // Update selection tracking
                    if (isChecked)
                    {
                        _selectedStatsTreeViewItems.Add(childItem);
                        if (childItem.Header != null)
                        {
                            _selectedStatsNodes.Add(childItem.Header);
                        }
                    }
                    else
                    {
                        _selectedStatsTreeViewItems.Remove(childItem);
                        if (childItem.Header != null)
                        {
                            _selectedStatsNodes.Remove(childItem.Header);
                        }
                    }
                    
                    // Re-attach event handlers
                    checkbox.Checked += StatsTreeViewItem_Checked;
                    checkbox.Unchecked += StatsTreeViewItem_Unchecked;
                    
                    // Recursively check children
                    CheckAllStatsChildNodes(childItem, isChecked);
                }
            }
        }
        catch (Exception ex)
        {
            AddToOutput($"Error checking Stats child nodes: {ex.Message}", LogSeverity.ERROR);
        }
    }

    /// <summary>
    /// Get the currently selected nodes for multi-selection operations
    /// </summary>
    public IReadOnlySet<HierarchyNode> GetSelectedNodes()
    {
        return _selectedNodes.ToHashSet();
    }

    /// <summary>
    /// Clear all multi-selections
    /// </summary>
    public void ClearMultiSelection()
    {
        try
        {
            _selectedNodes.Clear();
            _selectedTreeViewItems.Clear();
            
            // Clear selections using data binding approach (for HierarchyNode)
            ClearHierarchyNodeSelections();
            
            // Clear selections using visual tree approach (for TreeViewItem)
            ClearCheckboxes(HvpTreeView);
            
            UpdateMultiSelectionStatus();
            AddToOutput("🔄 Cleared all selections", LogSeverity.INFO);
        }
        catch (Exception ex)
        {
            AddToOutput($"Error clearing multi-selection: {ex.Message}", LogSeverity.ERROR);
        }
    }
    
    /// <summary>
    /// Clear selections for HierarchyNode data binding approach
    /// </summary>
    private void ClearHierarchyNodeSelections()
    {
        if (HvpTreeView.ItemsSource is IEnumerable<HierarchyNode> hierarchyNodes)
        {
            foreach (var node in hierarchyNodes)
            {
                ClearHierarchyNodeRecursive(node);
            }
        }
    }
    
    /// <summary>
    /// Recursively clear HierarchyNode selections
    /// </summary>
    private void ClearHierarchyNodeRecursive(HierarchyNode node)
    {
        node.IsSelected = false;
        foreach (var child in node.Children)
        {
            ClearHierarchyNodeRecursive(child);
        }
    }

    /// <summary>
    /// Recursively clear checkboxes in TreeView
    /// </summary>
    private void ClearCheckboxes(ItemsControl parent)
    {
        for (int i = 0; i < parent.Items.Count; i++)
        {
            var container = parent.ItemContainerGenerator.ContainerFromIndex(i) as TreeViewItem;
            if (container != null)
            {
                // Try to find checkbox with current name first
                var checkbox = FindVisualChild<CheckBox>(container, "NodeCheckBox");
                
                // Fallback to old name for backwards compatibility
                if (checkbox == null)
                {
                    checkbox = FindVisualChild<CheckBox>(container, "SelectionCheckBox");
                }
                
                // Fallback to finding any checkbox if named search fails
                if (checkbox == null)
                {
                    checkbox = FindAnyVisualChild<CheckBox>(container);
                }
                
                if (checkbox != null)
                {
                    checkbox.IsChecked = false;
                }
                
                // Recursively clear children
                ClearCheckboxes(container);
            }
        }
    }

    /// <summary>
    /// Find a visual child by name in the visual tree
    /// </summary>
    private T? FindVisualChild<T>(DependencyObject parent, string childName) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            
            if (child is T typedChild && child is FrameworkElement element && element.Name == childName)
            {
                return typedChild;
            }
            
            var foundChild = FindVisualChild<T>(child, childName);
            if (foundChild != null)
                return foundChild;
        }
        return null;
    }
    
    /// <summary>
    /// Find any visual child of the specified type in the visual tree
    /// </summary>
    private T? FindAnyVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            
            if (child is T typedChild)
            {
                return typedChild;
            }
            
            var foundChild = FindAnyVisualChild<T>(child);
            if (foundChild != null)
                return foundChild;
        }
        return null;
    }

    /// <summary>
    /// Handle Clear All Selections menu click
    /// </summary>
    private void ClearAllSelections_Click(object sender, RoutedEventArgs e)
    {
        ClearMultiSelection();
    }

    /// <summary>
    /// Handle Select All menu click
    /// </summary>
    private void SelectAll_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            SelectAllNodes(HvpTreeView);
            AddToOutput($"🎯 Selected all nodes (Total: {_selectedNodes.Count})", LogSeverity.INFO);
            UpdateMultiSelectionStatus();
        }
        catch (Exception ex)
        {
            AddToOutput($"Error selecting all nodes: {ex.Message}", LogSeverity.ERROR);
        }
    }

    /// <summary>
    /// Handle Export Selected Nodes menu click
    /// </summary>
    private void ExportSelectedNodes_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_selectedNodes.Count == 0)
            {
                MessageBox.Show("No nodes selected for export.", "Export Selected Nodes", 
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var saveDialog = new SaveFileDialog
            {
                Filter = "JSON files (*.json)|*.json|Text files (*.txt)|*.txt|All files (*.*)|*.*",
                DefaultExt = "json",
                FileName = "selected_nodes.json"
            };

            if (saveDialog.ShowDialog() == true)
            {
                var exportData = _selectedNodes.Select(node => new
                {
                    Name = node.Name,
                    FullPath = node.FullPath,
                    Link = node.Link,
                    CoveragePercentage = node.CoveragePercentage,
                    LinesCovered = node.LinesCovered,
                    TotalLines = node.TotalLines
                }).ToArray();

                string json = System.Text.Json.JsonSerializer.Serialize(exportData, new System.Text.Json.JsonSerializerOptions
                {
                    WriteIndented = true
                });

                File.WriteAllText(saveDialog.FileName, json);
                AddToOutput($"📄 Exported {_selectedNodes.Count} selected nodes to {saveDialog.FileName}", LogSeverity.INFO);
            }
        }
        catch (Exception ex)
        {
            AddToOutput($"Error exporting selected nodes: {ex.Message}", LogSeverity.ERROR);
        }
    }

    /// <summary>
    /// Recursively select all nodes in the TreeView
    /// </summary>
    private void SelectAllNodes(ItemsControl parent)
    {
        for (int i = 0; i < parent.Items.Count; i++)
        {
            var container = parent.ItemContainerGenerator.ContainerFromIndex(i) as TreeViewItem;
            if (container != null)
            {
                // Find checkbox in the container template
                var checkbox = FindVisualChild<CheckBox>(container, "SelectionCheckBox");
                if (checkbox != null)
                {
                    checkbox.IsChecked = true;
                }
                
                // Recursively select children
                SelectAllNodes(container);
            }
        }
    }

    #region Tools Menu Event Handlers

    /// <summary>
    /// Handle Select All Trees menu click
    /// </summary>
    private void SelectAllTrees_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // Select all HVP nodes
            SelectAllNodes(HvpTreeView);
            
            // Select all Stats nodes
            SelectAllStatsNodes(StatsTreeView);
            
            AddToOutput($"🎯 Selected all nodes in both trees", LogSeverity.INFO);
            UpdateMultiSelectionStatusCombined();
        }
        catch (Exception ex)
        {
            AddToOutput($"Error selecting all trees: {ex.Message}", LogSeverity.ERROR);
        }
    }

    /// <summary>
    /// Handle Clear All Trees menu click
    /// </summary>
    private void ClearAllTrees_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            ClearMultiSelection();
            ClearStatsMultiSelection();
            AddToOutput($"🔄 Cleared all selections in both trees", LogSeverity.INFO);
        }
        catch (Exception ex)
        {
            AddToOutput($"Error clearing all trees: {ex.Message}", LogSeverity.ERROR);
        }
    }

    /// <summary>
    /// Handle Select All HVP Nodes menu click
    /// </summary>
    private void SelectAllHvpNodes_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            SelectAllNodes(HvpTreeView);
            AddToOutput($"🎯 Selected all HVP nodes", LogSeverity.INFO);
            UpdateMultiSelectionStatusCombined();
        }
        catch (Exception ex)
        {
            AddToOutput($"Error selecting all HVP nodes: {ex.Message}", LogSeverity.ERROR);
        }
    }

    /// <summary>
    /// Handle Clear HVP Selections menu click
    /// </summary>
    private void ClearHvpSelections_Click(object sender, RoutedEventArgs e)
    {
        ClearMultiSelection();
    }

    /// <summary>
    /// Handle Select All Stats Nodes menu click
    /// </summary>
    private void SelectAllStatsNodes_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            SelectAllStatsNodes(StatsTreeView);
            AddToOutput($"🎯 Selected all Stats nodes", LogSeverity.INFO);
            UpdateMultiSelectionStatusCombined();
        }
        catch (Exception ex)
        {
            AddToOutput($"Error selecting all Stats nodes: {ex.Message}", LogSeverity.ERROR);
        }
    }

    /// <summary>
    /// Handle Clear Stats Selections menu click
    /// </summary>
    private void ClearStatsSelections_Click(object sender, RoutedEventArgs e)
    {
        ClearStatsMultiSelection();
    }

    #endregion

    #region Master Checkbox Event Handlers

    /// <summary>
    /// Handle master HVP checkbox checked
    /// </summary>
    private void MasterHvpCheckBox_Checked(object sender, RoutedEventArgs e)
    {
        try
        {
            SelectAllNodes(HvpTreeView);
            AddToOutput($"🎯 Master checkbox: Selected all HVP nodes", LogSeverity.INFO);
            UpdateMultiSelectionStatusCombined();
        }
        catch (Exception ex)
        {
            AddToOutput($"Error in master HVP checkbox: {ex.Message}", LogSeverity.ERROR);
        }
    }

    /// <summary>
    /// Handle master HVP checkbox unchecked
    /// </summary>
    private void MasterHvpCheckBox_Unchecked(object sender, RoutedEventArgs e)
    {
        ClearMultiSelection();
    }

    /// <summary>
    /// Handle master Stats checkbox checked
    /// </summary>
    private void MasterStatsCheckBox_Checked(object sender, RoutedEventArgs e)
    {
        try
        {
            SelectAllStatsNodes(StatsTreeView);
            AddToOutput($"🎯 Master checkbox: Selected all Stats nodes", LogSeverity.INFO);
            UpdateMultiSelectionStatusCombined();
        }
        catch (Exception ex)
        {
            AddToOutput($"Error in master Stats checkbox: {ex.Message}", LogSeverity.ERROR);
        }
    }

    /// <summary>
    /// Handle master Stats checkbox unchecked
    /// </summary>
    private void MasterStatsCheckBox_Unchecked(object sender, RoutedEventArgs e)
    {
        ClearStatsMultiSelection();
    }

    #endregion

    #region Stats Tree Multi-Selection

    /// <summary>
    /// Handle Stats TreeViewItem checkbox checked with parent-child cascading
    /// </summary>
    private void StatsTreeViewItem_Checked(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is CheckBox checkBox && 
                checkBox.TemplatedParent is TreeViewItem treeViewItem)
            {
                _selectedStatsTreeViewItems.Add(treeViewItem);
                
                // Store the header content as the selected node
                if (treeViewItem.Header != null)
                {
                    _selectedStatsNodes.Add(treeViewItem.Header);
                    AddToOutput($"✅ Stats Selected: {treeViewItem.Header} (Total: {_selectedStatsNodes.Count})", LogSeverity.INFO);
                    
                    // Check all child nodes
                    CheckAllStatsChildNodes(treeViewItem, true);
                }
                
                UpdateMultiSelectionStatusCombined();
            }
        }
        catch (Exception ex)
        {
            AddToOutput($"Error handling Stats checkbox selection: {ex.Message}", LogSeverity.ERROR);
        }
    }

    /// <summary>
    /// Handle Stats TreeViewItem checkbox unchecked with parent-child cascading
    /// </summary>
    private void StatsTreeViewItem_Unchecked(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is CheckBox checkBox && 
                checkBox.TemplatedParent is TreeViewItem treeViewItem)
            {
                _selectedStatsTreeViewItems.Remove(treeViewItem);
                
                if (treeViewItem.Header != null)
                {
                    _selectedStatsNodes.Remove(treeViewItem.Header);
                    AddToOutput($"❌ Stats Deselected: {treeViewItem.Header} (Total: {_selectedStatsNodes.Count})", LogSeverity.INFO);
                    
                    // Uncheck all child nodes
                    CheckAllStatsChildNodes(treeViewItem, false);
                }
                
                UpdateMultiSelectionStatusCombined();
            }
        }
        catch (Exception ex)
        {
            AddToOutput($"Error handling Stats checkbox deselection: {ex.Message}", LogSeverity.ERROR);
        }
    }

    /// <summary>
    /// Clear all Stats multi-selections
    /// </summary>
    public void ClearStatsMultiSelection()
    {
        try
        {
            _selectedStatsNodes.Clear();
            _selectedStatsTreeViewItems.Clear();
            
            // Uncheck all checkboxes in the stats tree
            ClearStatsCheckboxes(StatsTreeView);
            
            UpdateMultiSelectionStatusCombined();
            AddToOutput("🔄 Cleared all Stats selections", LogSeverity.INFO);
        }
        catch (Exception ex)
        {
            AddToOutput($"Error clearing Stats multi-selection: {ex.Message}", LogSeverity.ERROR);
        }
    }

    /// <summary>
    /// Recursively select all nodes in the Stats TreeView
    /// </summary>
    private void SelectAllStatsNodes(ItemsControl parent)
    {
        for (int i = 0; i < parent.Items.Count; i++)
        {
            var container = parent.ItemContainerGenerator.ContainerFromIndex(i) as TreeViewItem;
            if (container != null)
            {
                // Find checkbox in the container template
                var checkbox = FindVisualChild<CheckBox>(container, "StatsSelectionCheckBox");
                if (checkbox != null)
                {
                    checkbox.IsChecked = true;
                }
                
                // Recursively select children
                SelectAllStatsNodes(container);
            }
        }
    }

    /// <summary>
    /// Recursively clear checkboxes in Stats TreeView
    /// </summary>
    private void ClearStatsCheckboxes(ItemsControl parent)
    {
        for (int i = 0; i < parent.Items.Count; i++)
        {
            var container = parent.ItemContainerGenerator.ContainerFromIndex(i) as TreeViewItem;
            if (container != null)
            {
                // Find checkbox in the container template
                var checkbox = FindVisualChild<CheckBox>(container, "StatsSelectionCheckBox");
                if (checkbox != null)
                {
                    checkbox.IsChecked = false;
                }
                
                // Recursively clear children
                ClearStatsCheckboxes(container);
            }
        }
    }

    /// <summary>
    /// Get the currently selected Stats nodes
    /// </summary>
    public IReadOnlySet<object> GetSelectedStatsNodes()
    {
        return _selectedStatsNodes.ToHashSet();
    }

    #endregion

    /// <summary>
    /// Update the status bar with multi-selection information for both trees
    /// </summary>
    protected void UpdateMultiSelectionStatusCombined()
    {
        var hvpCount = _selectedNodes.Count;
        var statsCount = _selectedStatsNodes.Count;
        
        if (hvpCount > 0 || statsCount > 0)
        {
            StatusText.Text = $"Multi-Selection: {hvpCount} HVP nodes, {statsCount} Stats nodes selected";
        }
        else
        {
            StatusText.Text = "Ready";
        }
    }

    #region Simple Panel Docking - Context Menu Approach

    /// <summary>
    /// Handle right-click on Solution Explorer header for docking options
    /// </summary>
    private void SolutionExplorerHeader_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2) // Double-click to cycle through positions
        {
            CycleSolutionExplorerPosition();
        }
    }

    /// <summary>
    /// Handle right-click on Output header for docking options
    /// </summary>
    private void OutputHeader_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2) // Double-click to cycle through positions
        {
            CycleOutputPanelPosition();
        }
    }

    /// <summary>
    /// Cycle Solution Explorer through different dock positions
    /// </summary>
    private void CycleSolutionExplorerPosition()
    {
        // AvalonDock handles panel positioning - show message for now
        AddToOutput("Solution Explorer positioning is handled by AvalonDock - drag and drop panels to reposition");
    }

    /// <summary>
    /// Cycle Output Panel through different dock positions - AvalonDock handles this automatically
    /// </summary>
    private void CycleOutputPanelPosition()
    {
        // AvalonDock handles panel positioning - show message for now
        AddToOutput("Output panel positioning is handled by AvalonDock - drag and drop panels to reposition");
    }

    /// <summary>
    /// Move a panel to a specific dock position
    /// </summary>
    private void MovePanelToPosition(FrameworkElement panel, Dock newDock, string positionName)
    {
        // Remove panel from current position
        var dockPanel = panel.Parent as DockPanel;
        if (dockPanel != null)
        {
            dockPanel.Children.Remove(panel);
            
            // Set new dock position
            DockPanel.SetDock(panel, newDock);
            
            // Update panel dimensions based on new position
            if (newDock == Dock.Left || newDock == Dock.Right)
            {
                panel.Width = panel.Name == "SolutionExplorerPanel" ? 300 : 250;
                panel.Height = double.NaN; // Auto height
                panel.ClearValue(FrameworkElement.MaxHeightProperty);
                panel.MinWidth = 150;
                panel.MaxWidth = 600;
            }
            else if (newDock == Dock.Top || newDock == Dock.Bottom)
            {
                panel.Height = panel.Name == "OutputPanel" ? 200 : 150;
                panel.Width = double.NaN; // Auto width
                panel.ClearValue(FrameworkElement.MaxWidthProperty);
                panel.MinHeight = 100;
                panel.MaxHeight = 400;
            }
            
            // Add panel back at the beginning (highest priority)
            dockPanel.Children.Insert(0, panel);
            
            AddToOutput($"{panel.Name} moved to {positionName}");
            StatusText.Text = $"Panel repositioned to {positionName}";
            
            // Force layout update
            dockPanel.UpdateLayout();
        }
    }

    // Context Menu Event Handlers - AvalonDock handles docking automatically
    private void DockSolutionExplorerLeft_Click(object sender, RoutedEventArgs e) => AddToOutput("AvalonDock handles panel positioning - drag and drop to reposition panels");
    private void DockSolutionExplorerRight_Click(object sender, RoutedEventArgs e) => AddToOutput("AvalonDock handles panel positioning - drag and drop to reposition panels");
    private void DockSolutionExplorerTop_Click(object sender, RoutedEventArgs e) => AddToOutput("AvalonDock handles panel positioning - drag and drop to reposition panels");
    private void DockSolutionExplorerBottom_Click(object sender, RoutedEventArgs e) => AddToOutput("AvalonDock handles panel positioning - drag and drop to reposition panels");

    // Context Menu Event Handlers for Output Panel
    private void DockOutputLeft_Click(object sender, RoutedEventArgs e) => AddToOutput("AvalonDock handles panel positioning - drag and drop to reposition panels");
    private void DockOutputRight_Click(object sender, RoutedEventArgs e) => AddToOutput("AvalonDock handles panel positioning - drag and drop to reposition panels");
    private void DockOutputTop_Click(object sender, RoutedEventArgs e) => AddToOutput("AvalonDock handles panel positioning - drag and drop to reposition panels");
    private void DockOutputBottom_Click(object sender, RoutedEventArgs e) => AddToOutput("AvalonDock handles panel positioning - drag and drop to reposition panels");

    #endregion
}
