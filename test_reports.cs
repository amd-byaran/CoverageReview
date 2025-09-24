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
            try
            {
                // Get the current directory and look for the DLL
                var currentDir = Directory.GetCurrentDirectory();

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
                    if (File.Exists(fullPath))
                    {
                        dllPath = fullPath;
                        break;
                    }
                }

                if (dllPath == null)
                {
                    return;
                }

                // Load the assembly
                var assembly = Assembly.LoadFrom(dllPath);

                // Try to get the ProjectWizard type directly (avoid GetTypes() which loads all types)
                var projectWizardType = assembly.GetType("CoverageAnalyzerGUI.ProjectWizard");

                if (projectWizardType == null)
                {
                    // Try to find it by searching through exported types only
                    try
                    {
                        var exportedTypes = assembly.GetExportedTypes();
                        projectWizardType = exportedTypes.FirstOrDefault(t => t.Name == "ProjectWizard");
                    }
                    catch (Exception ex2)
                    {
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
                                // Error in module - continue
                            }
                        }
                    }
                }

                if (projectWizardType == null)
                {
                    return;
                }

                // Get the TryGetReportsMethod
                var method = projectWizardType.GetMethod("TryGetReportsMethod",
                    BindingFlags.NonPublic | BindingFlags.Instance);

                if (method == null)
                {
                    var methods = projectWizardType.GetMethods(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public)
                        .Where(m => m.Name.Contains("Report"))
                        .ToArray();
                    return;
                }

                // Check method signature
                var parameters = method.GetParameters();
                
                // Verify the signature matches what we expect
                if (parameters.Length == 3 &&
                    parameters[0].ParameterType == typeof(int) &&
                    parameters[1].ParameterType == typeof(string) &&
                    parameters[2].ParameterType == typeof(List<>).MakeGenericType(projectWizardType.Assembly.GetType("CoverageAnalyzerGUI.DatabaseReport")) &&
                    parameters[2].IsOut &&
                    method.ReturnType == typeof(bool))
                {
                    // Method signature is correct
                }
                else
                {
                    // Method signature does not match expected format
                }
            }
            catch (Exception ex)
            {
                // Exception handling removed
            }
        }
    }
}