using Xunit;
using static DcPgConn;
using System;

namespace DatabaseConnectivityTests
{
    public class DatabaseTests
    {
        [Fact]
        public void TestDatabaseInitialization()
        {
            // Test that InitDb() runs without throwing exceptions
            var exception = Record.Exception(() => InitDb());
            Assert.Null(exception);
        }

        [Fact]
        public void TestDatabaseConnection()
        {
            // Test comprehensive database connectivity
            var initException = Record.Exception(() => InitDb());
            Assert.Null(initException);
            
            var releases = GetAllReleases(3);
            Assert.NotNull(releases);
            
            Console.WriteLine($"Retrieved {releases.Count} releases from database");
            
            if (releases.Count > 0)
            {
                var firstRelease = releases[0];
                Console.WriteLine($"First release: {firstRelease.ReleaseName} (ID: {firstRelease.ReleaseId})");
                
                // Test with functional coverage type
                var reports = GetAllReportsForRelease(firstRelease.ReleaseId, "func_cov");
                Assert.NotNull(reports);
                
                Console.WriteLine($"Found {reports.Count} functional coverage reports for release {firstRelease.ReleaseName}");
                
                if (reports.Count > 0)
                {
                    var firstReport = reports[0];
                    Console.WriteLine($"First report: Project='{firstReport.Item3}', Name='{firstReport.Item4}'");
                }
            }
        }
    }
}
