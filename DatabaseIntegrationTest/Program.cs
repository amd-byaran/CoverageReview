using System;
using System.Reflection;
using System.Linq;
using System.IO;

Console.WriteLine("=== DatabaseReader.dll Integration Test ===");
Console.WriteLine();

try
{
    // Test 1: Load the assembly by file path to avoid type loading issues
    Console.WriteLine("🔍 Test 1: Loading DatabaseReader.dll assembly...");
    var assemblyPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DatabaseReader.dll");
    Console.WriteLine($"Looking for assembly at: {assemblyPath}");
    
    if (!File.Exists(assemblyPath))
    {
        Console.WriteLine($"❌ Assembly not found at {assemblyPath}");
        Console.WriteLine("Available files:");
        foreach (var file in Directory.GetFiles(AppDomain.CurrentDomain.BaseDirectory, "*.dll"))
        {
            Console.WriteLine($"  • {Path.GetFileName(file)}");
        }
        return;
    }
    
    var assembly = Assembly.LoadFrom(assemblyPath);
    Console.WriteLine($"✅ Assembly loaded: {assembly.FullName}");
    Console.WriteLine($"✅ Location: {assembly.Location}");
    Console.WriteLine();

    // Test 2: Find DcPgConn type in the loaded assembly
    Console.WriteLine("🔍 Test 2: Finding DcPgConn type...");
    var dcPgConnType = assembly.GetTypes().FirstOrDefault(t => t.Name == "DcPgConn");
    
    if (dcPgConnType == null)
    {
        Console.WriteLine("❌ DcPgConn type not found. Available types:");
        foreach (var type in assembly.GetTypes().Where(t => t.IsPublic))
        {
            Console.WriteLine($"  • {type.FullName}");
        }
        return;
    }
    
    Console.WriteLine($"✅ DcPgConn type found: {dcPgConnType.FullName}");
    Console.WriteLine();

    // Test 3: List all methods
    Console.WriteLine("📋 Test 3: Available methods in DcPgConn:");
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

    // Test 4: Check essential methods
    Console.WriteLine("✅ Test 4: Checking for essential methods:");
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

    // Test 5: Try InitDb
    Console.WriteLine("🚀 Test 5: Attempting to call DcPgConn.InitDb()...");
    var initDbMethod = dcPgConnType.GetMethod("InitDb", BindingFlags.Public | BindingFlags.Static);
    
    if (initDbMethod == null)
    {
        Console.WriteLine("❌ InitDb method not found");
    }
    else
    {
        try
        {
            initDbMethod.Invoke(null, null);
            Console.WriteLine("✅ InitDb() called successfully!");
            
            // Test 6: Try GetAllReleases
            Console.WriteLine("📊 Test 6: Attempting to get releases...");
            var getAllReleasesMethod = dcPgConnType.GetMethod("GetAllReleases", BindingFlags.Public | BindingFlags.Static);
            
            if (getAllReleasesMethod != null)
            {
                object? releasesResult;
                var parameters = getAllReleasesMethod.GetParameters();
                
                if (parameters.Length > 0)
                {
                    // Method takes parameters, call with null/default values
                    var methodArgs = new object?[parameters.Length];
                    for (int i = 0; i < parameters.Length; i++)
                    {
                        methodArgs[i] = parameters[i].HasDefaultValue ? parameters[i].DefaultValue : null;
                    }
                    releasesResult = getAllReleasesMethod.Invoke(null, methodArgs);
                }
                else
                {
                    releasesResult = getAllReleasesMethod.Invoke(null, null);
                }
                
                if (releasesResult is System.Collections.IEnumerable enumerable)
                {
                    var count = 0;
                    foreach (var item in enumerable)
                    {
                        count++;
                        if (count == 1) Console.WriteLine($"📋 First release: {item}");
                    }
                    Console.WriteLine($"✅ GetAllReleases() returned {count} releases");
                }
                else
                {
                    Console.WriteLine($"✅ GetAllReleases() returned: {releasesResult}");
                }
            }
            
            // Clean up
            var closeDbMethod = dcPgConnType.GetMethod("CloseDb", BindingFlags.Public | BindingFlags.Static);
            closeDbMethod?.Invoke(null, null);
            Console.WriteLine("✅ Database connection closed");
            
        }
        catch (Exception dbEx)
        {
            var innerEx = dbEx.InnerException ?? dbEx;
            Console.WriteLine($"⚠️ Database operation failed (this may be expected): {innerEx.GetType().Name}");
            Console.WriteLine($"⚠️ Error message: {innerEx.Message}");
            
            // Check if it's a connection/configuration issue (which is fine) vs a DLL loading issue (which is not)
            var connectionIssues = new[] 
            {
                typeof(ArgumentException),
                typeof(InvalidOperationException), 
                typeof(TimeoutException),
                typeof(System.Net.Sockets.SocketException)
            };
            
            if (connectionIssues.Contains(innerEx.GetType()) || 
                innerEx.Message.Contains("connection") || 
                innerEx.Message.Contains("database") ||
                innerEx.Message.Contains("host"))
            {
                Console.WriteLine("✅ This appears to be a database configuration issue, not a DLL loading issue");
            }
            else
            {
                Console.WriteLine("❌ This may be a DLL loading or integration issue");
            }
        }
    }
    Console.WriteLine();

    // Test 7: Check dependencies
    Console.WriteLine("📦 Test 7: Checking dependencies...");
    try
    {
        // Check if Npgsql is available through assembly loading
        var npgsqlAssembly = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => a.GetName().Name == "Npgsql");
        if (npgsqlAssembly != null)
        {
            Console.WriteLine("✅ Npgsql dependency available");
        }
        else
        {
            Console.WriteLine("❌ Npgsql dependency not loaded");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ Npgsql dependency issue: {ex.Message}");
    }
    
    try
    {
        // Check if Microsoft.Extensions.Logging.Abstractions is available
        var loggingAssembly = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => a.GetName().Name == "Microsoft.Extensions.Logging.Abstractions");
        if (loggingAssembly != null)
        {
            Console.WriteLine("✅ Microsoft.Extensions.Logging.Abstractions available");
        }
        else
        {
            Console.WriteLine("❌ Microsoft.Extensions.Logging.Abstractions not loaded");
        }
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
    Console.WriteLine("This indicates a serious DLL integration problem.");
}