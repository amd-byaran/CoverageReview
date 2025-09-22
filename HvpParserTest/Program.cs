using System;
using System.Net.Http;
using System.Threading.Tasks;
using HvpHtmlParser;

namespace HvpParserTest
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("=== HvpHtmlParser Console Test ===");
            Console.WriteLine($"Started at: {DateTime.Now}");
            Console.WriteLine();

            // Get URL from command line or use default
            string testUrl = args.Length > 0 ? args[0] : "";
            
            if (string.IsNullOrEmpty(testUrl))
            {
                Console.WriteLine("Usage: HvpParserTest.exe <hvp-file-url>");
                Console.WriteLine("Example: HvpParserTest.exe \"https://server.com/path/to/file.hvp\"");
                Console.WriteLine();
                Console.Write("Or enter URL now: ");
                testUrl = Console.ReadLine() ?? "";
                
                if (string.IsNullOrEmpty(testUrl))
                {
                    Console.WriteLine("No URL provided. Exiting.");
                    return;
                }
            }

            Console.WriteLine($"Testing URL: {testUrl}");
            Console.WriteLine();

            try
            {
                // Test 1: Basic HtmlReader creation
                Console.WriteLine("Step 1: Creating HtmlReader...");
                var reader = new HtmlReader();
                Console.WriteLine("✓ HtmlReader created successfully");
                Console.WriteLine();

                // Test 2: Check if URL requires authentication
                bool needsAuth = testUrl.StartsWith("http://") || testUrl.StartsWith("https://");
                
                if (needsAuth)
                {
                    Console.WriteLine("Step 2: URL requires HTTP authentication");
                    Console.Write("Enter username (or press Enter to skip auth): ");
                    string? username = Console.ReadLine();
                    
                    if (!string.IsNullOrEmpty(username))
                    {
                        Console.Write("Enter password: ");
                        string password = ReadPassword();
                        Console.WriteLine();
                        
                        Console.WriteLine("Setting up HTTP authentication...");
                        
                        // Create HttpClient with authentication
                        var handler = new HttpClientHandler();
                        var httpClient = new HttpClient(handler);
                        
                        // Add basic authentication
                        var credentials = Convert.ToBase64String(
                            System.Text.Encoding.ASCII.GetBytes($"{username}:{password}"));
                        httpClient.DefaultRequestHeaders.Authorization = 
                            new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", credentials);
                        
                        reader.SetHttpClient(httpClient);
                        Console.WriteLine("✓ HTTP authentication configured");
                        Console.WriteLine();
                    }
                    else
                    {
                        Console.WriteLine("⚠ Skipping authentication - may fail for protected resources");
                        Console.WriteLine();
                    }
                }
                else
                {
                    Console.WriteLine("Step 2: Local file, no authentication needed");
                    Console.WriteLine();
                }

                // Test 3: Parse the file
                Console.WriteLine("Step 3: Calling ParseFile...");
                Console.WriteLine($"Target: {testUrl}");
                Console.WriteLine("This may take a while for large files or slow connections...");
                Console.WriteLine();
                
                var startTime = DateTime.Now;
                var result = await reader.ParseFile(testUrl);
                var endTime = DateTime.Now;
                var duration = endTime - startTime;
                
                Console.WriteLine($"✓ ParseFile completed successfully!");
                Console.WriteLine($"Duration: {duration.TotalSeconds:F2} seconds");
                Console.WriteLine();

                // Test 4: Analyze the result
                Console.WriteLine("Step 4: Analyzing result...");
                
                if (result == null)
                {
                    Console.WriteLine("⚠ Result is null");
                }
                else
                {
                    var resultType = result.GetType();
                    Console.WriteLine($"Result type: {resultType.Name}");
                    
                    // Try to get basic properties
                    var nameProperty = resultType.GetProperty("Name");
                    var childrenProperty = resultType.GetProperty("Children");
                    
                    if (nameProperty != null)
                    {
                        var name = nameProperty.GetValue(result)?.ToString();
                        Console.WriteLine($"Root name: {name}");
                    }
                    
                    if (childrenProperty != null)
                    {
                        var children = childrenProperty.GetValue(result);
                        if (children is System.Collections.IEnumerable enumerable)
                        {
                            var childCount = 0;
                            foreach (var child in enumerable)
                            {
                                childCount++;
                                if (childCount > 10) break; // Don't count too many
                            }
                            Console.WriteLine($"Child count: {childCount}{(childCount > 10 ? "+" : "")}");
                        }
                    }
                    
                    Console.WriteLine("✓ Result analysis complete");
                }
                
                Console.WriteLine();
                Console.WriteLine("=== TEST COMPLETED SUCCESSFULLY ===");
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"❌ HTTP ERROR: {ex.Message}");
                Console.WriteLine();
                Console.WriteLine("Possible causes:");
                Console.WriteLine("- Network connectivity issues");
                Console.WriteLine("- VPN required but not connected");
                Console.WriteLine("- Server not responding");
                Console.WriteLine("- Authentication required");
                Console.WriteLine("- SSL/TLS certificate issues");
            }
            catch (UnauthorizedAccessException ex)
            {
                Console.WriteLine($"❌ AUTHENTICATION ERROR: {ex.Message}");
                Console.WriteLine();
                Console.WriteLine("The username/password is incorrect or insufficient permissions.");
            }
            catch (TaskCanceledException ex)
            {
                Console.WriteLine($"❌ TIMEOUT ERROR: {ex.Message}");
                Console.WriteLine();
                Console.WriteLine("The operation timed out. This could be due to:");
                Console.WriteLine("- Very large file size");
                Console.WriteLine("- Slow network connection");
                Console.WriteLine("- Server not responding");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ UNEXPECTED ERROR: {ex.Message}");
                Console.WriteLine();
                Console.WriteLine($"Exception type: {ex.GetType().Name}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner exception: {ex.InnerException.Message}");
                }
                Console.WriteLine();
                Console.WriteLine("Stack trace:");
                Console.WriteLine(ex.StackTrace);
            }
            
            Console.WriteLine();
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }
        
        // Helper method to read password without displaying it
        private static string ReadPassword()
        {
            string password = "";
            ConsoleKeyInfo key;
            
            do
            {
                key = Console.ReadKey(true);
                
                if (key.Key != ConsoleKey.Backspace && key.Key != ConsoleKey.Enter)
                {
                    password += key.KeyChar;
                    Console.Write("*");
                }
                else if (key.Key == ConsoleKey.Backspace && password.Length > 0)
                {
                    password = password[0..^1];
                    Console.Write("\b \b");
                }
            }
            while (key.Key != ConsoleKey.Enter);
            
            return password;
        }
    }
}