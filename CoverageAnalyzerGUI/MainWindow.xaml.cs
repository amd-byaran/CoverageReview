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
using Microsoft.Web.WebView2.Wpf;

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
    public string? Link { get; set; } // URL/path to the report file for this node
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
/// Utility class for creating color spectrum from red to yellow to green
/// </summary>
public static class ColorSpectrum
{
    /// <summary>
    /// Creates a color based on percentage (0-100) from red (0%) to yellow (50%) to green (100%)
    /// </summary>
    /// <param name="percentage">Percentage value (0-100)</param>
    /// <returns>SolidColorBrush with appropriate color</returns>
    public static SolidColorBrush GetColorForPercentage(double percentage)
    {
        // Clamp percentage to 0-100 range
        percentage = Math.Max(0, Math.Min(100, percentage));
        
        byte red, green, blue;
        
        if (percentage <= 50)
        {
            // From red (0%) to yellow (50%)
            // Red stays at 255, green increases from 0 to 255
            red = 255;
            green = (byte)(255 * (percentage / 50.0));
            blue = 0;
        }
        else
        {
            // From yellow (50%) to green (100%)
            // Green stays at 255, red decreases from 255 to 0
            red = (byte)(255 * ((100 - percentage) / 50.0));
            green = 255;
            blue = 0;
        }
        
        return new SolidColorBrush(Color.FromRgb(red, green, blue));
    }
}

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private ProjectSettings? _currentProject;
    private HttpClient? _authenticatedHttpClient;
    private bool _statsLoaded = false;
    
    // Project information for display in status bar
    public string ReleaseName { get; private set; } = string.Empty;
    public string CoverageType { get; private set; } = string.Empty;
    public string ReportName { get; private set; } = string.Empty;
    public string ReportType { get; private set; } = string.Empty;
    public string Changelist { get; private set; } = string.Empty;

    public MainWindow()
    {
        InitializeComponent();
        
        // Enable mouse wheel scrolling for TreeView and ScrollViewers
        SolutionExplorer.PreviewMouseWheel += SolutionExplorer_PreviewMouseWheel;
        
        try
        {
            LogToFile("=== APPLICATION STARTUP ===");
            LogToFile($"MainWindow constructor started at {DateTime.Now}");
            
            AddToOutput("Welcome to Coverage Analyzer GUI");
            AddToOutput("Ready to create or open a project");
            UpdateWindowTitle();
            
            // Initialize WebView2
            InitializeWebView();
            
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
            
            // Test the authentication by making a simple request
            try
            {
                var testResponse = await httpClient.GetAsync(serverUrl);
                
                if (testResponse.IsSuccessStatusCode || testResponse.StatusCode != System.Net.HttpStatusCode.Unauthorized)
                {
                    AddToOutput($"✅ Windows authentication successful for {serverUrl}", LogSeverity.INFO);
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
    /// Configure comprehensive WebView2 authentication using JavaScript injection
    /// </summary>
    private void ConfigureWebView2Authentication(string username, string password, string encodedCredentials)
    {
        try
        {
            var coreWebView2 = HvpBrowser.CoreWebView2;
            
            // Use JavaScript injection to handle authentication automatically
            coreWebView2.DOMContentLoaded += async (sender, args) =>
            {
                try
                {
                    // Inject comprehensive authentication handling
                    var script = $@"
                        (function() {{
                            // Store credentials for automatic authentication
                            window._webview2Auth = {{
                                header: 'Basic {encodedCredentials}',
                                username: '{username}',
                                password: '{password}',
                                enabled: true
                            }};
                            
                            console.log('WebView2: Auth credentials stored for automatic use');
                            
                            // Override XMLHttpRequest to add auth headers automatically
                            const originalOpen = XMLHttpRequest.prototype.open;
                            XMLHttpRequest.prototype.open = function(method, url, async, user, password) {{
                                this._method = method;
                                this._url = url;
                                return originalOpen.call(this, method, url, async, user, password);
                            }};
                            
                            const originalSend = XMLHttpRequest.prototype.send;
                            XMLHttpRequest.prototype.send = function(data) {{
                                if (this._url && (this._url.startsWith('http://') || this._url.startsWith('https://'))) {{
                                    if (window._webview2Auth && window._webview2Auth.enabled) {{
                                        this.setRequestHeader('Authorization', window._webview2Auth.header);
                                        console.log('WebView2: Auto-auth header added to XMLHttpRequest:', this._url);
                                    }}
                                }}
                                return originalSend.call(this, data);
                            }};
                            
                            // Override fetch to add auth headers automatically
                            const originalFetch = window.fetch;
                            window.fetch = function(input, init = {{}}) {{
                                const url = typeof input === 'string' ? input : input.url;
                                if (url && (url.startsWith('http://') || url.startsWith('https://'))) {{
                                    if (window._webview2Auth && window._webview2Auth.enabled) {{
                                        init.headers = init.headers || {{}};
                                        init.headers['Authorization'] = window._webview2Auth.header;
                                        console.log('WebView2: Auto-auth header added to fetch:', url);
                                    }}
                                }}
                                return originalFetch.call(this, input, init);
                            }};
                            
                            // Handle authentication dialogs automatically
                            const handleAuthDialog = function() {{
                                // Look for authentication forms and auto-fill them
                                const usernameFields = document.querySelectorAll('input[type=""text""], input[name*=""user""], input[id*=""user""]');
                                const passwordFields = document.querySelectorAll('input[type=""password""]');
                                
                                if (usernameFields.length > 0 && passwordFields.length > 0) {{
                                    usernameFields[0].value = window._webview2Auth.username;
                                    passwordFields[0].value = window._webview2Auth.password;
                                    
                                    // Try to submit the form
                                    const form = usernameFields[0].closest('form');
                                    if (form) {{
                                        const submitBtn = form.querySelector('button[type=""submit""], input[type=""submit""]');
                                        if (submitBtn) {{
                                            console.log('WebView2: Auto-submitting authentication form');
                                            submitBtn.click();
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
                            
                            console.log('WebView2: Comprehensive authentication system initialized');
                        }})();
                    ";
                    
                    await coreWebView2.ExecuteScriptAsync(script);
                    AddToOutput("✅ WebView2 auto-authentication system activated", LogSeverity.INFO);
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
            
            AddToOutput($"✅ WebView2 authentication configured for user: {username}", LogSeverity.INFO);
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
                
                // Add DOM content loaded handler for better timing
                HvpBrowser.CoreWebView2.DOMContentLoaded += (sender, args) =>
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
                
                // Add navigation completed handler as fallback
                HvpBrowser.CoreWebView2.NavigationCompleted += (sender, args) =>
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
            _statsLoaded = false; // Reset stats loaded flag for new project
            
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
                _statsLoaded = false; // Reset stats loaded flag for loaded project
                
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
        
        // Automatically load HVP data (authentication is now ready if needed)
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
                
                AddToOutput($"✓ HTTP authentication configured for server: {serverUrl}");
                
                // Configure WebView2 to use the same authentication
                ConfigureWebViewAuthentication();
            }
            else
            {
                AddToOutput("⚠ Windows authentication failed, showing login dialog", LogSeverity.INFO);
                
                // Fallback to manual authentication dialog
                // Just use the simple Windows username as default (no password extraction needed)
                string defaultUsername = Environment.UserName ?? "";

                
                var (dialogSuccess, dialogHttpClient, rememberCredentials) = HttpAuthDialog.GetHttpAuthentication(this, serverUrl, defaultUsername);
                


                
                if (dialogSuccess && dialogHttpClient != null)
                {

                    
                    // Store the authenticated HTTP client
                    _authenticatedHttpClient?.Dispose();
                    _authenticatedHttpClient = dialogHttpClient;
                    
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
        
        // Configure WebView2 to use the same authentication
        ConfigureWebViewAuthentication();
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
                                SolutionExplorer.Items.Clear();
                                SolutionExplorer.ItemsSource = treeItems;
                                // TreeView populated successfully
                            }
                            else
                            {
                                Dispatcher.Invoke(() => {
                                    // Clear existing items before setting ItemsSource
                                    SolutionExplorer.Items.Clear();
                                    SolutionExplorer.ItemsSource = treeItems;
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
            return new List<System.Windows.Controls.TreeViewItem> { CreateTreeViewItemFromHvpNode(hvpNode, isRoot: true) };
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
    private System.Windows.Controls.TreeViewItem CreateTreeViewItemFromHvpNode(object hvpNode, bool isRoot = false)
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
        
        // Root node analysis removed for performance
        
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
        var unknownCount = unknownCountProperty?.GetValue(hvpNode);
        
        // Score debugging removed for performance
        
        // Create table row structure as Header
        var tableRow = new Grid();
        tableRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(184) }); // Name - wider for better readability
        tableRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });  // Score - wider for percentages
        tableRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) });  // GroupScore - wider for percentages
        tableRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(60) });  // TestCount
        tableRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(60) });  // PassCount
        tableRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(60) });  // FailCount
        tableRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(60) });  // WarnCount
        tableRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(70) });  // AssertCount
        tableRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(70) });  // UnknownCount
        tableRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Remaining space
        
        // Create text blocks for each column
        var nameText = new TextBlock 
        { 
            Text = nodeName, 
            VerticalAlignment = VerticalAlignment.Center,
            Padding = new Thickness(4, 2, 4, 2),
            Foreground = new SolidColorBrush(Colors.Black)
        };
        Grid.SetColumn(nameText, 0);
        tableRow.Children.Add(nameText);
        
        // Score column with color coding
        var scoreText = new TextBlock 
        { 
            Text = score is double scoreVal ? $"{scoreVal:F1}%" : "",
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right,
            Padding = new Thickness(4, 2, 4, 2),
            FontFamily = new FontFamily("Consolas"),
            Foreground = new SolidColorBrush(Colors.Black)
        };
        if (score is double scoreDouble)
        {
            scoreText.Background = ColorSpectrum.GetColorForPercentage(scoreDouble);
        }
        Grid.SetColumn(scoreText, 1);
        tableRow.Children.Add(scoreText);
        
        // GroupScore column with color coding
        var groupScoreText = new TextBlock 
        { 
            Text = groupScore is double groupScoreVal ? $"{groupScoreVal:F1}%" : "",
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right,
            Padding = new Thickness(4, 2, 4, 2),
            FontFamily = new FontFamily("Consolas"),
            Foreground = new SolidColorBrush(Colors.Black)
        };
        if (groupScore is double groupScoreDouble)
        {
            groupScoreText.Background = ColorSpectrum.GetColorForPercentage(groupScoreDouble);
        }
        Grid.SetColumn(groupScoreText, 2);
        tableRow.Children.Add(groupScoreText);
        
        // TestCount column
        var testCountText = new TextBlock 
        { 
            Text = testCount?.ToString() ?? "",
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right,
            Padding = new Thickness(4, 2, 4, 2),
            FontFamily = new FontFamily("Consolas"),
            Foreground = new SolidColorBrush(Colors.Black)
        };
        Grid.SetColumn(testCountText, 3);
        tableRow.Children.Add(testCountText);
        
        // PassCount column
        var passCountText = new TextBlock 
        { 
            Text = passCount?.ToString() ?? "",
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right,
            Padding = new Thickness(4, 2, 4, 2),
            FontFamily = new FontFamily("Consolas"),
            Foreground = new SolidColorBrush(Colors.Black)
        };
        Grid.SetColumn(passCountText, 4);
        tableRow.Children.Add(passCountText);
        
        // FailCount column
        var failCountText = new TextBlock 
        { 
            Text = failCount?.ToString() ?? "",
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right,
            Padding = new Thickness(4, 2, 4, 2),
            FontFamily = new FontFamily("Consolas"),
            Foreground = new SolidColorBrush(Colors.Black)
        };
        Grid.SetColumn(failCountText, 5);
        tableRow.Children.Add(failCountText);
        
        // WarnCount column
        var warnCountText = new TextBlock 
        { 
            Text = warnCount?.ToString() ?? "",
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right,
            Padding = new Thickness(4, 2, 4, 2),
            FontFamily = new FontFamily("Consolas"),
            Foreground = new SolidColorBrush(Colors.Black)
        };
        Grid.SetColumn(warnCountText, 6);
        tableRow.Children.Add(warnCountText);
        
        // AssertCount column
        var assertCountText = new TextBlock 
        { 
            Text = assertCount?.ToString() ?? "",
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right,
            Padding = new Thickness(4, 2, 4, 2),
            FontFamily = new FontFamily("Consolas"),
            Foreground = new SolidColorBrush(Colors.Black)
        };
        Grid.SetColumn(assertCountText, 7);
        tableRow.Children.Add(assertCountText);
        
        // UnknownCount column
        var unknownCountText = new TextBlock 
        { 
            Text = unknownCount?.ToString() ?? "",
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right,
            Padding = new Thickness(4, 2, 4, 2),
            FontFamily = new FontFamily("Consolas"),
            Foreground = new SolidColorBrush(Colors.Black)
        };
        Grid.SetColumn(unknownCountText, 8);
        tableRow.Children.Add(unknownCountText);
        
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
                        var childItem = CreateTreeViewItemFromHvpNode(child, isRoot: false);
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
                // Calculate scroll amount (3 lines per wheel click is typical)
                double scrollAmount = -e.Delta / 40.0; // Standard scroll amount
                
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
    private void SolutionExplorer_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
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
                
                // Add navigation completed handler for timing and progress
                HvpBrowser.CoreWebView2.NavigationCompleted += (sender, args) =>
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
                
                HvpBrowser.CoreWebView2.Navigate(url);
            }
        }
        catch (Exception ex)
        {
            AddToOutput($"Error navigating to URL: {ex.Message}", LogSeverity.ERROR);
        }
    }
}
