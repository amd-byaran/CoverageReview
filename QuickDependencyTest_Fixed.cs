using System;
using System.Reflection;
using System.Linq;

Console.WriteLine("=== DatabaseReader.dll Dependency Test ===");
Console.WriteLine("⚠️  WARNING: This test requires AMD.DatabaseReader NuGet package.");
Console.WriteLine();

#if ENABLE_DATABASE_TESTS // Use project-level conditional compilation
try
{
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
}
#else
Console.WriteLine("🔒 Database dependency tests are disabled.");
Console.WriteLine("📝 To enable:");
Console.WriteLine("   1. Install required NuGet packages: AMD.DatabaseReader, Npgsql, Microsoft.Extensions.Logging");
Console.WriteLine("   2. Add <DefineConstants>ENABLE_DATABASE_TESTS</DefineConstants> to your project file");
#endif

Console.WriteLine("\nPress any key to continue...");
Console.ReadKey();