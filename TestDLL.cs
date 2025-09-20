using System;
using System.Reflection;
using System.Linq;

class Program
{
    static void Main()
    {
        try
        {
            Console.WriteLine("=== DatabaseReader DLL Function Test ===");
            
            // Load the TempDbReader.dll (which is actually DatabaseReader.dll)
            var dllPath = @".\CoverageAnalyzerGUI\lib\TempDbReader.dll";
            var assembly = Assembly.LoadFrom(dllPath);
            
            Console.WriteLine($"✅ Successfully loaded: {assembly.FullName}");
            Console.WriteLine($"📍 From: {dllPath}");
            Console.WriteLine();
            
            // Find the DcPgConn type
            var dcPgConnType = assembly.GetTypes().FirstOrDefault(t => t.Name.Contains("DcPgConn"));
            
            if (dcPgConnType == null)
            {
                Console.WriteLine("❌ DcPgConn type not found. Available types:");
                foreach (var type in assembly.GetTypes().Where(t => t.IsPublic))
                {
                    Console.WriteLine($"  • {type.FullName}");
                }
                return;
            }
            
            Console.WriteLine($"✅ Found type: {dcPgConnType.FullName}");
            Console.WriteLine();
            
            // Get all public static methods
            var methods = dcPgConnType.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(m => !m.Name.StartsWith("get_") && !m.Name.StartsWith("set_"))
                .OrderBy(m => m.Name)
                .ToArray();
            
            Console.WriteLine($"📋 Public Static Methods ({methods.Length} total):");
            Console.WriteLine("=" + new string('=', 80));
            
            foreach (var method in methods)
            {
                var parameters = string.Join(", ", method.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"));
                var returnType = method.ReturnType.Name;
                Console.WriteLine($"• {method.Name}({parameters}) -> {returnType}");
            }
            
            Console.WriteLine();
            Console.WriteLine("🔍 Report-related methods:");
            Console.WriteLine("=" + new string('=', 50));
            
            var reportMethods = methods.Where(m => 
                m.Name.ToLower().Contains("report") || 
                m.Name.ToLower().Contains("coverage") ||
                m.Name.ToLower().Contains("release")).ToArray();
            
            if (reportMethods.Length == 0)
            {
                Console.WriteLine("❌ No report-related methods found!");
            }
            else
            {
                foreach (var method in reportMethods)
                {
                    var parameters = string.Join(", ", method.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"));
                    var returnType = method.ReturnType.Name;
                    Console.WriteLine($"✅ {method.Name}({parameters}) -> {returnType}");
                }
            }
            
            // Test specific methods we need
            Console.WriteLine();
            Console.WriteLine("🎯 Testing specific required methods:");
            Console.WriteLine("=" + new string('=', 50));
            
            var requiredMethods = new[]
            {
                "GetAllReleases",
                "GetAllReportsForRelease",
                "GetReportsForRelease",
                "GetAllReports",
                "GetReports"
            };
            
            foreach (var methodName in requiredMethods)
            {
                var foundMethods = dcPgConnType.GetMethods(BindingFlags.Public | BindingFlags.Static)
                    .Where(m => m.Name.Equals(methodName, StringComparison.OrdinalIgnoreCase))
                    .ToArray();
                
                if (foundMethods.Length > 0)
                {
                    Console.WriteLine($"✅ {methodName}: FOUND");
                    foreach (var method in foundMethods)
                    {
                        var parameters = string.Join(", ", method.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"));
                        Console.WriteLine($"   • {method.Name}({parameters}) -> {method.ReturnType.Name}");
                    }
                }
                else
                {
                    Console.WriteLine($"❌ {methodName}: NOT FOUND");
                }
            }
            
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error: {ex.Message}");
            Console.WriteLine($"❌ Stack Trace: {ex.StackTrace}");
        }
        
        Console.WriteLine();
        Console.WriteLine("Press any key to exit...");
        Console.ReadKey();
    }
}