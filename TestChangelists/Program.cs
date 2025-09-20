using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("=== CHANGELIST TESTING UNIT - CORRECT FLOW ===");
        Console.WriteLine("Testing the correct flow:");
        Console.WriteLine("1. GetAllReleases -> find releaseId for dcn6_0");
        Console.WriteLine("2. GetReportsForRelease(releaseId) -> check if dcn_core exists");
        Console.WriteLine("3. GetAllReportsForRelease(releaseId, 'code_cov') -> find dcn_core entry");
        Console.WriteLine("4. GetChangelistsForReport(releaseId, 'dcn_core')");
        Console.WriteLine();

        // Follow the exact flow
        TestCompleteFlow();
        
        Console.WriteLine("\n=== END OF TESTS ===");
        Console.WriteLine("Press any key to exit...");
        Console.ReadKey();
    }
    
    static void TestCompleteFlow()
    {
        Console.WriteLine("🔄 TESTING COMPLETE FLOW:");
        try
        {
            var dcPgConnType = Type.GetType("DcPgConn, TempDbReader");
            if (dcPgConnType == null)
            {
                Console.WriteLine("❌ ERROR: Could not find DcPgConn type");
                return;
            }
            
            // Step 1: Get all releases and find dcn6_0
            Console.WriteLine("\n📋 Step 1: Getting all releases...");
            var getAllReleasesMethod = dcPgConnType.GetMethod("GetAllReleases", new[] { typeof(int?) });
            if (getAllReleasesMethod == null)
            {
                Console.WriteLine("❌ GetAllReleases method not found");
                return;
            }
            
            var releasesResult = getAllReleasesMethod.Invoke(null, new object[] { 100 });
            if (releasesResult == null)
            {
                Console.WriteLine("❌ GetAllReleases returned null");
                return;
            }
            
            int? dcn6ReleaseId = null;
            if (releasesResult is System.Collections.IEnumerable releasesEnum)
            {
                var releases = releasesEnum.Cast<object>().ToList();
                Console.WriteLine($"✅ Found {releases.Count} releases");
                
                foreach (var release in releases)
                {
                    Console.WriteLine($"  Release: {release}");
                    var releaseStr = release?.ToString() ?? "";
                    
                    // Look for exact match "dcn6_0" (not "dcn6_0_mg" or other variants)
                    if (releaseStr.Contains("ReleaseName = dcn6_0 }"))
                    {
                        // Parse format like "Release { ReleaseId = 325, ReleaseName = dcn6_0 }"
                        if (releaseStr.Contains("ReleaseId = "))
                        {
                            var startIndex = releaseStr.IndexOf("ReleaseId = ") + "ReleaseId = ".Length;
                            var endIndex = releaseStr.IndexOf(",", startIndex);
                            if (endIndex > startIndex)
                            {
                                var idStr = releaseStr.Substring(startIndex, endIndex - startIndex).Trim();
                                if (int.TryParse(idStr, out int releaseId))
                                {
                                    dcn6ReleaseId = releaseId;
                                    Console.WriteLine($"  🎯 Found EXACT dcn6_0 match with releaseId: {dcn6ReleaseId}");
                                    break;
                                }
                            }
                        }
                    }
                }
            }
            
            if (!dcn6ReleaseId.HasValue)
            {
                Console.WriteLine("❌ Could not find dcn6_0 release ID");
                return;
            }
            
            // Step 2: GetReportsForRelease to check if dcn_core exists
            Console.WriteLine($"\n📋 Step 2: Getting reports for release {dcn6ReleaseId}...");
            var getReportsForReleaseMethod = dcPgConnType.GetMethod("GetReportsForRelease", new[] { typeof(int) });
            if (getReportsForReleaseMethod == null)
            {
                Console.WriteLine("❌ GetReportsForRelease method not found");
                return;
            }
            
            var reportsResult = getReportsForReleaseMethod.Invoke(null, new object[] { dcn6ReleaseId.Value });
            if (reportsResult != null && reportsResult is System.Collections.IEnumerable reportsEnum)
            {
                var reports = reportsEnum.Cast<object>().ToList();
                Console.WriteLine($"✅ Found {reports.Count} reports for release {dcn6ReleaseId}");
                
                Console.WriteLine("📋 ALL REPORTS FROM GetReportsForRelease:");
                bool dcnCoreFound = false;
                int reportIndex = 0;
                foreach (var report in reports)
                {
                    reportIndex++;
                    Console.WriteLine($"  [{reportIndex}] Report: {report}");
                    if (report?.ToString()?.Contains("dcn_core") == true)
                    {
                        dcnCoreFound = true;
                        Console.WriteLine($"      🎯 *** FOUND dcn_core in this report! ***");
                    }
                }
                
                if (!dcnCoreFound)
                {
                    Console.WriteLine("❌ dcn_core not found in any of the reports listed above");
                }
            }
            else
            {
                Console.WriteLine("❌ GetReportsForRelease returned null or empty");
            }
            
            // Step 3: GetAllReportsForRelease with releaseId and covType
            Console.WriteLine($"\n📋 Step 3: Getting all reports for release {dcn6ReleaseId} with code_cov...");
            var getAllReportsForReleaseMethod = dcPgConnType.GetMethod("GetAllReportsForRelease", new[] { typeof(int), typeof(string) });
            if (getAllReportsForReleaseMethod == null)
            {
                Console.WriteLine("❌ GetAllReportsForRelease(int, string) method not found");
                return;
            }
            
            var allReportsResult = getAllReportsForReleaseMethod.Invoke(null, new object[] { dcn6ReleaseId.Value, "code_cov" });
            if (allReportsResult != null && allReportsResult is System.Collections.IEnumerable allReportsEnum)
            {
                var allReports = allReportsEnum.Cast<object>().ToList();
                Console.WriteLine($"✅ Found {allReports.Count} reports for release {dcn6ReleaseId} with code_cov");
                
                Console.WriteLine("📋 ALL REPORTS FROM GetAllReportsForRelease:");
                int reportIndex = 0;
                foreach (var report in allReports)
                {
                    reportIndex++;
                    Console.WriteLine($"  [{reportIndex}] Report: {report}");
                    if (report?.ToString()?.Contains("dcn_core") == true)
                    {
                        Console.WriteLine($"      🎯 *** FOUND dcn_core in this report! ***");
                    }
                }
            }
            else
            {
                Console.WriteLine("❌ GetAllReportsForRelease returned null or empty");
            }
            
            // Step 4: GetChangelistsForReport with releaseId and reportName
            Console.WriteLine($"\n📋 Step 4: Getting changelists for release {dcn6ReleaseId} and report 'dcn_core'...");
            var getChangelistsMethod = dcPgConnType.GetMethod("GetChangelistsForReport", new[] { typeof(int), typeof(string), typeof(int?) });
            if (getChangelistsMethod == null)
            {
                Console.WriteLine("❌ GetChangelistsForReport method not found");
                return;
            }
            
            var changelistsResult = getChangelistsMethod.Invoke(null, new object[] { dcn6ReleaseId.Value, "dcn_core", 100 });
            if (changelistsResult != null && changelistsResult is System.Collections.IEnumerable changelistsEnum)
            {
                var changelists = changelistsEnum.Cast<object>().ToList();
                Console.WriteLine($"✅ SUCCESS! Found {changelists.Count} changelists:");
                
                foreach (var changelist in changelists.Take(10)) // Show first 10
                {
                    Console.WriteLine($"  📋 Changelist: '{changelist}'");
                }
                
                if (changelists.Count > 10)
                {
                    Console.WriteLine($"  ... and {changelists.Count - 10} more");
                }
            }
            else
            {
                Console.WriteLine("❌ GetChangelistsForReport returned null or empty");
            }
            
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ ERROR in TestCompleteFlow: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
        }
    }
    
    static void InspectDllMethods()
    {
        Console.WriteLine("🔍 INSPECTING DLL METHODS:");
        try
        {
            var dcPgConnType = Type.GetType("DcPgConn, TempDbReader");
            if (dcPgConnType == null)
            {
                Console.WriteLine("❌ ERROR: Could not find DcPgConn type in TempDbReader");
                return;
            }
            
            Console.WriteLine($"✅ Found DcPgConn type: {dcPgConnType.FullName}");
            Console.WriteLine($"Assembly: {dcPgConnType.Assembly.FullName}");
            Console.WriteLine($"Assembly Location: {dcPgConnType.Assembly.Location}");
            
            var allMethods = dcPgConnType.GetMethods(BindingFlags.Public | BindingFlags.Static);
            Console.WriteLine($"Total public static methods: {allMethods.Length}");
            
            // Show all methods containing 'changelist'
            var changelistMethods = allMethods.Where(m => m.Name.ToLower().Contains("changelist")).ToArray();
            Console.WriteLine($"\n📋 Methods containing 'changelist': {changelistMethods.Length}");
            foreach (var method in changelistMethods)
            {
                var parameters = string.Join(", ", method.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"));
                Console.WriteLine($"  ✓ {method.Name}({parameters}) → {method.ReturnType.Name}");
            }
            
            // Show methods containing 'report'
            var reportMethods = allMethods.Where(m => m.Name.ToLower().Contains("report")).Take(10).ToArray();
            Console.WriteLine($"\n📋 Methods containing 'report' (first 10): {reportMethods.Length}");
            foreach (var method in reportMethods)
            {
                var parameters = string.Join(", ", method.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"));
                Console.WriteLine($"  • {method.Name}({parameters}) → {method.ReturnType.Name}");
            }
            
            // Show methods containing 'release'
            var releaseMethods = allMethods.Where(m => m.Name.ToLower().Contains("release")).Take(10).ToArray();
            Console.WriteLine($"\n📋 Methods containing 'release' (first 10): {releaseMethods.Length}");
            foreach (var method in releaseMethods)
            {
                var parameters = string.Join(", ", method.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"));
                Console.WriteLine($"  • {method.Name}({parameters}) → {method.ReturnType.Name}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ ERROR inspecting DLL: {ex.Message}");
        }
    }
    
    static void TestGetReleases()
    {
        Console.WriteLine("🧪 TESTING RELEASE DISCOVERY:");
        try
        {
            var dcPgConnType = Type.GetType("DcPgConn, TempDbReader");
            if (dcPgConnType == null)
            {
                Console.WriteLine("❌ ERROR: Could not find DcPgConn type");
                return;
            }
            
            // Look for release methods
            var releaseMethods = dcPgConnType.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(m => m.Name.ToLower().Contains("release") && m.GetParameters().Length == 0)
                .ToArray();
                
            Console.WriteLine($"Found {releaseMethods.Length} release methods with no parameters:");
            foreach (var method in releaseMethods)
            {
                Console.WriteLine($"  • {method.Name}() → {method.ReturnType.Name}");
                
                try
                {
                    var result = method.Invoke(null, null);
                    if (result != null)
                    {
                        if (result is System.Collections.IEnumerable enumerable)
                        {
                            var items = enumerable.Cast<object>().Take(5).ToList();
                            Console.WriteLine($"    Result: {items.Count} items (first 5): [{string.Join(", ", items.Select(i => i?.ToString()))}]");
                            
                            // Check if dcn6_0 is in the results
                            var allItems = enumerable.Cast<object>().ToList();
                            var dcn6Found = allItems.Any(item => item?.ToString()?.Contains("dcn6_0") == true);
                            Console.WriteLine($"    Contains 'dcn6_0': {dcn6Found}");
                        }
                        else
                        {
                            Console.WriteLine($"    Result: {result}");
                        }
                    }
                    else
                    {
                        Console.WriteLine($"    Result: NULL");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"    ❌ Error calling method: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ ERROR in TestGetReleases: {ex.Message}");
        }
    }
    
    static void TestGetReports()
    {
        Console.WriteLine("🧪 TESTING REPORT DISCOVERY:");
        try
        {
            var dcPgConnType = Type.GetType("DcPgConn, TempDbReader");
            if (dcPgConnType == null)
            {
                Console.WriteLine("❌ ERROR: Could not find DcPgConn type");
                return;
            }
            
            // Look for report methods that take parameters
            var reportMethods = dcPgConnType.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(m => m.Name.ToLower().Contains("report") && m.GetParameters().Length >= 1)
                .ToArray();
                
            Console.WriteLine($"Found {reportMethods.Length} report methods with parameters:");
            foreach (var method in reportMethods)
            {
                var parameters = string.Join(", ", method.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"));
                Console.WriteLine($"  • {method.Name}({parameters}) → {method.ReturnType.Name}");
                
                // Try to call methods that might get reports for a release
                if (method.GetParameters().Length == 2)
                {
                    var param1Type = method.GetParameters()[0].ParameterType;
                    var param2Type = method.GetParameters()[1].ParameterType;
                    
                    if ((param1Type == typeof(int) || param1Type == typeof(string)) && param2Type == typeof(string))
                    {
                        Console.WriteLine($"    🎯 Trying to call with test parameters...");
                        try
                        {
                            object[] args;
                            if (param1Type == typeof(int))
                            {
                                args = new object[] { 1, "code_cov" }; // Assuming release ID 1 for dcn6_0
                            }
                            else
                            {
                                args = new object[] { "dcn6_0", "code_cov" };
                            }
                            
                            var result = method.Invoke(null, args);
                            if (result != null)
                            {
                                if (result is System.Collections.IEnumerable enumerable)
                                {
                                    var items = enumerable.Cast<object>().Take(3).ToList();
                                    Console.WriteLine($"      Result: {items.Count} items (first 3): [{string.Join(", ", items.Select(i => i?.ToString()))}]");
                                    
                                    // Check if dcn_core is in the results
                                    var allItems = enumerable.Cast<object>().ToList();
                                    var dcnCoreFound = allItems.Any(item => item?.ToString()?.Contains("dcn_core") == true);
                                    Console.WriteLine($"      Contains 'dcn_core': {dcnCoreFound}");
                                }
                                else
                                {
                                    Console.WriteLine($"      Result: {result}");
                                }
                            }
                            else
                            {
                                Console.WriteLine($"      Result: NULL");
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"      ❌ Error calling method: {ex.Message}");
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ ERROR in TestGetReports: {ex.Message}");
        }
    }
    
    static void TestGetChangelists()
    {
        Console.WriteLine("🧪 TESTING CHANGELIST DISCOVERY:");
        try
        {
            var dcPgConnType = Type.GetType("DcPgConn, TempDbReader");
            if (dcPgConnType == null)
            {
                Console.WriteLine("❌ ERROR: Could not find DcPgConn type");
                return;
            }
            
            // Look for changelist methods
            var changelistMethods = dcPgConnType.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(m => m.Name.ToLower().Contains("changelist"))
                .ToArray();
                
            Console.WriteLine($"Found {changelistMethods.Length} changelist methods:");
            foreach (var method in changelistMethods)
            {
                var parameters = string.Join(", ", method.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"));
                Console.WriteLine($"  • {method.Name}({parameters}) → {method.ReturnType.Name}");
                
                // Try to call methods that might get changelists
                if (method.GetParameters().Length == 2)
                {
                    var param1Type = method.GetParameters()[0].ParameterType;
                    var param2Type = method.GetParameters()[1].ParameterType;
                    
                    if (param1Type == typeof(int) && param2Type == typeof(string))
                    {
                        Console.WriteLine($"    🎯 Trying to call with test parameters (reportId=22287, reportType='code_cov')...");
                        try
                        {
                            var result = method.Invoke(null, new object[] { 22287, "code_cov", 100 });
                            if (result != null)
                            {
                                if (result is System.Collections.IEnumerable enumerable)
                                {
                                    var items = enumerable.Cast<object>().Take(10).ToList();
                                    Console.WriteLine($"      ✅ Result: {items.Count} changelist items: [{string.Join(", ", items.Select(i => $"'{i?.ToString()}'"))}]");
                                }
                                else
                                {
                                    Console.WriteLine($"      Result: {result}");
                                }
                            }
                            else
                            {
                                Console.WriteLine($"      Result: NULL");
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"      ❌ Error calling method: {ex.Message}");
                        }
                    }
                    else if (param1Type == typeof(string) && param2Type == typeof(string))
                    {
                        Console.WriteLine($"    🎯 Trying to call with test parameters (reportName='dcn_core', reportType='code_cov')...");
                        try
                        {
                            var result = method.Invoke(null, new object[] { "dcn_core", "code_cov" });
                            if (result != null)
                            {
                                if (result is System.Collections.IEnumerable enumerable)
                                {
                                    var items = enumerable.Cast<object>().Take(10).ToList();
                                    Console.WriteLine($"      ✅ Result: {items.Count} changelist items: [{string.Join(", ", items.Select(i => $"'{i?.ToString()}'"))}]");
                                }
                                else
                                {
                                    Console.WriteLine($"      Result: {result}");
                                }
                            }
                            else
                            {
                                Console.WriteLine($"      Result: NULL");
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"      ❌ Error calling method: {ex.Message}");
                        }
                    }
                }
            }
            
            // Also try looking for any method that might return changelists
            Console.WriteLine("\n🔍 Looking for other methods that might return changelist data:");
            var allMethods = dcPgConnType.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(m => m.GetParameters().Length >= 1 && m.GetParameters().Length <= 3)
                .Take(20); // Limit to avoid spam
                
            foreach (var method in allMethods)
            {
                if (method.Name.ToLower().Contains("get") && !method.Name.ToLower().Contains("report") && !method.Name.ToLower().Contains("release"))
                {
                    var parameters = string.Join(", ", method.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"));
                    Console.WriteLine($"  ? {method.Name}({parameters}) → {method.ReturnType.Name}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ ERROR in TestGetChangelists: {ex.Message}");
        }
    }
}