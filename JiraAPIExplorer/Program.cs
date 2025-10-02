using System;
using System.Reflection;
using System.Linq;

// Load and examine the JiraAPI assembly
try 
{
    Console.WriteLine("=== Examining JiraAPI.dll ===");
    string jiraApiPath = @"c:\Users\byaran\OneDrive - Advanced Micro Devices Inc\programming\coverage_analyzer\CoveageReview\CoverageAnalyzerGUI\bin\Debug\net10.0-windows\JiraAPI.dll";
    Assembly jiraApiAssembly = Assembly.LoadFrom(jiraApiPath);
    
    var types = jiraApiAssembly.GetTypes().Where(t => t.IsPublic);
    
    foreach (var type in types)
    {
        Console.WriteLine($"\nType: {type.FullName}");
        
        var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
                          .Where(m => !m.IsSpecialName && m.DeclaringType == type);
        
        foreach (var method in methods)
        {
            var parameters = string.Join(", ", method.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"));
            Console.WriteLine($"  - {method.ReturnType.Name} {method.Name}({parameters})");
        }
    }
}
catch (Exception ex)
{
    Console.WriteLine($"Error examining JiraAPI: {ex.Message}");
}
