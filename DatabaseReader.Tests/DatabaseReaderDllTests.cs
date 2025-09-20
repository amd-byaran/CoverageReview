using System.Reflection;
using Xunit;
using Xunit.Abstractions;

namespace DatabaseReader.Tests;

public class DatabaseReaderDllTests
{
    private readonly ITestOutputHelper _output;

    public DatabaseReaderDllTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void DllShouldLoad()
    {
        // Test that we can load the DatabaseReader.dll
        _output.WriteLine("=== Testing DatabaseReader.dll Loading ===");
        
        // Try to get the DcPgConn type
        var dcPgConnType = typeof(DcPgConn);
        
        Assert.NotNull(dcPgConnType);
        _output.WriteLine($"✅ DcPgConn type loaded: {dcPgConnType.FullName}");
        _output.WriteLine($"✅ Assembly: {dcPgConnType.Assembly.FullName}");
        _output.WriteLine($"✅ Assembly Location: {dcPgConnType.Assembly.Location}");
    }

    [Fact]
    public void DcPgConnShouldHaveExpectedMethods()
    {
        // Test that DcPgConn has the expected public static methods
        _output.WriteLine("=== Testing DcPgConn Methods ===");
        
        var dcPgConnType = typeof(DcPgConn);
        var methods = dcPgConnType.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(m => !m.Name.StartsWith("get_") && !m.Name.StartsWith("set_"))
            .OrderBy(m => m.Name)
            .ToArray();

        _output.WriteLine($"📋 Found {methods.Length} public static methods:");
        
        // List all methods for debugging
        foreach (var method in methods)
        {
            var parameters = string.Join(", ", method.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"));
            _output.WriteLine($"  • {method.Name}({parameters}) -> {method.ReturnType.Name}");
        }

        // Check for essential methods
        var expectedMethods = new[]
        {
            "InitDb",
            "CloseDb", 
            "GetAllReleases",
            "GetAllReportsForRelease",
            "GetChangelistsForReport",
            "GetReportPath"
        };

        foreach (var expectedMethod in expectedMethods)
        {
            var foundMethod = methods.FirstOrDefault(m => m.Name == expectedMethod);
            Assert.NotNull(foundMethod);
            _output.WriteLine($"✅ {expectedMethod}: Found");
        }
    }

    [Fact]
    public void InitDbShouldNotThrowTypeLoadException()
    {
        // Test that calling InitDb doesn't throw a TypeLoadException (which would indicate DLL loading issues)
        _output.WriteLine("=== Testing InitDb Method Call ===");
        
        var dcPgConnType = typeof(DcPgConn);
        var initDbMethod = dcPgConnType.GetMethod("InitDb", BindingFlags.Public | BindingFlags.Static);
        
        Assert.NotNull(initDbMethod);
        _output.WriteLine("✅ InitDb method found");

        // Try to call InitDb - we expect it might throw a database connection error, 
        // but it should NOT throw TypeLoadException or method not found exceptions
        try
        {
            initDbMethod.Invoke(null, null);
            _output.WriteLine("✅ InitDb called successfully (database connected)");
        }
        catch (TargetInvocationException ex) when (ex.InnerException != null)
        {
            // This is expected - we likely don't have a database configured
            // But the important thing is that the method exists and can be called
            _output.WriteLine($"⚠️ InitDb threw expected database error: {ex.InnerException.GetType().Name}");
            _output.WriteLine($"⚠️ Error message: {ex.InnerException.Message}");
            
            // These exceptions are fine - they indicate the DLL is loaded properly but database config is missing
            var acceptableExceptions = new[]
            {
                typeof(ArgumentException),
                typeof(InvalidOperationException),
                typeof(TimeoutException),
                typeof(System.Net.Sockets.SocketException),
                typeof(Npgsql.NpgsqlException)
            };
            
            Assert.Contains(ex.InnerException.GetType(), acceptableExceptions);
            _output.WriteLine("✅ Exception type is acceptable (indicates DLL loaded properly)");
        }
        catch (TypeLoadException ex)
        {
            _output.WriteLine($"❌ TypeLoadException: {ex.Message}");
            throw new Exception("DLL loading failed - dependencies missing", ex);
        }
        catch (FileNotFoundException ex)
        {
            _output.WriteLine($"❌ FileNotFoundException: {ex.Message}");
            throw new Exception("Required DLL files not found", ex);
        }
    }

    [Fact]
    public void GetAllReleasesShouldBeCallable()
    {
        // Test that GetAllReleases method exists and can be called
        _output.WriteLine("=== Testing GetAllReleases Method ===");
        
        var dcPgConnType = typeof(DcPgConn);
        var getAllReleasesMethod = dcPgConnType.GetMethod("GetAllReleases", new[] { typeof(int?) });
        
        if (getAllReleasesMethod == null)
        {
            // Try without parameters
            getAllReleasesMethod = dcPgConnType.GetMethod("GetAllReleases", new Type[0]);
        }
        
        Assert.NotNull(getAllReleasesMethod);
        _output.WriteLine($"✅ GetAllReleases method found: {getAllReleasesMethod.Name}");
        
        // Test method signature
        var parameters = getAllReleasesMethod.GetParameters();
        _output.WriteLine($"📋 Method signature: {getAllReleasesMethod.Name}({string.Join(", ", parameters.Select(p => $"{p.ParameterType.Name} {p.Name}"))})");
        
        // Don't actually call it since we don't have database configured, but verify it's callable
        Assert.True(getAllReleasesMethod.IsStatic);
        Assert.True(getAllReleasesMethod.IsPublic);
        _output.WriteLine("✅ Method is public and static as expected");
    }

    [Fact]
    public void DependenciesShouldBeAvailable()
    {
        // Test that required dependencies are available
        _output.WriteLine("=== Testing Dependencies ===");
        
        // Test Npgsql
        try
        {
            var npgsqlType = typeof(Npgsql.NpgsqlConnection);
            Assert.NotNull(npgsqlType);
            _output.WriteLine("✅ Npgsql dependency available");
        }
        catch (Exception ex)
        {
            _output.WriteLine($"❌ Npgsql dependency missing: {ex.Message}");
            throw;
        }
        
        // Test Microsoft.Extensions.Logging.Abstractions
        try
        {
            var loggerType = typeof(Microsoft.Extensions.Logging.ILogger);
            Assert.NotNull(loggerType);
            _output.WriteLine("✅ Microsoft.Extensions.Logging.Abstractions dependency available");
        }
        catch (Exception ex)
        {
            _output.WriteLine($"❌ Microsoft.Extensions.Logging.Abstractions dependency missing: {ex.Message}");
            throw;
        }
        
        // Test Microsoft.Extensions.DependencyInjection.Abstractions
        try
        {
            var serviceType = typeof(Microsoft.Extensions.DependencyInjection.IServiceCollection);
            Assert.NotNull(serviceType);
            _output.WriteLine("✅ Microsoft.Extensions.DependencyInjection.Abstractions dependency available");
        }
        catch (Exception ex)
        {
            _output.WriteLine($"❌ Microsoft.Extensions.DependencyInjection.Abstractions dependency missing: {ex.Message}");
            throw;
        }
    }

    [Fact]
    public void AssemblyVersionShouldBeCorrect()
    {
        // Test assembly version and metadata
        _output.WriteLine("=== Testing Assembly Information ===");
        
        var dcPgConnType = typeof(DcPgConn);
        var assembly = dcPgConnType.Assembly;
        
        _output.WriteLine($"📋 Assembly Full Name: {assembly.FullName}");
        _output.WriteLine($"📋 Assembly Location: {assembly.Location}");
        _output.WriteLine($"📋 Assembly Version: {assembly.GetName().Version}");
        
        // Verify it's the correct assembly
        Assert.Contains("DatabaseReader", assembly.FullName ?? string.Empty);
        _output.WriteLine("✅ Assembly name contains 'DatabaseReader'");
        
        // Check if it's .NET 8.0 assembly (since that's what we built)
        var targetFramework = assembly.GetCustomAttribute<System.Runtime.Versioning.TargetFrameworkAttribute>();
        if (targetFramework != null)
        {
            _output.WriteLine($"📋 Target Framework: {targetFramework.FrameworkName}");
        }
    }
}