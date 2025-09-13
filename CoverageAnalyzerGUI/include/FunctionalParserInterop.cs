using System;
using System.Runtime.InteropServices;

namespace CoverageAnalyzerGUI.Interop
{
    /// <summary>
    /// P/Invoke wrapper for FunctionalCoverageParsers.dll
    /// Based on the DLL_DOCUMENTATION.md documentation
    /// </summary>
    public static class FunctionalParserInterop
    {
        private const string DllName = "FunctionalCoverageParsers.dll";

        // Basic data structures for functional coverage
        [StructLayout(LayoutKind.Sequential)]
        public struct FunctionalCoverageInfo
        {
            public int TotalCoverageGroups;
            public int TotalCoveragePoints;
            public double CoveragePercentage;
            public int ProcessingTimeMs;
        }

        // DLL function declarations (placeholder - will need actual exports)
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern bool InitializeFunctionalParser([MarshalAs(UnmanagedType.LPStr)] string dataPath);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern bool GetFunctionalCoverageInfo(out FunctionalCoverageInfo info);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern bool ParseFunctionalCoverageFile([MarshalAs(UnmanagedType.LPStr)] string filePath);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void CleanupFunctionalParser();
    }
}