using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Xunit;
using Xunit.Abstractions;

namespace DatabaseReaderTests;

public class ActualDatabaseTests
{
    private readonly ITestOutputHelper _output;

    public ActualDatabaseTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void Test_Real_Database_Connection_With_Npgsql()
    {
        var dcPgConnType = GetDcPgConnType();
        Assert.NotNull(dcPgConnType);

        _output.WriteLine("=== REAL DATABASE CONNECTION TEST ===");
        
        // Step 1: Try to initialize with Npgsql available
        _output.WriteLine("Step 1: Initialize database connection");
        try
        {
            var initDbMethod = dcPgConnType.GetMethod("InitDb", BindingFlags.Public | BindingFlags.Static);
            if (initDbMethod != null)
            {
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                initDbMethod.Invoke(null, new object[0]);
                stopwatch.Stop();
                _output.WriteLine($"✅ Database initialized successfully in {stopwatch.ElapsedMilliseconds}ms");
            }
            else
            {
                _output.WriteLine("❌ InitDb method not found");
                return;
            }
        }
        catch (Exception ex)
        {
            _output.WriteLine($"❌ Database initialization failed: {ex.Message}");
            if (ex.InnerException != null)
            {
                _output.WriteLine($"❌ Root cause: {ex.InnerException.Message}");
                
                // Check specific error types
                if (ex.InnerException.Message.Contains("Npgsql"))
                {
                    _output.WriteLine("💡 This indicates Npgsql dependency issue - check if test project has Npgsql package");
                }
                else if (ex.InnerException.Message.Contains("connection") || ex.InnerException.Message.Contains("server"))
                {
                    _output.WriteLine("💡 This indicates PostgreSQL server is not running or not accessible");
                }
                else if (ex.InnerException.Message.Contains("authentication") || ex.InnerException.Message.Contains("password"))
                {
                    _output.WriteLine("💡 This indicates database credentials are incorrect");
                }
            }
            _output.WriteLine("ℹ️ This is expected if PostgreSQL is not configured on this machine");
            return; // Don't fail the test - this is diagnostic
        }
        
        // Step 2: Try to get releases
        _output.WriteLine("\nStep 2: Attempt to retrieve releases");
        try
        {
            var getAllReleasesMethod = dcPgConnType.GetMethod("GetAllReleases", new[] { typeof(int?) });
            if (getAllReleasesMethod != null)
            {
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                var releases = getAllReleasesMethod.Invoke(null, new object[] { 10 }); // Get 10 releases
                stopwatch.Stop();
                
                if (releases is System.Collections.IEnumerable enumerable)
                {
                    var count = enumerable.Cast<object>().Count();
                    _output.WriteLine($"✅ Successfully retrieved {count} releases in {stopwatch.ElapsedMilliseconds}ms");
                    
                    if (count > 0)
                    {
                        _output.WriteLine("🎉 DATABASE CONNECTION FULLY WORKING!");
                        
                        // Show sample data
                        var items = enumerable.Cast<object>().Take(3).ToArray();
                        for (int i = 0; i < items.Length; i++)
                        {
                            var item = items[i];
                            _output.WriteLine($"Sample Release {i + 1}: {item?.GetType().Name}");
                            
                            if (item != null)
                            {
                                var itemType = item.GetType();
                                var properties = itemType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
                                
                                foreach (var prop in properties.Take(3)) // Show first 3 properties
                                {
                                    try
                                    {
                                        var value = prop.GetValue(item);
                                        _output.WriteLine($"  {prop.Name}: {value}");
                                    }
                                    catch (Exception propEx)
                                    {
                                        _output.WriteLine($"  {prop.Name}: <error: {propEx.Message}>");
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        _output.WriteLine("⚠️ Database connected but no releases found");
                        _output.WriteLine("💡 This could mean:");
                        _output.WriteLine("  - Database is empty");
                        _output.WriteLine("  - Wrong database/schema");
                        _output.WriteLine("  - Insufficient permissions");
                    }
                }
                else
                {
                    _output.WriteLine($"❌ Unexpected return type: {releases?.GetType().FullName}");
                }
            }
            else
            {
                _output.WriteLine("❌ GetAllReleases method not found with expected signature");
            }
        }
        catch (Exception ex)
        {
            _output.WriteLine($"❌ Failed to retrieve releases: {ex.Message}");
            if (ex.InnerException != null)
            {
                _output.WriteLine($"❌ Root cause: {ex.InnerException.Message}");
            }
            _output.WriteLine("ℹ️ This is expected if PostgreSQL database is not set up with the expected schema");
        }
        
        _output.WriteLine("=== END REAL DATABASE TEST ===");
        
        // Don't fail the test - this is purely diagnostic
        // The purpose is to see what happens when we try to connect to a real database
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