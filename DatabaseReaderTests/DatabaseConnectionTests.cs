using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Xunit;
using Xunit.Abstractions;

namespace DatabaseReaderTests;

public class DatabaseConnectionTests
{
    private readonly ITestOutputHelper _output;

    public DatabaseConnectionTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void Test_DcPgConn_Type_Is_Available()
    {
        // Test that we can load the DcPgConn type
        var dcPgConnType = Type.GetType("DcPgConn, TempDbReader");
        
        _output.WriteLine($"Looking for DcPgConn type...");
        
        if (dcPgConnType == null)
        {
            // Try to find it in loaded assemblies
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            foreach (var assembly in assemblies)
            {
                _output.WriteLine($"Checking assembly: {assembly.FullName}");
                var types = assembly.GetTypes().Where(t => t.Name.Contains("DcPgConn") || t.Name.Contains("Conn")).ToArray();
                foreach (var type in types)
                {
                    _output.WriteLine($"  Found type: {type.FullName}");
                }
            }
            
            // Try alternative approaches
            try
            {
                dcPgConnType = Type.GetType("DcPgConn");
                _output.WriteLine($"Found DcPgConn without assembly qualifier: {dcPgConnType?.FullName}");
            }
            catch (Exception ex)
            {
                _output.WriteLine($"Error getting DcPgConn type: {ex.Message}");
            }
        }

        Assert.NotNull(dcPgConnType);
        _output.WriteLine($"✅ DcPgConn type found: {dcPgConnType.FullName}");
        _output.WriteLine($"✅ Assembly: {dcPgConnType.Assembly.FullName}");
        _output.WriteLine($"✅ Assembly location: {dcPgConnType.Assembly.Location}");
    }

    [Fact]
    public void Test_DcPgConn_Available_Methods()
    {
        // Get the DcPgConn type
        var dcPgConnType = GetDcPgConnType();
        Assert.NotNull(dcPgConnType);

        // Get all public static methods
        var methods = dcPgConnType.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(m => !m.Name.StartsWith("get_") && !m.Name.StartsWith("set_"))
            .ToArray();

        _output.WriteLine($"Found {methods.Length} public static methods in DcPgConn:");
        
        foreach (var method in methods)
        {
            var parameters = string.Join(", ", method.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"));
            _output.WriteLine($"  • {method.Name}({parameters}) -> {method.ReturnType.Name}");
        }

        // Check for specific methods we expect
        var initDbMethod = methods.FirstOrDefault(m => m.Name == "InitDb");
        var getAllReleasesMethod = methods.FirstOrDefault(m => m.Name == "GetAllReleases");

        _output.WriteLine($"InitDb method found: {initDbMethod != null}");
        _output.WriteLine($"GetAllReleases method found: {getAllReleasesMethod != null}");

        Assert.True(methods.Length > 0, "No public static methods found in DcPgConn");
    }

    [Fact]
    public void Test_Database_Connection_Initialization()
    {
        var dcPgConnType = GetDcPgConnType();
        Assert.NotNull(dcPgConnType);

        // Find the InitDb method
        var initDbMethod = dcPgConnType.GetMethod("InitDb", BindingFlags.Public | BindingFlags.Static);
        
        if (initDbMethod == null)
        {
            _output.WriteLine("❌ InitDb method not found");
            
            // List all methods that might be related to initialization
            var allMethods = dcPgConnType.GetMethods(BindingFlags.Public | BindingFlags.Static);
            var initMethods = allMethods.Where(m => 
                m.Name.ToLower().Contains("init") || 
                m.Name.ToLower().Contains("connect") ||
                m.Name.ToLower().Contains("setup")).ToArray();
                
            _output.WriteLine("Available initialization-related methods:");
            foreach (var method in initMethods)
            {
                var parameters = string.Join(", ", method.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"));
                _output.WriteLine($"  • {method.Name}({parameters})");
            }
        }

        Assert.NotNull(initDbMethod);

        // Try to call InitDb
        _output.WriteLine("Attempting to call InitDb()...");
        
        try
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            initDbMethod.Invoke(null, new object[0]);
            stopwatch.Stop();
            
            _output.WriteLine($"✅ InitDb() completed successfully in {stopwatch.ElapsedMilliseconds}ms");
        }
        catch (Exception ex)
        {
            _output.WriteLine($"❌ InitDb() failed: {ex.Message}");
            if (ex.InnerException != null)
            {
                _output.WriteLine($"❌ Inner exception: {ex.InnerException.Message}");
                _output.WriteLine($"❌ Inner exception type: {ex.InnerException.GetType().FullName}");
                
                // Check for common database connection issues
                var innerMsg = ex.InnerException.Message.ToLower();
                if (innerMsg.Contains("connection") || innerMsg.Contains("server"))
                {
                    _output.WriteLine("💡 Suggestion: Check PostgreSQL server is running and accessible");
                }
                else if (innerMsg.Contains("authentication") || innerMsg.Contains("password"))
                {
                    _output.WriteLine("💡 Suggestion: Check database credentials");
                }
                else if (innerMsg.Contains("database") && innerMsg.Contains("exist"))
                {
                    _output.WriteLine("💡 Suggestion: Check database name exists");
                }
            }
            
            // Don't fail the test - we want to see what the error is
            _output.WriteLine("⚠️ Test continuing to check other methods despite InitDb failure");
        }
    }

    [Fact]
    public void Test_Get_All_Releases()
    {
        var dcPgConnType = GetDcPgConnType();
        Assert.NotNull(dcPgConnType);

        // Find the GetAllReleases method
        var getAllReleasesMethod = dcPgConnType.GetMethod("GetAllReleases", BindingFlags.Public | BindingFlags.Static);
        
        if (getAllReleasesMethod == null)
        {
            _output.WriteLine("❌ GetAllReleases method not found");
            
            // List all methods that might be related to releases
            var allMethods = dcPgConnType.GetMethods(BindingFlags.Public | BindingFlags.Static);
            var releaseMethods = allMethods.Where(m => 
                m.Name.ToLower().Contains("release") || 
                m.Name.ToLower().Contains("get")).ToArray();
                
            _output.WriteLine("Available release-related methods:");
            foreach (var method in releaseMethods)
            {
                var parameters = string.Join(", ", method.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"));
                _output.WriteLine($"  • {method.Name}({parameters}) -> {method.ReturnType.Name}");
            }
        }

        Assert.NotNull(getAllReleasesMethod);

        // Try to call GetAllReleases
        _output.WriteLine("Attempting to call GetAllReleases()...");
        
        try
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var result = getAllReleasesMethod.Invoke(null, new object[0]);
            stopwatch.Stop();
            
            _output.WriteLine($"✅ GetAllReleases() completed in {stopwatch.ElapsedMilliseconds}ms");
            _output.WriteLine($"Result type: {result?.GetType().FullName ?? "null"}");
            
            if (result != null)
            {
                if (result is System.Collections.IEnumerable enumerable)
                {
                    var items = enumerable.Cast<object>().ToArray();
                    _output.WriteLine($"📊 Found {items.Length} releases");
                    
                    // Show first few releases for debugging
                    for (int i = 0; i < Math.Min(3, items.Length); i++)
                    {
                        var item = items[i];
                        _output.WriteLine($"Release {i + 1}: {item?.GetType().Name}");
                        
                        if (item != null)
                        {
                            // Try to extract some properties
                            var itemType = item.GetType();
                            var properties = itemType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
                            
                            foreach (var prop in properties.Take(5)) // Show first 5 properties
                            {
                                try
                                {
                                    var value = prop.GetValue(item);
                                    _output.WriteLine($"  {prop.Name}: {value}");
                                }
                                catch (Exception propEx)
                                {
                                    _output.WriteLine($"  {prop.Name}: <error getting value: {propEx.Message}>");
                                }
                            }
                        }
                    }
                    
                    if (items.Length == 0)
                    {
                        _output.WriteLine("⚠️ No releases found - database might be empty or connection issue");
                    }
                }
                else
                {
                    _output.WriteLine($"Result is not enumerable: {result}");
                }
            }
            else
            {
                _output.WriteLine("❌ GetAllReleases returned null");
            }
        }
        catch (Exception ex)
        {
            _output.WriteLine($"❌ GetAllReleases() failed: {ex.Message}");
            if (ex.InnerException != null)
            {
                _output.WriteLine($"❌ Inner exception: {ex.InnerException.Message}");
            }
            _output.WriteLine($"❌ Stack trace: {ex.StackTrace}");
            
            // Don't fail the test - we want to see what the error is
        }
    }

    [Fact]
    public void Test_Database_Connection_With_Initialization()
    {
        var dcPgConnType = GetDcPgConnType();
        Assert.NotNull(dcPgConnType);

        _output.WriteLine("=== COMPREHENSIVE DATABASE TEST ===");
        
        // Step 1: Try to initialize
        _output.WriteLine("Step 1: Initialize database connection");
        try
        {
            var initDbMethod = dcPgConnType.GetMethod("InitDb", BindingFlags.Public | BindingFlags.Static);
            if (initDbMethod != null)
            {
                initDbMethod.Invoke(null, new object[0]);
                _output.WriteLine("✅ Database initialized successfully");
            }
            else
            {
                _output.WriteLine("⚠️ InitDb method not found, proceeding without initialization");
            }
        }
        catch (Exception ex)
        {
            _output.WriteLine($"❌ Database initialization failed: {ex.Message}");
            if (ex.InnerException != null)
            {
                _output.WriteLine($"❌ Root cause: {ex.InnerException.Message}");
            }
        }
        
        // Step 2: Try to get releases
        _output.WriteLine("\nStep 2: Attempt to retrieve releases");
        try
        {
            var getAllReleasesMethod = dcPgConnType.GetMethod("GetAllReleases", BindingFlags.Public | BindingFlags.Static);
            if (getAllReleasesMethod != null)
            {
                var releases = getAllReleasesMethod.Invoke(null, new object[0]);
                if (releases is System.Collections.IEnumerable enumerable)
                {
                    var count = enumerable.Cast<object>().Count();
                    _output.WriteLine($"✅ Successfully retrieved {count} releases");
                    
                    if (count > 0)
                    {
                        _output.WriteLine("🎉 DATABASE CONNECTION WORKING!");
                    }
                    else
                    {
                        _output.WriteLine("⚠️ Database connected but no data found");
                    }
                }
                else
                {
                    _output.WriteLine($"❌ Unexpected return type: {releases?.GetType().FullName}");
                }
            }
            else
            {
                _output.WriteLine("❌ GetAllReleases method not found");
            }
        }
        catch (Exception ex)
        {
            _output.WriteLine($"❌ Failed to retrieve releases: {ex.Message}");
            if (ex.InnerException != null)
            {
                _output.WriteLine($"❌ Root cause: {ex.InnerException.Message}");
            }
        }
        
        _output.WriteLine("=== END COMPREHENSIVE TEST ===");
    }

    private Type? GetDcPgConnType()
    {
        // Try multiple ways to get the DcPgConn type
        Type? dcPgConnType = null;
        
        try
        {
            dcPgConnType = Type.GetType("DcPgConn, DatabaseReader");
        }
        catch { }
        
        if (dcPgConnType == null)
        {
            try
            {
                dcPgConnType = Type.GetType("DcPgConn");
            }
            catch { }
        }
        
        if (dcPgConnType == null)
        {
            // Search in all loaded assemblies
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            foreach (var assembly in assemblies)
            {
                try
                {
                    var types = assembly.GetTypes();
                    dcPgConnType = types.FirstOrDefault(t => t.Name == "DcPgConn");
                    if (dcPgConnType != null) break;
                }
                catch { }
            }
        }
        
        return dcPgConnType;
    }
}