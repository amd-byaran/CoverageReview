using System;
using System.Reflection;
using System.Linq;

class Program
{
    static void Main()
    {
        try
        {
            // Load the TempDbReader.dll (which is actually DatabaseReader.dll)
            var dllPath = @".\CoverageAnalyzerGUI\lib\TempDbReader.dll";
            var assembly = Assembly.LoadFrom(dllPath);
            
            // Find the DcPgConn type
            var dcPgConnType = assembly.GetTypes().FirstOrDefault(t => t.Name.Contains("DcPgConn"));
            
            if (dcPgConnType == null)
            {
                return;
            }
            
            // Get all public static methods
            var methods = dcPgConnType.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(m => !m.Name.StartsWith("get_") && !m.Name.StartsWith("set_"))
                .OrderBy(m => m.Name)
                .ToArray();
            
            var reportMethods = methods.Where(m => 
                m.Name.ToLower().Contains("report") || 
                m.Name.ToLower().Contains("coverage") ||
                m.Name.ToLower().Contains("release")).ToArray();
            
            // Test specific methods we need
            
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
                    // Method found
                }
                else
                {
                    // Method not found
                }
            }
            
        }
        catch (Exception ex)
        {
            // Error handling removed
        }
    }
}