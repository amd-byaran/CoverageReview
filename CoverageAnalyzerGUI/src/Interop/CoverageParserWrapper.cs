using System;
using System.Runtime.InteropServices;
using System.Text;

namespace CoverageAnalyzerGUI.Interop
{
    /// <summary>
    /// P/Invoke wrapper for CoverageParser.dll
    /// </summary>
    public static class CoverageParserWrapper
    {
        private const string DLL_NAME = "CoverageParser.dll";

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr ParseHierarchyFile([MarshalAs(UnmanagedType.LPStr)] string filePath);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern void FreeHierarchyData(IntPtr data);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern int GetNodeCount(IntPtr data);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr GetNodeName(IntPtr data, int index);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr GetNodePath(IntPtr data, int index);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern int GetNodeType(IntPtr data, int index);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern double GetCoveragePercentage(IntPtr data, int index);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern int GetHitCount(IntPtr data, int index);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern int GetTotalCount(IntPtr data, int index);

        // Helper method to convert IntPtr string to managed string
        public static string PtrToString(IntPtr ptr)
        {
            if (ptr == IntPtr.Zero)
                return string.Empty;
            
            return Marshal.PtrToStringAnsi(ptr) ?? string.Empty;
        }
    }
}