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
            
            if (releases.Count > 0)
            {
                var firstRelease = releases[0];
                
                // Test with functional coverage type
                var reports = GetAllReportsForRelease(firstRelease.ReleaseId, "func_cov");
                Assert.NotNull(reports);
                
                if (reports.Count > 0)
                {
                    var firstReport = reports[0];
                    // Report test completed successfully
                }
            }
        }
    }
}
