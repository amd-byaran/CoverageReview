using System;
using System.IO;
using System.Reflection;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace HvpParserTest
{
    class Program
    {
        private static readonly string TestUrl = "https://logviewer-atl.amd.com/proj/videoip/web/merged_reports/dcn6_0/dcn6_0/func_cov/dcn_core_verif_plan/accumulate/8231593/hvp.dcn_core.html";
        
        static async Task Main(string[] args)
        {
            Console.WriteLine("=== HvpParser Auto-Detection Test ===");
            Console.WriteLine($"Testing URL: {TestUrl}");
            Console.WriteLine();

            try
            {
                // Load the HvpHtmlParser assembly
                var hvpAssembly = LoadHvpAssembly();
                if (hvpAssembly == null)
                {
                    Console.WriteLine("ERROR: Could not load HvpHtmlParser assembly");
                    return;
                }

                Console.WriteLine($"Successfully loaded HvpHtmlParser assembly: {hvpAssembly.GetName().Name}");
                Console.WriteLine();

                // Discover available types and methods
                await DiscoverAndTestHvpParser(hvpAssembly);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }

            Console.WriteLine("\nPress any key to exit...");
            Console.ReadKey();
        }

        private static Assembly LoadHvpAssembly()
        {
            // First try to find it in loaded assemblies
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            var hvpAssembly = assemblies.FirstOrDefault(a => a.FullName?.Contains("HvpHtmlParser") == true);
            
            if (hvpAssembly != null)
            {
                Console.WriteLine("Found HvpHtmlParser in loaded assemblies");
                return hvpAssembly;
            }

            // Try to load from various locations
            var possiblePaths = new[]
            {
                @"C:\Users\byaran\OneDrive - Advanced Micro Devices Inc\programming\coverage_analyzer\CoveageReview\CoverageAnalyzerGUI\bin\Debug\net10.0-windows\HvpHtmlParser.Lib.dll",
                @"CoverageAnalyzerGUI\bin\Debug\net10.0-windows\HvpHtmlParser.Lib.dll",
                @"HvpHtmlParser.Lib.dll"
            };

            foreach (var path in possiblePaths)
            {
                try
                {
                    var fullPath = Path.GetFullPath(path);
                    Console.WriteLine($"Checking for HvpHtmlParser at: {fullPath}");
                    
                    if (File.Exists(fullPath))
                    {
                        Console.WriteLine($"Found HvpHtmlParser DLL, loading...");
                        return Assembly.LoadFrom(fullPath);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to load from {path}: {ex.Message}");
                }
            }

            return null;
        }

        private static async Task DiscoverAndTestHvpParser(Assembly hvpAssembly)
        {
            Console.WriteLine("=== Discovering HvpParser Types and Methods ===");
            
            var types = hvpAssembly.GetExportedTypes();
            Console.WriteLine($"Found {types.Length} exported types:");
            
            Type hvpParserType = null;
            Type hvpNodeType = null;
            
            foreach (var type in types)
            {
                Console.WriteLine($"  - {type.FullName}");
                
                if (type.Name.Contains("HvpParser") || type.Name.Equals("HvpParser", StringComparison.OrdinalIgnoreCase))
                {
                    hvpParserType = type;
                    Console.WriteLine($"    ✓ Identified as HvpParser type");
                }
                
                if (type.Name.Contains("HvpNode") || type.Name.Equals("HvpNode", StringComparison.OrdinalIgnoreCase))
                {
                    hvpNodeType = type;
                    Console.WriteLine($"    ✓ Identified as HvpNode type");
                }
            }
            
            Console.WriteLine();
            
            if (hvpParserType == null)
            {
                Console.WriteLine("ERROR: Could not find HvpParser type");
                return;
            }
            
            Console.WriteLine($"=== Analyzing HvpParser Methods ===");
            Console.WriteLine($"HvpParser type: {hvpParserType.FullName}");
            
            var methods = hvpParserType.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance);
            
            foreach (var method in methods)
            {
                if (method.DeclaringType == typeof(object)) continue; // Skip Object methods
                
                var parameters = string.Join(", ", method.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"));
                var returnTypeName = GetFriendlyTypeName(method.ReturnType);
                
                Console.WriteLine($"Method: {method.Name}({parameters}) -> {returnTypeName}");
                
                // Look for methods that might auto-detect and parse
                if (IsAutoDetectionMethod(method))
                {
                    Console.WriteLine($"  ✓ Potential auto-detection method");
                    await TestMethod(method, hvpParserType, hvpNodeType);
                }
            }
        }
        
        private static bool IsAutoDetectionMethod(MethodInfo method)
        {
            // Look for methods that:
            // 1. Take a string parameter (URL or file path)
            // 2. Have names suggesting automatic parsing/detection
            // 3. Return HvpNode or similar hierarchical structures
            
            if (method.GetParameters().Length == 0) return false;
            
            var firstParam = method.GetParameters()[0];
            if (firstParam.ParameterType != typeof(string)) return false;
            
            var methodName = method.Name.ToLowerInvariant();
            var returnTypeName = method.ReturnType.Name.ToLowerInvariant();
            var fullReturnTypeName = (method.ReturnType.FullName ?? "").ToLowerInvariant();
            
            // Check for auto-detection method names
            var autoDetectionNames = new[] { "parse", "parsehtml", "parsefile", "autoparse", "detect", "load", "analyze" };
            var hasAutoDetectionName = autoDetectionNames.Any(name => methodName.Contains(name));
            
            // Check if it returns HvpNode or hierarchical structure
            var returnsHvpNode = returnTypeName.Contains("hvpnode") || 
                               returnTypeName.Contains("node") && !returnTypeName.Contains("testreport") ||
                               fullReturnTypeName.Contains("hvpnode") ||
                               fullReturnTypeName.Contains("node") && !fullReturnTypeName.Contains("testreport");
            
            return hasAutoDetectionName && returnsHvpNode;
        }
        
        private static async Task TestMethod(MethodInfo method, Type hvpParserType, Type hvpNodeType)
        {
            try
            {
                Console.WriteLine($"\n=== Testing Method: {method.Name} ===");
                
                // Create parser instance if needed
                object parserInstance = null;
                if (!method.IsStatic)
                {
                    var constructor = hvpParserType.GetConstructor(Type.EmptyTypes);
                    if (constructor != null)
                    {
                        parserInstance = Activator.CreateInstance(hvpParserType);
                        Console.WriteLine("Created parser instance");
                    }
                    else
                    {
                        Console.WriteLine("Could not create parser instance - no parameterless constructor");
                        return;
                    }
                }
                
                // Prepare parameters
                var parameters = method.GetParameters();
                var args = new object[parameters.Length];
                args[0] = TestUrl; // First parameter is the URL
                
                // Fill in additional parameters if needed
                for (int i = 1; i < parameters.Length; i++)
                {
                    var paramType = parameters[i].ParameterType;
                    if (paramType == typeof(string))
                    {
                        args[i] = ""; // Empty string for additional string parameters
                    }
                    else if (paramType == typeof(HttpClient))
                    {
                        args[i] = new HttpClient(); // Provide HttpClient if needed
                    }
                    else if (paramType.IsClass && paramType != typeof(string))
                    {
                        args[i] = null; // Null for reference types
                    }
                    else
                    {
                        args[i] = Activator.CreateInstance(paramType); // Default value for value types
                    }
                }
                
                Console.WriteLine($"Calling {method.Name} with URL: {TestUrl}");
                
                // Call the method
                object result;
                if (method.ReturnType.IsGenericType && method.ReturnType.GetGenericTypeDefinition() == typeof(Task<>))
                {
                    // Async method
                    var task = (Task)method.Invoke(parserInstance, args);
                    await task;
                    
                    // Get the result from Task<T>
                    var resultProperty = task.GetType().GetProperty("Result");
                    result = resultProperty?.GetValue(task);
                }
                else if (method.ReturnType == typeof(Task))
                {
                    // Async void method
                    var task = (Task)method.Invoke(parserInstance, args);
                    await task;
                    result = null;
                }
                else
                {
                    // Sync method
                    result = method.Invoke(parserInstance, args);
                }
                
                // Analyze the result
                if (result != null)
                {
                    Console.WriteLine($"✓ Method returned result of type: {result.GetType().FullName}");
                    
                    if (result.GetType().Name.Contains("HvpNode"))
                    {
                        Console.WriteLine("✓ SUCCESS: Method returned HvpNode structure!");
                        AnalyzeHvpNode(result);
                    }
                    else
                    {
                        Console.WriteLine($"Result type: {result.GetType().FullName}");
                        Console.WriteLine("This might not be the HvpNode we're looking for...");
                        
                        // Try to analyze properties anyway
                        var properties = result.GetType().GetProperties();
                        Console.WriteLine($"Available properties ({properties.Length}):");
                        foreach (var prop in properties.Take(10)) // Show first 10 properties
                        {
                            Console.WriteLine($"  - {prop.Name}: {prop.PropertyType.Name}");
                        }
                    }
                }
                else
                {
                    Console.WriteLine("Method returned null");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR testing method {method.Name}: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner exception: {ex.InnerException.Message}");
                }
            }
        }
        
        private static void AnalyzeHvpNode(object hvpNode)
        {
            try
            {
                var type = hvpNode.GetType();
                Console.WriteLine($"\n=== Analyzing HvpNode Structure ===");
                Console.WriteLine($"Type: {type.FullName}");
                
                var properties = type.GetProperties();
                Console.WriteLine($"Properties ({properties.Length}):");
                
                foreach (var prop in properties)
                {
                    try
                    {
                        var value = prop.GetValue(hvpNode);
                        var valueStr = value?.ToString() ?? "null";
                        if (valueStr.Length > 50) valueStr = valueStr.Substring(0, 50) + "...";
                        
                        Console.WriteLine($"  - {prop.Name}: {prop.PropertyType.Name} = {valueStr}");
                        
                        // Check for hierarchical properties
                        if (prop.Name.ToLowerInvariant().Contains("children") || 
                            prop.Name.ToLowerInvariant().Contains("child") ||
                            prop.Name.ToLowerInvariant().Contains("nodes"))
                        {
                            Console.WriteLine($"    ✓ Potential hierarchy property: {prop.Name}");
                            
                            if (value is System.Collections.IEnumerable enumerable && !(value is string))
                            {
                                var count = 0;
                                foreach (var item in enumerable)
                                {
                                    count++;
                                    if (count > 5) break; // Don't enumerate too many
                                }
                                Console.WriteLine($"    Contains {count}+ items");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"  - {prop.Name}: {prop.PropertyType.Name} = ERROR: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR analyzing HvpNode: {ex.Message}");
            }
        }
        
        private static string GetFriendlyTypeName(Type type)
        {
            if (type.IsGenericType)
            {
                var genericArgs = string.Join(", ", type.GetGenericArguments().Select(t => GetFriendlyTypeName(t)));
                return $"{type.GetGenericTypeDefinition().Name.Split('`')[0]}<{genericArgs}>";
            }
            return type.Name;
        }
    }
}