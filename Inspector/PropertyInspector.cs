using System;
using System.Reflection;
using static DcPgConn;

namespace DatabasePropertyInspector
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("=== AMD.DatabaseReader Type Inspection ===");
            
            // Get Release type
            var releaseType = typeof(Release);
            Console.WriteLine($"\nRelease type: {releaseType.FullName}");
            
            Console.WriteLine("\nPublic Properties:");
            foreach (var prop in releaseType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                Console.WriteLine($"  {prop.PropertyType.Name} {prop.Name}");
            }
            
            Console.WriteLine("\nPublic Fields:");
            foreach (var field in releaseType.GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                Console.WriteLine($"  {field.FieldType.Name} {field.Name}");
            }
            
            // Check for tuple return type from GetAllReportsForRelease
            var methods = typeof(DcPgConn).GetMethods(BindingFlags.Public | BindingFlags.Static);
            foreach (var method in methods)
            {
                if (method.Name == "GetAllReportsForRelease")
                {
                    Console.WriteLine($"\nGetAllReportsForRelease method signature:");
                    Console.WriteLine($"  Return type: {method.ReturnType}");
                    Console.WriteLine($"  Parameters:");
                    foreach (var param in method.GetParameters())
                    {
                        Console.WriteLine($"    {param.ParameterType.Name} {param.Name}");
                    }
                }
            }
        }
    }
}