using System;
using System.Runtime.InteropServices;

namespace CoverageAnalyzerGUI.Interop
{
    /// <summary>
    /// P/Invoke wrapper for CoverageParser.dll
    /// Based on the PARSER_ARCHITECTURE.md documentation
    /// </summary>
    public static class CoverageParserInterop
    {
        private const string DllName = "CoverageParser.dll";

        // Basic data structures based on documentation
        [StructLayout(LayoutKind.Sequential)]
        public struct DataSummary
        {
            public int TotalModules;
            public int TotalInstances;
            public int TotalTests;
            public int InitializationTime;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct PerformanceStats
        {
            public int InitializationTimeMs;
            public double MemoryEfficiency;
            public int ParsedLines;
        }

        // DLL function declarations (will need to be updated based on actual DLL exports)
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern bool InitializeParsers([MarshalAs(UnmanagedType.LPStr)] string dataDirectory);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern bool GetDataSummary(out DataSummary summary);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern bool GetPerformanceStats(out PerformanceStats stats);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr FindHierarchyNode([MarshalAs(UnmanagedType.LPStr)] string path);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void Cleanup();

        // Helper methods for safe string handling
        public static string GetStringFromPtr(IntPtr ptr)
        {
            return ptr != IntPtr.Zero ? Marshal.PtrToStringAnsi(ptr) ?? string.Empty : string.Empty;
        }
    }
}