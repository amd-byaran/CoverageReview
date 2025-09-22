using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;

namespace HvpHtmlParser
{
    /// <summary>
    /// Test program that reads AMD LogViewer URL and extracts FFBM information
    /// </summary>
    class AmdLogViewerFFBMTest
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("🧪 AMD LogViewer FFBM Information Extractor");
            Console.WriteLine("===========================================");
            Console.WriteLine();

            // AMD LogViewer URL
            string url = "https://logviewer-atl.amd.com/proj/videoip/web/merged_reports/dcn6_0/dcn6_0/func_cov/dcn_core_verif_plan/accumulate/8231593/hvp.dcn_core.html.gz";
            
            Console.WriteLine($"🎯 Target URL: {url}");
            Console.WriteLine();

            // Get credentials from user
            var (username, password) = GetCredentialsFromUser();
            
            // Create HttpClient with authentication
            using var httpClient = new HttpClient();
            var credentials = Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes($"{username}:{password}"));
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);
            
            // Create parser and set HttpClient
            var reader = new HtmlReader();
            reader.SetHttpClient(httpClient);
            
            Console.WriteLine("🔄 Connecting to AMD LogViewer...");
            
            try
            {
                // Parse the remote file
                var result = await reader.ParseFile(url);
                Console.WriteLine("✅ Successfully connected and parsed HVP report");
                Console.WriteLine();
                
                // Extract and display FFBM information
                if (result != null)
                {
                    await ExtractFFBMInformation(result);
                }
                else
                {
                    Console.WriteLine("⚠️  No data returned from parser");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error: {ex.Message}");
                
                if (ex.Message.Contains("401") || ex.Message.Contains("Unauthorized"))
                {
                    Console.WriteLine("🔐 Authentication failed - check your credentials");
                }
                else if (ex.Message.Contains("timeout") || ex.Message.Contains("DNS"))
                {
                    Console.WriteLine("🌐 Network issue - may need VPN for AMD internal resources");
                }
                else if (ex.Message.Contains("404") || ex.Message.Contains("Not Found"))
                {
                    Console.WriteLine("📄 File not found - check URL or build number");
                }
            }
            
            Console.WriteLine();
            Console.WriteLine("🏁 Test completed. Press any key to exit...");
            Console.ReadKey();
        }

        static (string username, string password) GetCredentialsFromUser()
        {
            Console.WriteLine("🔐 Please enter your AMD credentials:");
            
            // Get username (without domain)
            Console.Write("Username (without @amd.com): ");
            string username = Console.ReadLine()?.Trim() ?? "";
            
            // Ensure no domain suffix
            if (username.Contains("@"))
            {
                username = username.Split('@')[0];
            }
            
            // Get password securely
            Console.Write("Password: ");
            string password = ReadPasswordSecurely();
            
            Console.WriteLine();
            Console.WriteLine($"✅ Using credentials for user: {username}");
            
            return (username, password);
        }

        static string ReadPasswordSecurely()
        {
            string password = "";
            ConsoleKeyInfo keyInfo;
            
            do
            {
                keyInfo = Console.ReadKey(true);
                
                if (keyInfo.Key != ConsoleKey.Backspace && keyInfo.Key != ConsoleKey.Enter)
                {
                    password += keyInfo.KeyChar;
                    Console.Write("*");
                }
                else if (keyInfo.Key == ConsoleKey.Backspace && password.Length > 0)
                {
                    password = password.Substring(0, password.Length - 1);
                    Console.Write("\b \b");
                }
            }
            while (keyInfo.Key != ConsoleKey.Enter);
            
            Console.WriteLine();
            return password;
        }

        static async Task ExtractFFBMInformation(object result)
        {
            Console.WriteLine("🔍 Extracting FFBM Information");
            Console.WriteLine("==============================");
            
            switch (result)
            {
                case HvpNode rootNode:
                    await ExtractFFBMFromHierarchy(rootNode);
                    break;
                    
                case List<HvpFeature> features:
                    ExtractFFBMFromFeatures(features);
                    break;
                    
                default:
                    Console.WriteLine($"⚠️  Unexpected result type: {result?.GetType().Name}");
                    Console.WriteLine("Attempting to find FFBM in raw data...");
                    if (result != null)
                    {
                        ExtractFFBMFromGeneric(result);
                    }
                    break;
            }
        }

        static Task ExtractFFBMFromHierarchy(HvpNode rootNode)
        {
            Console.WriteLine($"📊 Root Node: {rootNode.Name}");
            Console.WriteLine($"📈 Overall Score: {rootNode.Score:F2}");
            Console.WriteLine();
            
            // Search for FFBM-related nodes
            var ffbmNodes = FindFFBMNodes(rootNode);
            
            if (ffbmNodes.Any())
            {
                Console.WriteLine($"🎯 Found {ffbmNodes.Count} FFBM-related items:");
                Console.WriteLine();
                
                foreach (var node in ffbmNodes)
                {
                    DisplayFFBMNode(node);
                }
            }
            else
            {
                Console.WriteLine("❓ No FFBM-specific nodes found in hierarchy");
                Console.WriteLine("📋 Showing all top-level modules:");
                Console.WriteLine();
                
                foreach (var child in rootNode.Children.Take(10))
                {
                    Console.WriteLine($"   📁 {child.Name} - {child.Score:F1}");
                }
                
                if (rootNode.Children.Count > 10)
                {
                    Console.WriteLine($"   ... and {rootNode.Children.Count - 10} more modules");
                }
            }
            
            return Task.CompletedTask;
        }

        static List<HvpNode> FindFFBMNodes(HvpNode rootNode)
        {
            var ffbmNodes = new List<HvpNode>();
            
            // Search recursively for FFBM-related nodes
            SearchFFBMRecursive(rootNode, ffbmNodes);
            
            return ffbmNodes;
        }

        static void SearchFFBMRecursive(HvpNode node, List<HvpNode> ffbmNodes)
        {
            // Check if this node contains FFBM in its name
            if (node.Name.ToUpper().Contains("FFBM"))
            {
                ffbmNodes.Add(node);
            }
            
            // Search in children
            foreach (var child in node.Children)
            {
                SearchFFBMRecursive(child, ffbmNodes);
            }
        }

        static void DisplayFFBMNode(HvpNode node)
        {
            Console.WriteLine($"🎯 FFBM Module: {node.Name}");
            Console.WriteLine($"   📊 Score: {node.Score:F2}");
            Console.WriteLine($"   🔗 Link: {node.Hyperlink ?? "N/A"}");
            Console.WriteLine($"   📁 Children: {node.Children.Count}");
            
            if (node.Children.Any())
            {
                Console.WriteLine($"   🔍 Sub-modules:");
                foreach (var child in node.Children.Take(5))
                {
                    Console.WriteLine($"      • {child.Name} - {child.Score:F1}");
                }
                if (node.Children.Count > 5)
                {
                    Console.WriteLine($"      ... and {node.Children.Count - 5} more");
                }
            }
            Console.WriteLine();
        }

        static void ExtractFFBMFromFeatures(List<HvpFeature> features)
        {
            Console.WriteLine($"🎯 Found {features.Count} features in report");
            
            var ffbmFeatures = features.Where(f => f.Name.ToUpper().Contains("FFBM")).ToList();
            
            if (ffbmFeatures.Any())
            {
                Console.WriteLine($"🎯 Found {ffbmFeatures.Count} FFBM-related features:");
                Console.WriteLine();
                
                foreach (var feature in ffbmFeatures)
                {
                    Console.WriteLine($"🎯 Feature: {feature.Name}");
                    Console.WriteLine($"   📊 Score: {feature.Score:F2}");
                    Console.WriteLine($"   🔍 Assert Score: {feature.AssertScore:F2}");
                    Console.WriteLine($"   📈 Group Score: {feature.GroupScore:F2}");
                    Console.WriteLine();
                }
            }
            else
            {
                Console.WriteLine("❓ No FFBM-specific features found");
                Console.WriteLine("📋 Available features:");
                
                foreach (var feature in features.Take(5))
                {
                    Console.WriteLine($"   🎯 {feature.Name} - Score: {feature.Score:F1}");
                }
                if (features.Count > 5)
                {
                    Console.WriteLine($"   ... and {features.Count - 5} more features");
                }
            }
        }

        static void ExtractFFBMFromGeneric(object result)
        {
            if (result == null)
            {
                Console.WriteLine("❌ No data returned from parser");
                return;
            }
            
            Console.WriteLine($"📋 Data type: {result.GetType().Name}");
            
            // Convert to string and search for FFBM
            string resultText = result.ToString() ?? "";
            
            if (resultText.ToUpper().Contains("FFBM"))
            {
                Console.WriteLine("✅ Found FFBM references in data");
                
                // Simple text analysis
                var lines = resultText.Split('\n');
                var ffbmLines = lines.Where(line => line.ToUpper().Contains("FFBM")).Take(10);
                
                Console.WriteLine("🔍 FFBM-related content:");
                foreach (var line in ffbmLines)
                {
                    Console.WriteLine($"   {line.Trim()}");
                }
            }
            else
            {
                Console.WriteLine("❓ No FFBM references found in parsed data");
                Console.WriteLine($"📏 Data length: {resultText.Length} characters");
            }
        }
    }
}