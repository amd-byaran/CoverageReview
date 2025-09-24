using System;
using System.Net.Http;
using System.Threading.Tasks;
using HvpHtmlParser;

namespace CoverageAnalyzer.Examples
{
    /// <summary>
    /// Example demonstrating comprehensive usage of HtmlReader as a universal parser
    /// for various file types and scenarios.
    /// </summary>
    public class HtmlReaderUsageExample
    {
        /// <summary>
        /// Demonstrates universal parsing capabilities of HtmlReader
        /// </summary>
        public static async Task DemonstrateUniversalParsing()
        {
            var reader = new HtmlReader();
            
            // Example 1: Parse different file types - HtmlReader auto-detects format
            var testFiles = new[]
            {
                "https://server.com/coverage/hvp.dcn_core.html",     // Returns HvpNode
                "https://server.com/stats/stats.html.gz",           // Returns StatsNode  
                "https://server.com/reports/coverage.html",         // Returns appropriate node type
                @"C:\local\reports\coverage_report.html",           // Local file parsing
                @"C:\compressed\report.html.gz"                     // Compressed file parsing
            };
            
            foreach (var file in testFiles)
            {
                try
                {
                    Console.WriteLine($"🔍 Parsing: {file}");
                    
                    // Universal parser automatically:
                    // 1. Detects file format (HTML, compressed, local vs remote)
                    // 2. Chooses appropriate parsing strategy
                    // 3. Returns correct data structure type
                    var result = await reader.ParseFile(file);
                    
                    Console.WriteLine($"✅ Parsed successfully as: {result?.GetType().Name}");
                    
                    // Process result based on actual type returned
                    await ProcessUniversalResult(result);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Failed to parse {file}: {ex.Message}");
                }
            }
        }
        
        /// <summary>
        /// Set up authenticated HTTP client for protected resources
        /// </summary>
        public static HtmlReader CreateAuthenticatedReader(string username, string password)
        {
            var reader = new HtmlReader();
            
            // Create HTTP client with authentication
            var httpClient = new HttpClient();
            var credentials = Convert.ToBase64String(
                System.Text.Encoding.ASCII.GetBytes($"{username}:{password}")
            );
            httpClient.DefaultRequestHeaders.Authorization = 
                new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", credentials);
                
            // Configure reader for authenticated requests
            reader.SetHttpClient(httpClient);
            
            return reader;
        }
        
        /// <summary>
        /// Process results from universal parser based on actual type returned
        /// </summary>
        public static async Task ProcessUniversalResult(object result)
        {
            if (result == null)
            {
                Console.WriteLine("⚠️  No data returned");
                return;
            }
            
            // Universal result processing - handle different node types
            var resultType = result.GetType();
            Console.WriteLine($"📊 Processing {resultType.Name}...");
            
            // Common properties most node types have
            var nameProperty = resultType.GetProperty("Name");
            var childrenProperty = resultType.GetProperty("Children");
            var attributesProperty = resultType.GetProperty("Attributes");
            
            if (nameProperty != null)
            {
                var name = nameProperty.GetValue(result)?.ToString();
                Console.WriteLine($"   📁 Root: {name}");
            }
            
            if (childrenProperty != null)
            {
                var children = childrenProperty.GetValue(result);
                if (children is System.Collections.IEnumerable enumerable)
                {
                    var count = 0;
                    foreach (var child in enumerable)
                    {
                        count++;
                        if (count <= 5) // Show first few children
                        {
                            var childName = child.GetType().GetProperty("Name")?.GetValue(child);
                            Console.WriteLine($"     └─ {childName}");
                        }
                    }
                    if (count > 5)
                        Console.WriteLine($"     └─ ... and {count - 5} more children");
                        
                    Console.WriteLine($"   📈 Total children: {count}");
                }
            }
            
            // Handle specific node types if needed
            switch (resultType.Name)
            {
                case "StatsNode":
                    Console.WriteLine("   📊 Statistics report detected - contains numerical data");
                    break;
                case "HvpNode":
                    Console.WriteLine("   🎯 HVP coverage report detected - contains verification data");
                    break;
                default:
                    Console.WriteLine($"   🔧 Generic {resultType.Name} detected - universal structure available");
                    break;
            }
        }
        
        /// <summary>
        /// Example of batch processing multiple files with universal parser
        /// </summary>
        public static async Task BatchProcessFiles(string[] fileUrls, string username = null, string password = null)
        {
            HtmlReader reader;
            
            // Create authenticated or non-authenticated reader
            if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
            {
                reader = CreateAuthenticatedReader(username, password);
                Console.WriteLine("🔐 Using authenticated reader");
            }
            else
            {
                reader = new HtmlReader();
                Console.WriteLine("🌐 Using non-authenticated reader");
            }
            
            var results = new System.Collections.Generic.List<object>();
            
            foreach (var file in fileUrls)
            {
                try
                {
                    Console.WriteLine($"⏳ Processing: {System.IO.Path.GetFileName(file)}...");
                    
                    var startTime = DateTime.Now;
                    var result = await reader.ParseFile(file);
                    var duration = DateTime.Now - startTime;
                    
                    if (result != null)
                    {
                        results.Add(result);
                        Console.WriteLine($"✅ Completed in {duration.TotalSeconds:F1}s - Type: {result.GetType().Name}");
                    }
                    else
                    {
                        Console.WriteLine($"⚠️  No data returned in {duration.TotalSeconds:F1}s");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Failed: {ex.Message}");
                }
            }
            
            Console.WriteLine($"\n📋 Batch Summary: {results.Count}/{fileUrls.Length} files processed successfully");
        }
    }
}