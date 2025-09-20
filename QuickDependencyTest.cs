using System;
using System.Reflection;

try
{
    Console.WriteLine("=== DatabaseReader.dll Dependency Test ===");
    
    // Test 1: Load DcPgConn
    var dcPgConnType = typeof(DcPgConn);
    Console.WriteLine($"✅ DcPgConn loaded: {dcPgConnType.FullName}");
    
    // Test 2: Check methods
    var methods = dcPgConnType.GetMethods(BindingFlags.Public | BindingFlags.Static)
        .Where(m => !m.Name.StartsWith("get_") && !m.Name.StartsWith("set_"))
        .Select(m => m.Name)
        .Distinct()
        .OrderBy(n => n)
        .ToArray();
    
    Console.WriteLine($"✅ Found {methods.Length} methods: {string.Join(", ", methods)}");
    
    // Test 3: Check dependencies
    Console.WriteLine($"✅ Npgsql available: {typeof(Npgsql.NpgsqlConnection).FullName}");
    Console.WriteLine($"✅ Logging available: {typeof(Microsoft.Extensions.Logging.ILogger).FullName}");
    
    Console.WriteLine("\n🎯 All dependencies resolved successfully!");
    
}
catch (Exception ex)
{
    Console.WriteLine($"❌ Error: {ex.Message}");
    if (ex.InnerException != null)
        Console.WriteLine($"❌ Inner: {ex.InnerException.Message}");
}