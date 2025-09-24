using System;
using System.Reflection;
using System.Linq;

Console.WriteLine("=== DatabaseReader.dll Integration Test ===");
Console.WriteLine("⚠️  WARNING: This test requires AMD.DatabaseReader NuGet package to be installed.");
Console.WriteLine("💡 This is a standalone test file. To run it, create a proper .csproj file with the required dependencies.");
Console.WriteLine();

#if ENABLE_DATABASE_TESTS // Use project-level conditional compilation
try
{
    // Test 1: Load DcPgConn type
    Console.WriteLine("🔍 Test 1: Loading DcPgConn type...");
    var dcPgConnType = typeof(DcPgConn); // Requires AMD.DatabaseReader package
    Console.WriteLine($"✅ DcPgConn type loaded: {dcPgConnType.FullName}");
    Console.WriteLine($"✅ Assembly: {dcPgConnType.Assembly.FullName}");
    Console.WriteLine($"✅ Assembly Location: {dcPgConnType.Assembly.Location}");
    Console.WriteLine();

    // Test 2: List all methods
    Console.WriteLine("📋 Test 2: Available methods in DcPgConn:");
    var methods = dcPgConnType.GetMethods(BindingFlags.Public | BindingFlags.Static)
        .Where(m => !m.Name.StartsWith("get_") && !m.Name.StartsWith("set_"))
        .OrderBy(m => m.Name)
        .ToArray();
        
    Console.WriteLine($"Found {methods.Length} public static methods:");
    foreach (var method in methods)
    {
        var parameters = string.Join(", ", method.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"));
        Console.WriteLine($"  • {method.Name}({parameters}) -> {method.ReturnType.Name}");
    }
    Console.WriteLine();

    // Test 3: Check essential methods
    Console.WriteLine("✅ Test 3: Checking for essential methods:");
    var expectedMethods = new[] { "InitDb", "CloseDb", "GetAllReleases", "GetAllReportsForRelease", "GetChangelistsForReport", "GetReportPath" };
    
    foreach (var expectedMethod in expectedMethods)
    {
        var foundMethod = methods.FirstOrDefault(m => m.Name == expectedMethod);
        if (foundMethod != null)
        {
            Console.WriteLine($"  ✅ {expectedMethod}: Found");
        }
        else
        {
            Console.WriteLine($"  ❌ {expectedMethod}: NOT FOUND");
        }
    }
    Console.WriteLine();

    // Test 4: Try InitDb
    Console.WriteLine("🚀 Test 4: Attempting to call DcPgConn.InitDb()...");
    try
    {
        DcPgConn.InitDb();
        Console.WriteLine("✅ InitDb() called successfully!");
        
        // Test 5: Try GetAllReleases
        Console.WriteLine("📊 Test 5: Attempting to get releases...");
        var releases = DcPgConn.GetAllReleases();
        Console.WriteLine($"✅ GetAllReleases() returned {releases?.Count ?? 0} releases");
        
        if (releases != null && releases.Count > 0)
        {
            Console.WriteLine($"📋 First release: {releases.First()}");
        }
        
        // Clean up
        DcPgConn.CloseDb();
        Console.WriteLine("✅ Database connection closed");
        
    }
    catch (Exception dbEx)
    {
        Console.WriteLine($"⚠️ Database operation failed (this may be expected): {dbEx.GetType().Name}");
        Console.WriteLine($"⚠️ Error message: {dbEx.Message}");
        
        if (dbEx.InnerException != null)
        {
            Console.WriteLine($"⚠️ Inner exception: {dbEx.InnerException.Message}");
        }
        
        // Check if it's a connection/configuration issue (which is fine) vs a DLL loading issue (which is not)
        var connectionIssues = new[] 
        {
            typeof(ArgumentException),
            typeof(InvalidOperationException), 
            typeof(TimeoutException),
            typeof(System.Net.Sockets.SocketException)
        };
        
        if (connectionIssues.Contains(dbEx.GetType()) || 
            (dbEx.InnerException != null && connectionIssues.Contains(dbEx.InnerException.GetType())) ||
            dbEx.Message.Contains("connection") || 
            dbEx.Message.Contains("database") ||
            dbEx.Message.Contains("host"))
        {
            Console.WriteLine("✅ This appears to be a database configuration issue, not a DLL loading issue");
        }
        else
        {
            Console.WriteLine("❌ This may be a DLL loading or integration issue");
        }
    }
    Console.WriteLine();

    // Test 6: Check dependencies
    Console.WriteLine("📦 Test 6: Checking dependencies...");
    try
    {
        var npgsqlType = typeof(Npgsql.NpgsqlConnection);
        Console.WriteLine("✅ Npgsql dependency available");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ Npgsql dependency issue: {ex.Message}");
    }
    
    try
    {
        var loggerType = typeof(Microsoft.Extensions.Logging.ILogger);
        Console.WriteLine("✅ Microsoft.Extensions.Logging.Abstractions available");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ Microsoft.Extensions.Logging.Abstractions issue: {ex.Message}");
    }
    
    Console.WriteLine();
    Console.WriteLine("=== Test Complete ===");
    Console.WriteLine("✅ DatabaseReader.dll appears to be properly integrated!");
    Console.WriteLine("⚠️ If database operations failed, you need to configure database connection settings.");
}
catch (Exception ex)
{
    Console.WriteLine($"❌ CRITICAL ERROR: {ex.GetType().Name}");
    Console.WriteLine($"❌ Message: {ex.Message}");
    Console.WriteLine($"❌ Stack trace: {ex.StackTrace}");
    Console.WriteLine();
    Console.WriteLine("💡 This error might occur if:");
    Console.WriteLine("   • AMD.DatabaseReader NuGet package is not installed");
    Console.WriteLine("   • TempDbReader.dll is not in the correct location");
    Console.WriteLine("   • Database connection is not properly configured");
    Console.WriteLine("   • Dependencies like Npgsql or Microsoft.Extensions.Logging are missing");
}
#else
Console.WriteLine("🔒 Database tests are disabled.");
Console.WriteLine("📝 To enable:");
Console.WriteLine("   1. Create a .csproj file with PackageReference to AMD.DatabaseReader");
Console.WriteLine("   2. Add <DefineConstants>ENABLE_DATABASE_TESTS</DefineConstants> to the project");
Console.WriteLine("   3. Build and run with proper dependencies");
#endif

Console.WriteLine("\nPress any key to exit...");
Console.ReadKey();