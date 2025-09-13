using System;
using System.Runtime.InteropServices;

namespace CoverageAnalyzerGUI.Interop
{
    /// <summary>
    /// P/Invoke wrapper for ExclusionParser.dll
    /// Based on the PROJECT_SUMMARY.md documentation
    /// </summary>
    public static class ExclusionParserInterop
    {
        private const string DllName = "ExclusionParser.dll";

        // Basic data structures for exclusion data
        [StructLayout(LayoutKind.Sequential)]
        public struct ExclusionInfo
        {
            public int TotalExclusions;
            public int ActiveRules;
            public int ProcessedFiles;
        }

        // DLL function declarations (placeholder - will need actual exports)
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern bool InitializeExclusionParser([MarshalAs(UnmanagedType.LPStr)] string configPath);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern bool GetExclusionInfo(out ExclusionInfo info);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern bool ProcessExclusionFile([MarshalAs(UnmanagedType.LPStr)] string filePath);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void CleanupExclusionParser();
    }
}