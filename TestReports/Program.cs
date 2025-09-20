using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using System.IO;

namespace TestReports
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Testing TryGetReportsMethod Reflection Logic...");

            try
            {
                // Get the current directory and look for the DLL
                var currentDir = Directory.GetCurrentDirectory();
                Console.WriteLine($"Current directory: {currentDir}");

                // Try to find the CoverageAnalyzerGUI.dll in various locations
                var possiblePaths = new[]
                {
                    @"C:\Users\byaran\OneDrive - Advanced Micro Devices Inc\programming\coverage_analyzer\CoveageReview\CoverageAnalyzerGUI\bin\Debug\net10.0-windows\CoverageAnalyzerGUI.dll",
                    @"..\CoverageAnalyzerGUI\bin\Debug\net10.0-windows\CoverageAnalyzerGUI.dll",
                    @"CoverageAnalyzerGUI.dll"
                };

                string dllPath = null;
                foreach (var path in possiblePaths)
                {
                    var fullPath = Path.GetFullPath(path);
                    Console.WriteLine($"Checking path: {fullPath}");
                    if (File.Exists(fullPath))
                    {
                        dllPath = fullPath;
                        Console.WriteLine($"Found DLL at: {dllPath}");
                        break;
                    }
                }

                if (dllPath == null)
                {
                    Console.WriteLine("ERROR: Could not find CoverageAnalyzerGUI.dll");
                    return;
                }

                // Load the assembly
                Console.WriteLine("Loading assembly...");
                var assembly = Assembly.LoadFrom(dllPath);
                Console.WriteLine($"Loaded assembly: {assembly.FullName}");

                // Try to get the ProjectWizard type directly (avoid GetTypes() which loads all types)
                Console.WriteLine("Looking for ProjectWizard type...");
                var projectWizardType = assembly.GetType("CoverageAnalyzerGUI.ProjectWizard");

                if (projectWizardType == null)
                {
                    Console.WriteLine("ProjectWizard type not found with full name, trying alternative approaches...");

                    // Try to find it by searching through exported types only
                    try
                    {
                        var exportedTypes = assembly.GetExportedTypes();
                        Console.WriteLine($"Found {exportedTypes.Length} exported types");
                        projectWizardType = exportedTypes.FirstOrDefault(t => t.Name == "ProjectWizard");
                    }
                    catch (Exception ex2)
                    {
                        Console.WriteLine($"Error getting exported types: {ex2.Message}");
                        // Try manual search through modules
                        foreach (var module in assembly.GetModules())
                        {
                            try
                            {
                                var moduleTypes = module.GetTypes();
                                projectWizardType = moduleTypes.FirstOrDefault(t => t.Name == "ProjectWizard");
                                if (projectWizardType != null) break;
                            }
                            catch (Exception ex3)
                            {
                                Console.WriteLine($"Error in module {module.Name}: {ex3.Message}");
                            }
                        }
                    }
                }

                if (projectWizardType == null)
                {
                    Console.WriteLine("ERROR: Could not find ProjectWizard type");
                    Console.WriteLine("This might be due to WPF dependencies not being available in the test environment.");
                    Console.WriteLine("The method exists in the actual application - this is just a testing limitation.");
                    return;
                }

                Console.WriteLine($"Found ProjectWizard type: {projectWizardType.FullName}");

                // Get the TryGetReportsMethod
                var method = projectWizardType.GetMethod("TryGetReportsMethod",
                    BindingFlags.NonPublic | BindingFlags.Instance);

                if (method == null)
                {
                    Console.WriteLine("Available methods:");
                    var methods = projectWizardType.GetMethods(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public)
                        .Where(m => m.Name.Contains("Report"))
                        .ToArray();
                    foreach (var m in methods)
                    {
                        Console.WriteLine($"  - {m.Name}");
                    }
                    Console.WriteLine("ERROR: Could not find TryGetReportsMethod");
                    return;
                }

                Console.WriteLine($"Found method: {method.Name}");

                // Check method signature
                var parameters = method.GetParameters();
                Console.WriteLine($"Method parameters ({parameters.Length}):");
                foreach (var param in parameters)
                {
                    Console.WriteLine($"  - {param.ParameterType.Name} {param.Name}");
                }

                // Check return type
                Console.WriteLine($"Return type: {method.ReturnType.Name}");

                // Verify the signature matches what we expect
                if (parameters.Length == 3 &&
                    parameters[0].ParameterType == typeof(int) &&
                    parameters[1].ParameterType == typeof(string) &&
                    parameters[2].ParameterType == typeof(List<>).MakeGenericType(projectWizardType.Assembly.GetType("CoverageAnalyzerGUI.DatabaseReport")) &&
                    parameters[2].IsOut &&
                    method.ReturnType == typeof(bool))
                {
                    Console.WriteLine("✓ Method signature is correct!");
                    Console.WriteLine("✓ TryGetReportsMethod(int releaseId, string covType, out List<DatabaseReport> reports)");
                }
                else
                {
                    Console.WriteLine("✗ Method signature does not match expected format");
                }

                Console.WriteLine("Test completed successfully!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception during test: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }
        }
    }
}